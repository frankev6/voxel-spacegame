using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data.Combat;
using Game.Data.Streams;
using Game.Simulation.Commands;
using Game.Simulation.Power;
using Game.Simulation.Spatial;
using Game.Simulation.Weapons;
using Game.Tests.EditMode.Voxel.Damage;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Game.Voxel.PresentationStreams;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.Tests.EditMode.Simulation.Weapons
{
    /// <summary>WPN-010: four required checks execute the production system and guarded event channels.</summary>
    public sealed class ExplosionTests
    {
        sealed class Fixture : IDisposable
        {
            public readonly TestGridFactory Grids = new TestGridFactory();
            public readonly SpatialHash Hash = SpatialHash.Create(128);
            public readonly DamageStreams Streams = new DamageStreams
            { Damage = SimEventChannel<DamageEvent>.Create("blast_damage"), Explosions = SimEventChannel<ExplosionRequest>.Create("blast_requests") };
            public readonly ShieldHitStreams Shields = new ShieldHitStreams { Hits = SimEventChannel<ShieldHit>.Create("blast_shields") };
            public readonly FxEventQueue Fx = new FxEventQueue { Queue = new NativeQueue<FxEvent>(Allocator.Persistent), Access = GuardedHandles.Create() };
            public readonly SfxEventQueue Sfx = new SfxEventQueue { Queue = new NativeQueue<SfxEvent>(Allocator.Persistent), Access = GuardedHandles.Create() };
            readonly SystemHandle system, probe;
            public List<DamageEvent> Damage;
            public List<ShieldHit> Hits;
            public EntityManager Em => Grids.EM;
            public Fixture()
            {
                Em.CreateSingleton(Streams); Em.CreateSingleton(Shields); Em.CreateSingleton(Hash); Em.CreateSingleton(Fx); Em.CreateSingleton(Sfx);
                Em.CreateSingleton(new WeaponTuning { ExplosionImpulsePerDamage = 2 });
                Em.CreateSingleton(new CombatAudioCues { ExplosionSmall = new ContentHash(1), ExplosionLarge = new ContentHash(2) });
                probe = Grids.World.GetOrCreateSystem<HitTestProbeSystem>();
                system = Grids.World.GetOrCreateSystem<ExplosionSystem>();
            }
            public Entity Grid(float3 center, bool shield = false)
            {
                Entity root = Grids.Spawn(Grids.Box(0, new int3(1)));
                Em.SetComponentData(root, LocalTransform.FromPosition(center - new float3(.5f)));
                if (shield) Em.AddComponentData(root, new ShieldState { Active = 1, Hp = 100, MaxHp = 100, Center = new float3(.5f), Radii = new float3(2) });
                Hash.Proxies.Add(new SpatialProxy { Entity = root, Center = center, Radius = shield ? 2 : 1, HullRadius = 1, Kind = ProxyKind.Ship });
                Hash.Large.Add(Hash.Proxies.Length - 1); return root;
            }
            public ExplosionRequest Request(float3 pos, float radius = 4, uint sequence = 17) => new ExplosionRequest
            { WorldPos = pos, Radius = radius, Amount = 100, Type = DamageType.Explosive, Source = new Entity { Index = 90000, Version = 1 },
                Instigator = new Entity { Index = 90001, Version = 1 }, Sequence = sequence, Producer = ProducerKind.Projectile };
            public void Publish(ExplosionRequest request)
            {
                ref var state = ref Grids.World.Unmanaged.ResolveSystemStateRef(probe);
                var writer = Streams.Explosions.BeginWrite(ref state, request.Producer, 1);
                var handle = new PublishJob { Writer = writer, Request = request }.Schedule(state.Dependency);
                Streams.Explosions.EndWrite(handle); state.Dependency = handle;
            }
            public void Step()
            {
                system.Update(Grids.World.Unmanaged);
                Damage = Read(Streams.Damage); Hits = Read(Shields.Hits);
                Fx.Access.CompleteAccess(); Sfx.Access.CompleteAccess();
            }
            List<T> Read<T>(SimEventChannel<T> channel) where T : unmanaged
            {
                ref var state = ref Grids.World.Unmanaged.ResolveSystemStateRef(probe);
                using var output = new NativeList<T>(Allocator.TempJob);
                var set = channel.BeginRead(ref state); var handle = SimEventGather.Schedule(set, output, state.Dependency);
                channel.EndRead(handle); state.Dependency = handle; handle.Complete();
                var result = new List<T>(); foreach (var value in output) result.Add(value); return result;
            }
            public void Dispose()
            {
                Em.CompleteAllTrackedJobs(); Streams.Damage.Dispose(); Streams.Explosions.Dispose(); Shields.Hits.Dispose();
                Hash.Dispose(); Fx.Access.Dispose(); Fx.Queue.Dispose(); Sfx.Access.Dispose(); Sfx.Queue.Dispose(); Grids.Dispose();
            }
        }
        struct PublishJob : IJob
        {
            public NativeStream.Writer Writer; public ExplosionRequest Request;
            public void Execute() { Writer.BeginForEachIndex(0); Writer.Write(Request); Writer.EndForEachIndex(); }
        }
        [Test] public void ShieldFirst()
        {
            using var f = new Fixture(); Entity root = f.Grid(new float3(1,0,0), true);
            var request = f.Request(default, 3); f.Publish(request); f.Step();
            Assert.AreEqual(0, f.Damage.Count); Assert.AreEqual(1, f.Hits.Count);
            var hit = f.Hits[0]; Assert.AreEqual(root, hit.Grid); Assert.AreEqual(100, hit.Amount);
            Assert.AreEqual(3, hit.Radius); Assert.AreEqual(ShieldHitFlags.Blast, hit.Flags);
            Assert.AreEqual(request.Source, hit.Source); Assert.AreEqual(request.Instigator, hit.Instigator);
            Assert.AreEqual(request.Sequence, hit.Sequence); Assert.AreEqual(ProducerKind.Explosion, hit.Producer);
            Assert.AreEqual(DamageType.Explosive, hit.Type); Assert.AreEqual(float3.zero, hit.LocalDir); Assert.AreEqual(0, hit.Penetration);
            Assert.AreEqual(new float3(-.5f,.5f,.5f), hit.LocalPoint);
            Assert.AreEqual(1, f.Fx.Queue.Count); Assert.AreEqual(1, f.Sfx.Queue.Count);
            Assert.AreEqual(new ContentHash(1), f.Sfx.Queue.Dequeue().Cue);
        }
        [Test] public void TwoGrids_TwoEvents()
        {
            using var f = new Fixture(); Entity shield = f.Grid(new float3(-1,0,0), true), hull = f.Grid(new float3(1,0,0));
            var request = f.Request(default); f.Publish(request); f.Step();
            Assert.AreEqual(1, f.Hits.Count); Assert.AreEqual(shield, f.Hits[0].Grid);
            Assert.AreEqual(1, f.Damage.Count); var hit = f.Damage[0]; Assert.AreEqual(hull, hit.Grid);
            Assert.AreEqual(request.Amount, hit.Amount); Assert.AreEqual(request.Radius, hit.Radius);
            Assert.AreEqual(request.Source, hit.Source); Assert.AreEqual(request.Instigator, hit.Instigator);
            Assert.AreEqual(request.Sequence, hit.Sequence); Assert.AreEqual(ProducerKind.Explosion, hit.Producer);
            Assert.AreEqual(1, f.Fx.Queue.Count); Assert.AreEqual(1, f.Sfx.Queue.Count);
            Assert.AreEqual(new ContentHash(2), f.Sfx.Queue.Dequeue().Cue);
        }
        [Test] public void SameTickProjectileRequests()
        {
            using var f = new Fixture(); f.Grid(default);
            f.Publish(f.Request(default, 4, 1)); f.Step();
            Assert.AreEqual(1, f.Damage.Count); Assert.AreEqual(1, f.Damage[0].Sequence);
            // DamageResolve publishes reactor requests after ExplosionSystem's read snapshot, under ProducerKind.Explosion.
            var reactor = f.Request(default, 4, 2); reactor.Producer = ProducerKind.Explosion; f.Publish(reactor);
            Assert.AreEqual(1, f.Damage[0].Sequence, "A later producer cannot alter the already-consumed tick.");
            f.Step(); Assert.AreEqual(1, f.Damage.Count); Assert.AreEqual(2, f.Damage[0].Sequence);
            f.Step(); Assert.AreEqual(0, f.Damage.Count); Assert.AreEqual(0, f.Hits.Count);
        }
        [Test] public void Impulse_Falloff()
        {
            using var f = new Fixture(); Entity center = f.Grid(default), halfway = f.Grid(new float3(2,0,0)), edge = f.Grid(new float3(4,0,0));
            f.Publish(f.Request(default)); f.Step(); Assert.AreEqual(3, f.Damage.Count);
            foreach (var hit in f.Damage)
            {
                float3 expected = hit.Grid == halfway ? new float3(50,0,0) : float3.zero;
                Assert.That(math.distance(expected, hit.WorldImpulse), Is.LessThan(.0001f));
                Assert.IsTrue(hit.Grid == center || hit.Grid == halfway || hit.Grid == edge);
            }
        }
    }
}
