using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data.Combat;
using Game.Simulation.Power;
using Game.Simulation.Spatial;
using Game.Simulation.Weapons;
using Game.Tests.EditMode.Voxel.Damage;
using Game.Voxel.Damage;
using Game.Voxel.PresentationStreams;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Tests.EditMode.Simulation.Weapons
{
    /// <summary>WPN-008 required checks run the production scheduled step and stable compaction jobs.</summary>
    public sealed class ProjectileStepTests
    {
        sealed class Fixture : IDisposable
        {
            public readonly TestGridFactory Grids = new TestGridFactory();
            public ProjectileStore Store = ProjectileStore.Create(256);
            public SpatialHash Hash = SpatialHash.Create(128);
            public readonly Entity Target;
            public WeaponDef Weapon = new WeaponDef { Damage = 50, PenetrationCells = 2, ImpulsePerHit = 7, HomingTurnRateRad = .5f };
            public List<DamageEvent> Damage;
            public List<ShieldHit> Shields;
            public List<ExplosionRequest> Explosions;
            public List<FxEvent> Effects;
            public List<SfxEvent> Sounds;
            readonly SystemHandle probe;

            public Fixture()
            {
                Target = Grids.Spawn(Grids.Box(0, new int3(3)));
                probe = Grids.World.GetOrCreateSystem<HitTestProbeSystem>();
                Hash.Proxies.Add(new SpatialProxy { Entity = Target, Center = new float3(1), Radius = 8, HullRadius = 2, Kind = ProxyKind.Ship });
                // Query's large-proxy path is canonical and avoids rebuilding cell buckets in each small fixture.
                Hash.Large.Add(0);
            }
            public ProjectileRow Shot(uint id = 1) => new ProjectileRow { Position = new float3(-5, .5f, .5f), Velocity = new float3(600, 0, 0),
                ShotId = id, DeathTick = 100, Weapon = 1, Type = DamageType.Kinetic, DamageMultiplier = 2,
                Source = new Entity { Index = 99999, Version = 1 }, Instigator = new Entity { Index = 99998, Version = 2 } };
            public void Step(uint tick = 1)
            {
                using var builder = new BlobBuilder(Allocator.Temp);
                ref var root = ref builder.ConstructRoot<WeaponDefsBlob>(); var defs = builder.Allocate(ref root.Weapons, 2); defs[1] = Weapon;
                var blob = builder.CreateBlobAssetReference<WeaponDefsBlob>(Allocator.TempJob);
                int lanes = math.max(1, (Store.Rows.Length + 63) / 64);
                using var counts = new NativeArray<int2>(lanes, Allocator.TempJob);
                using var damage = new NativeStream(lanes, Allocator.TempJob);
                using var shields = new NativeStream(lanes, Allocator.TempJob);
                using var explosions = new NativeStream(lanes, Allocator.TempJob);
                using var effects = new NativeQueue<FxEvent>(Allocator.TempJob);
                using var sounds = new NativeQueue<SfxEvent>(Allocator.TempJob);
                try
                {
                    ref var state = ref Grids.World.Unmanaged.ResolveSystemStateRef(probe);
                    var hit = CombatHitTest.Create(ref state); hit.Update(ref state);
                    var job = new ProjectileStepJob { Rows = Store.Rows, BatchCounts = counts, HitTest = hit, Hash = Hash.AsReadOnly(),
                        Velocities = state.GetComponentLookup<PhysicsVelocity>(true), Weapons = blob, Tick = tick, MaxCandidates = 32,
                        Damage = damage.AsWriter(), Shields = shields.AsWriter(), Explosions = explosions.AsWriter(),
                        Fx = effects.AsParallelWriter(), Sfx = sounds.AsParallelWriter(),
                        Cues = new CombatAudioCues { ImpactKinetic = new ContentHash(StableHash.Hash64("cue_impact_hull")) } };
                    var step = job.ScheduleBatch(lanes * 64, 64, Store.Acquire(Hash.BeginRead(default)));
                    Hash.EndRead(step);
                    var compact = new ProjectileCompactJob { Rows = Store.Rows, Stats = Store.Stats, BatchCounts = counts }.Schedule(step);
                    Store.Release(compact); Store.CompleteAccess();
                    Damage = Read<DamageEvent>(damage); Shields = Read<ShieldHit>(shields); Explosions = Read<ExplosionRequest>(explosions);
                    Effects = new List<FxEvent>(); while (effects.TryDequeue(out var effect)) Effects.Add(effect);
                    Sounds = new List<SfxEvent>(); while (sounds.TryDequeue(out var sound)) Sounds.Add(sound);
                }
                finally { blob.Dispose(); }
            }
            static List<T> Read<T>(NativeStream stream) where T : unmanaged
            {
                var result = new List<T>(); var reader = stream.AsReader();
                for (int i = 0; i < reader.ForEachCount; i++)
                { int count = reader.BeginForEachIndex(i); for (int j = 0; j < count; j++) result.Add(reader.Read<T>()); reader.EndForEachIndex(); }
                return result;
            }
            public void Dispose() { Store.Dispose(); Hash.Dispose(); Grids.Dispose(); }
        }

        [Test] public void HullHit_Event()
        {
            using var f = new Fixture(); var row = f.Shot(); f.Store.Rows.Add(row); f.Step();
            Assert.AreEqual(1, f.Damage.Count); var hit = f.Damage[0]; Assert.AreEqual(f.Target, hit.Grid);
            Assert.That(math.distance(new float3(0,.5f,.5f), hit.LocalPoint), Is.LessThan(.001f));
            Assert.AreEqual(new float3(1,0,0), hit.LocalDir); Assert.AreEqual(100, hit.Amount); Assert.AreEqual(2, hit.Penetration);
            Assert.AreEqual(DamageType.Kinetic, hit.Type); Assert.AreEqual(ProducerKind.Projectile, hit.Producer);
            Assert.AreEqual(row.ShotId, hit.Sequence); Assert.AreEqual(row.Source, hit.Source); Assert.AreEqual(row.Instigator, hit.Instigator);
            Assert.AreEqual(new float3(7,0,0), hit.WorldImpulse); Assert.AreEqual(0, hit.Radius);
            Assert.AreEqual(1, f.Effects.Count); Assert.AreEqual(1, f.Sounds.Count); Assert.AreEqual(0, f.Store.Rows.Length);
            Assert.AreEqual(1, f.Store.Stats.Value.Hits);
        }
        [Test] public void ShieldHit_Event()
        {
            using var f = new Fixture(); f.Grids.EM.AddComponentData(f.Target, new ShieldState { Active=1, Hp=100, MaxHp=100, Center=new float3(1,.5f,.5f), Radii=new float3(3) });
            var row=f.Shot();f.Store.Rows.Add(row);f.Step();Assert.AreEqual(0,f.Damage.Count);Assert.AreEqual(1,f.Shields.Count);
            var hit=f.Shields[0];Assert.AreEqual(f.Target,hit.Grid);Assert.That(hit.LocalPoint.x,Is.EqualTo(-2).Within(.001f));
            Assert.AreEqual(100,hit.Amount);Assert.AreEqual(2,hit.Penetration);Assert.AreEqual(row.Source,hit.Source);Assert.AreEqual(row.Instigator,hit.Instigator);
            Assert.AreEqual(row.ShotId,hit.Sequence);Assert.AreEqual(ProducerKind.Projectile,hit.Producer);Assert.AreEqual(DamageType.Kinetic,hit.Type);
            Assert.AreEqual(new float3(1,0,0),hit.LocalDir);Assert.AreEqual(new float3(7,0,0),hit.WorldImpulse);
            Assert.AreEqual(0,f.Effects.Count);Assert.AreEqual(0,f.Sounds.Count);Assert.AreEqual(0,f.Store.Rows.Length);
        }
        [Test] public void Explosive_Request()
        {
            using var f=new Fixture();f.Weapon.ExplosionRadius=3;f.Weapon.ExplosionDamage=40;
            var row=f.Shot();f.Store.Rows.Add(row);f.Step();Assert.AreEqual(1,f.Explosions.Count);
            var e=f.Explosions[0];Assert.AreEqual(80,e.Amount);Assert.AreEqual(3,e.Radius);Assert.AreEqual(DamageType.Explosive,e.Type);
            Assert.AreEqual(row.Source,e.Source);Assert.AreEqual(row.Instigator,e.Instigator);Assert.AreEqual(row.ShotId,e.Sequence);
            Assert.That(e.WorldPos.x,Is.EqualTo(0).Within(.001f));
            f.Grids.EM.AddComponentData(f.Target,new ShieldState { Active=1,Hp=100,MaxHp=100,Center=new float3(1,.5f,.5f),Radii=new float3(3) });
            f.Store.Rows.Add(row);f.Step();Assert.AreEqual(1,f.Shields.Count);Assert.AreEqual(1,f.Explosions.Count);
            Assert.That(f.Explosions[0].WorldPos.x,Is.EqualTo(-2).Within(.001f));
        }
        [Test] public void Fuse_Explodes()
        {
            using var f=new Fixture();f.Weapon.ExplosionRadius=2;f.Weapon.ExplosionDamage=30;
            var row=f.Shot();row.Position=new float3(0,20,0);row.Flags=ProjectileFlags.ExplodeOnExpire;row.DeathTick=1;
            f.Store.Rows.Add(row);f.Step();Assert.AreEqual(0,f.Damage.Count);Assert.AreEqual(1,f.Explosions.Count);
            Assert.AreEqual(row.Position+row.Velocity*Game.Core.SimTime.FixedTimestep,f.Explosions[0].WorldPos);
            Assert.AreEqual(1,f.Store.Stats.Value.Expired);Assert.AreEqual(0,f.Store.Stats.Value.Live);
        }
        [Test] public void Homing_TurnRate()
        {
            using var f=new Fixture();var row=f.Shot();row.Position=new float3(0,20,0);row.Velocity=new float3(10,0,0);
            row.Target=f.Target;row.Flags=ProjectileFlags.Homing;f.Store.Rows.Add(row);
            for(int i=0;i<12;i++)
            {
                var before=f.Store.Rows[0];var proxy=f.Hash.Proxies[0];proxy.Center=new float3(5+i,10,10+i);f.Hash.Proxies[0]=proxy;
                float3 wanted=math.normalize(proxy.Center-before.Position);f.Step();var after=f.Store.Rows[0];
                float angle=math.acos(math.clamp(math.dot(math.normalize(before.Velocity),math.normalize(after.Velocity)),-1f,1f));
                Assert.LessOrEqual(angle,f.Weapon.HomingTurnRateRad*SimTime.FixedTimestep+.0001f);
                Assert.That(math.length(after.Velocity),Is.EqualTo(10).Within(.0001f));
                Assert.Greater(math.dot(math.normalize(after.Velocity),wanted),math.dot(math.normalize(before.Velocity),wanted));
            }
            var opposite=ProjectileStepJob.TurnTowards(new float3(10,0,0),new float3(-1,0,0),.1f);
            Assert.That(math.length(opposite),Is.EqualTo(10).Within(.0001f));Assert.That(math.acos(opposite.x/10),Is.EqualTo(.1f).Within(.0001f));
            var straight=f.Store.Rows[0];straight.Target=Entity.Null;f.Store.Rows[0]=straight;f.Step();Assert.AreEqual(straight.Velocity,f.Store.Rows[0].Velocity);
        }
        [Test] public void Compaction_StableOrder()
        {
            using var f=new Fixture();var expected=new List<uint>();
            for(uint i=0;i<137;i++) { var row=f.Shot(i);row.Position=new float3(0,20,0);if(i%3==0)row.Flags=ProjectileFlags.Dead;else expected.Add(i);f.Store.Rows.Add(row); }
            f.Step();Assert.AreEqual(expected.Count,f.Store.Stats.Value.Live);Assert.AreEqual(expected.Count,f.Store.Stats.Value.PeakLive);
            for(int i=0;i<expected.Count;i++)Assert.AreEqual(expected[i],f.Store.Rows[i].ShotId);
        }
        [Test] public void Streams_WorkerCountIndependent()
        {
            int previous=JobsUtility.JobWorkerCount;
            try
            {
                using var f=new Fixture();f.Weapon.ExplosionRadius=2;f.Weapon.ExplosionDamage=10;
                List<DamageEvent> expected=null;List<ExplosionRequest> explosions=null;
                foreach(int count in new[]{1,JobsUtility.JobWorkerMaximumCount})
                {
                    JobsUtility.JobWorkerCount=count;
                    for(uint i=0;i<193;i++)f.Store.Rows.Add(f.Shot(i));f.Step();
                    if(expected==null){expected=f.Damage;explosions=f.Explosions;}
                    else{CollectionAssert.AreEqual(expected,f.Damage);CollectionAssert.AreEqual(explosions,f.Explosions);}
                    Assert.AreEqual(193,f.Damage.Count);Assert.AreEqual(193,f.Explosions.Count);Assert.AreEqual(0,f.Store.Rows.Length);
                }
            }
            finally { JobsUtility.JobWorkerCount=previous; }
        }
    }
}
