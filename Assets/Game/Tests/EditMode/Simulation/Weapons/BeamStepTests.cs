using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Components;
using Game.Data.Combat;
using Game.Data.Components;
using Game.Data.Streams;
using Game.Simulation.Commands;
using Game.Simulation.Power;
using Game.Simulation.Spatial;
using Game.Simulation.Weapons;
using Game.Tests.Common;
using Game.Tests.EditMode.Voxel.Damage;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Game.Voxel.PresentationStreams;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Tests.EditMode.Simulation.Weapons
{
    /// <summary>WPN-009's three required checks exercise the production system, real voxel queries and guarded event channels.</summary>
    public sealed class BeamStepTests : GameTestBase
    {
        sealed class Fixture : IDisposable
        {
            public readonly TestGridFactory Grids = new TestGridFactory();
            public readonly Entity Source, Target, Weapon, Owner;
            public readonly WeaponDef Definition = new WeaponDef { Index = 1, Delivery = Game.Data.Definitions.WeaponDelivery.Beam,
                Type = DamageType.Energy, Damage = 180, PenetrationCells = 3, ImpulsePerHit = 300,
                BeamRange = 20, BeamPulseTicks = 12, BeamWidth = .2f, HitFx = new ContentHash(123) };
            public readonly SpatialHash Hash = SpatialHash.Create(128);
            public readonly List<DamageEvent> Damage = new List<DamageEvent>();
            public readonly List<ShieldHit> Shields = new List<ShieldHit>();
            public readonly List<FxEvent> Effects = new List<FxEvent>();
            readonly List<SpatialProxy> proxies = new List<SpatialProxy>();
            readonly BlobAssetReference<WeaponDefsBlob> definitions;
            readonly ShieldHitStreams shieldChannel;
            readonly DamageStreams streams;
            readonly SystemHandle step, streamOwner;
            readonly Entity clock;
            uint tick;
            public EntityManager Em => Grids.EM;
            public BeamState State => Em.GetComponentData<BeamState>(Weapon);
            public Fixture()
            {
                Target = Grids.Spawn(Grids.Box(0, new int3(3)));
                Source = Grids.Spawn(Grids.Box(0, 0)); Em.SetComponentData(Source, LocalTransform.FromPosition(new float3(-5, .5f, .5f)));
                Owner = Em.CreateEntity(); Em.AddComponentData(Source, new OwnedBy { Owner = Owner }); Em.AddComponent<PlayerShip>(Source);
                Em.AddComponentData(Target, new PhysicsVelocity { Linear = new float3(1, 2, 3) });
                using (var builder = new BlobBuilder(Allocator.Temp))
                {
                    ref var data = ref builder.ConstructRoot<WeaponDefsBlob>(); var rows = builder.Allocate(ref data.Weapons, 2); rows[1] = Definition;
                    definitions = builder.CreateBlobAssetReference<WeaponDefsBlob>(Allocator.Persistent);
                }
                Em.CreateSingleton(new WeaponDefsRef { Value = definitions }); Em.CreateSingleton(Hash); clock = Em.CreateSingleton(new SimClock());
                streamOwner = Grids.World.GetOrCreateSystem<DamageStreamsSystem>(); Grids.World.GetOrCreateSystem<FxEventQueueCreateSystem>();
                shieldChannel = new ShieldHitStreams { Hits = SimEventChannel<ShieldHit>.Create("beam_test_shields") }; Em.CreateSingleton(shieldChannel);
                using (var query = Em.CreateEntityQuery(typeof(DamageStreams))) streams = query.GetSingleton<DamageStreams>();
                step = Grids.World.GetOrCreateSystem<BeamStepSystem>();
                Weapon = Mount(); var inactive = Mount(); Em.SetComponentData(inactive, default(BeamState)); Em.AddComponent<SecondChunk>(inactive);
                Reset(); AddGrid(Target); AddGrid(Source); PublishHash();
            }
            Entity Mount()
            {
                var entity = Em.CreateEntity(typeof(FunctionalBlock), typeof(WeaponMount), typeof(WeaponAim), typeof(BeamState));
                Em.SetComponentData(entity, new FunctionalBlock { Grid = Source, InstanceIndex = 37 });
                Em.SetComponentData(entity, new WeaponMount { Initialized = 1, WeaponIndex = 1, ForwardLocal = new float3(1, 0, 0), UpLocal = new float3(0, 1, 0) });
                Em.SetComponentData(entity, new WeaponAim { HasAim = 1, FireDirLocal = new float3(1, 0, 0) }); return entity;
            }
            public void Reset() => Em.SetComponentData(Weapon, new BeamState { Active = 1, TicksLeft = 12, Width = Definition.BeamWidth });
            public void AddGrid(Entity entity)
            {
                var transform = Em.GetComponentData<LocalTransform>(entity);
                proxies.Add(new SpatialProxy { Entity = entity, Kind = ProxyKind.Ship, Center = transform.TransformPoint(new float3(1)), Radius = 8, HullRadius = 2 });
            }
            public void AddBody(Entity entity, float3 center, float radius)
                => proxies.Add(new SpatialProxy { Entity = entity, Kind = ProxyKind.Celestial, Center = center, Radius = radius, HullRadius = radius });
            public void PublishHash()
            {
                Hash.CompleteAccess(); Hash.Proxies.Clear(); Hash.Large.Clear();
                proxies.Sort((a, b) => a.Entity.Index.CompareTo(b.Entity.Index));
                for (int i = 0; i < proxies.Count; i++)
                {
                    var proxy = proxies[i];
                    if (proxy.Kind != ProxyKind.Celestial) proxy.Center = Em.GetComponentData<LocalTransform>(proxy.Entity).TransformPoint(new float3(1));
                    Hash.Proxies.Add(proxy); Hash.Large.Add(i); // Canonical large-proxy query path, as in ProjectileStepTests.
                }
            }
            public void Step()
            {
                Em.SetComponentData(clock, new SimClock { Tick = ++tick }); streamOwner.Update(Grids.World.Unmanaged); shieldChannel.Hits.BeginTick(tick);
                step.Update(Grids.World.Unmanaged); Em.CompleteAllTrackedJobs();
                ref var state = ref Grids.World.Unmanaged.ResolveSystemStateRef(step);
                Capture(streams.Damage, Damage, ref state); Capture(shieldChannel.Hits, Shields, ref state);
                using var query = Em.CreateEntityQuery(typeof(FxEventQueue)); var effects = query.GetSingleton<FxEventQueue>(); effects.Access.CompleteAccess();
                Effects.Clear(); while (effects.Queue.TryDequeue(out var effect)) Effects.Add(effect);
            }
            static void Capture<T>(SimEventChannel<T> channel, List<T> output, ref SystemState state) where T : unmanaged
            {
                using var captured = new NativeList<T>(Allocator.TempJob); var set = channel.BeginRead(ref state);
                var handle = SimEventGather.Schedule(set, captured, state.Dependency); channel.EndRead(handle); handle.Complete();
                output.Clear(); foreach (var item in captured) output.Add(item);
            }
            public void Dispose() { Grids.Dispose(); shieldChannel.Hits.Dispose(); Hash.Dispose(); definitions.Dispose(); }
        }

        [Test] public void PulseDamage_Sums()
        {
            using var f = new Fixture();
            foreach (bool shielded in new[] { false, true })
            {
                if (shielded) f.Em.AddComponentData(f.Target, new ShieldState { Active = 1, Hp = 1000, MaxHp = 1000,
                    Center = new float3(1, .5f, .5f), Radii = new float3(3) });
                f.Reset(); float total = 0; float3 impulse = default; int events = 0, effects = 0;
                for (int i = 0; i < 12; i++)
                {
                    f.Step(); Assert.AreEqual(shielded ? 0 : 1, f.Damage.Count); Assert.AreEqual(shielded ? 1 : 0, f.Shields.Count);
                    if (shielded)
                    {
                        var hit = f.Shields[0]; total += hit.Amount; impulse += hit.WorldImpulse; events++;
                        Assert.AreEqual(f.Target, hit.Grid); Assert.AreEqual(f.Source, hit.Source); Assert.AreEqual(f.Owner, hit.Instigator);
                        Assert.AreEqual(ProducerKind.Beam, hit.Producer); Assert.AreEqual(37u, hit.Sequence); Assert.AreEqual(DamageType.Energy, hit.Type);
                        Assert.AreEqual(3, hit.Penetration); Assert.AreEqual(0, hit.Radius); Assert.AreEqual(ShieldHitFlags.None, hit.Flags);
                        Assert.That(hit.LocalPoint.x, Is.EqualTo(-2).Within(.001f)); Assert.AreEqual(new float3(1, 0, 0), hit.LocalDir);
                    }
                    else
                    {
                        var hit = f.Damage[0]; total += hit.Amount; impulse += hit.WorldImpulse; events++;
                        Assert.AreEqual(f.Target, hit.Grid); Assert.AreEqual(f.Source, hit.Source); Assert.AreEqual(f.Owner, hit.Instigator);
                        Assert.AreEqual(ProducerKind.Beam, hit.Producer); Assert.AreEqual(37u, hit.Sequence); Assert.AreEqual(DamageType.Energy, hit.Type);
                        Assert.AreEqual(3, hit.Penetration); Assert.AreEqual(0, hit.Radius); Assert.AreEqual(DamageEventFlags.None, hit.Flags);
                    }
                    foreach (var effect in f.Effects)
                    {
                        effects++; Assert.AreEqual(Game.Data.Definitions.FxKind.HitEnergy, effect.Kind); Assert.AreEqual(f.Definition.HitFx, effect.Fx);
                        Assert.AreEqual(new float3(1, 2, 3), effect.Vel); Assert.AreEqual(FxEventFlags.PlayerSource, effect.Flags);
                        Assert.That(math.distance(new float3(0, .5f, .5f), effect.Pos), Is.LessThan(.001f));
                        Assert.AreEqual(new float3(-1, 0, 0), effect.Dir);
                    }
                    Assert.AreEqual(11 - i, f.State.TicksLeft); Assert.AreEqual(i < 11 ? 1 : 0, f.State.Active);
                }
                Assert.AreEqual(12, events); Assert.AreEqual(f.Definition.Damage, total, .001f);
                Assert.AreEqual(new float3(f.Definition.ImpulsePerHit, 0, 0), impulse); Assert.AreEqual(shielded ? 0 : 2, effects);
                f.Step(); Assert.AreEqual(0, f.Damage.Count); Assert.AreEqual(0, f.Shields.Count);
            }
            f.Em.RemoveComponent<ShieldState>(f.Target); f.Em.RemoveComponent<OwnedBy>(f.Source);
            var multipliers = new ShipStatMultipliers(); for (int i = 0; i <= (int)StatId.WeaponDamage; i++) multipliers.Values.Add(i == (int)StatId.WeaponDamage ? 2 : 1);
            f.Em.AddComponentData(f.Source, multipliers); f.Reset(); f.Step();
            Assert.AreEqual(30, f.Damage[0].Amount); Assert.AreEqual(f.Source, f.Damage[0].Instigator);
        }
        [Test] public void EndWorld_AtHit()
        {
            using var f = new Fixture(); quaternion rotation = quaternion.RotateY(math.PI / 2);
            var targetPose = LocalTransform.FromPositionRotationScale(new float3(10, 2, -3), rotation, 1); f.Em.SetComponentData(f.Target, targetPose);
            float3 point = targetPose.TransformPoint(new float3(0, .5f, .5f)), direction = math.rotate(rotation, new float3(1, 0, 0));
            var mount = f.Em.GetComponentData<WeaponMount>(f.Weapon); mount.PivotLocal = new float3(.5f, 0, 0); mount.MuzzleLocal = new float3(1, 0, 0);
            f.Em.SetComponentData(f.Weapon, mount); var aim = f.Em.GetComponentData<WeaponAim>(f.Weapon); aim.Yaw = math.PI / 2; f.Em.SetComponentData(f.Weapon, aim);
            float3 muzzleLocal = new float3(.5f, 0, -.5f), muzzle = point - direction * 5;
            var sourcePose = LocalTransform.FromPositionRotationScale(muzzle - math.rotate(rotation, muzzleLocal), rotation, 1); f.Em.SetComponentData(f.Source, sourcePose);
            var behind = f.Grids.Spawn(f.Grids.Box(0, new int3(3))); f.Em.SetComponentData(behind, LocalTransform.FromPositionRotationScale(targetPose.Position + direction * 5, rotation, 1));
            f.AddGrid(behind); f.PublishHash(); f.Step();
            Assert.AreEqual(1, f.State.HitSomething); Assert.That(math.distance(point, f.State.EndWorld), Is.LessThan(.001f));
            Assert.That(math.distance(muzzleLocal, f.State.MuzzleLocal), Is.LessThan(.001f)); Assert.AreEqual(f.Target, f.Damage[0].Grid);
            Assert.That(math.distance(new float3(0, .5f, .5f), f.Damage[0].LocalPoint), Is.LessThan(.001f));
            Assert.That(math.distance(new float3(1, 0, 0), f.Damage[0].LocalDir), Is.LessThan(.001f));
            aim.FireDirLocal = new float3(-1, 0, 0); f.Em.SetComponentData(f.Weapon, aim); f.Reset(); f.Step();
            Assert.AreEqual(0, f.State.HitSomething); Assert.That(math.distance(muzzle - direction * 20, f.State.EndWorld), Is.LessThan(.001f));
            Assert.AreEqual(0, f.Damage.Count); Assert.AreEqual(0, f.Shields.Count); Assert.AreEqual(0, f.Effects.Count);
            var body = f.Em.CreateEntity(); f.AddBody(body, muzzle - direction * 8, 2); f.PublishHash(); f.Reset(); f.Step();
            Assert.AreEqual(1, f.State.HitSomething); Assert.That(math.distance(muzzle - direction * 6, f.State.EndWorld), Is.LessThan(.001f));
            Assert.AreEqual(0, f.Damage.Count); Assert.AreEqual(0, f.Shields.Count); Assert.AreEqual(1, f.Effects.Count);
        }
        [Test] public void PowerLoss_StopsPulse()
        {
            using var f = new Fixture(); f.Step(); Assert.AreEqual(1, f.Damage.Count);
            f.Em.AddComponent<Powered>(f.Weapon); f.Em.SetComponentEnabled<Powered>(f.Weapon, false); f.Step();
            AssertStopped(f); f.Em.SetComponentEnabled<Powered>(f.Weapon, true); f.Step(); AssertStopped(f);
            f.Reset(); f.Step(); Assert.AreEqual(1, f.Damage.Count);
            f.Em.AddComponent<Hulk>(f.Source); f.Step(); AssertStopped(f); f.Em.RemoveComponent<Hulk>(f.Source);
            f.Reset(); f.Em.SetComponentData(f.Weapon, new FunctionalBlock { Grid = Entity.Null, InstanceIndex = 37 }); f.Step(); AssertStopped(f);
        }
        static void AssertStopped(Fixture f)
        { Assert.AreEqual(0, f.State.Active); Assert.AreEqual(0, f.State.TicksLeft); Assert.AreEqual(0, f.State.HitSomething); Assert.AreEqual(0, f.Damage.Count); Assert.AreEqual(0, f.Shields.Count); Assert.AreEqual(0, f.Effects.Count); }
    }
}
