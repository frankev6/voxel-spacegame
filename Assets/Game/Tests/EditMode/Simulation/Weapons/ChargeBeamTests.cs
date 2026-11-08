using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Components;
using Game.Data.Combat;
using Game.Data.Components;
using Game.Data.Definitions;
using Game.Data.Streams;
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
using Unity.Transforms;

namespace Game.Tests.EditMode.Simulation.Weapons
{
    /// <summary>WPN-019: the charge beam's handler and step system on real voxel queries and guarded event channels (BeamStepTests' fixture).</summary>
    public sealed class ChargeBeamTests : GameTestBase
    {
        const int ChargeTicks = 6, PulseTicks = 12;

        sealed class Fixture : IDisposable
        {
            public readonly TestGridFactory Grids = new TestGridFactory();
            public readonly Entity Source, Target, Weapon;
            public readonly WeaponDef Definition = new WeaponDef { Index = 1, Delivery = WeaponDelivery.ChargeBeam, Type = DamageType.Energy,
                Damage = 240, PenetrationCells = 2, ImpulsePerHit = 120, BeamRange = 20, BeamPulseTicks = PulseTicks, BeamWidth = 1, HitFx = new ContentHash(77) };
            public ChargeBeamDef Charge = new ChargeBeamDef { ChargeTicks = ChargeTicks, TrackRateRad = math.radians(60f), ConvergeRadius = 4,
                ConvergeBeamWidth = .1f, ConvergedHoldFraction = .2f, ConvergeBeams = 6, FireCue = new ContentHash(555) };
            public readonly SpatialHash Hash = SpatialHash.Create(128);
            public readonly List<DamageEvent> Damage = new List<DamageEvent>();
            public readonly List<ShieldHit> Shields = new List<ShieldHit>();
            public readonly List<SfxEvent> Sounds = new List<SfxEvent>();
            readonly BlobAssetReference<WeaponDefsBlob> definitions;
            readonly ShieldHitStreams shieldChannel;
            readonly DamageStreams streams;
            readonly SystemHandle step, streamOwner;
            readonly Entity clock;
            uint tick;
            public EntityManager Em => Grids.EM;
            public ChargeBeamState State => Em.GetComponentData<ChargeBeamState>(Weapon);
            public Fixture(uint chargeTicks = ChargeTicks)
            {
                Charge.ChargeTicks = chargeTicks;
                Target = Grids.Spawn(Grids.Box(0, new int3(3)));
                Source = Grids.Spawn(Grids.Box(0, 0)); Em.SetComponentData(Source, LocalTransform.FromPosition(new float3(-5, .5f, .5f)));
                using (var builder = new BlobBuilder(Allocator.Temp))
                {
                    ref var data = ref builder.ConstructRoot<WeaponDefsBlob>();
                    var rows = builder.Allocate(ref data.Weapons, 2); rows[1] = Definition;
                    var charges = builder.Allocate(ref data.ChargeBeams, 2); charges[1] = Charge;
                    definitions = builder.CreateBlobAssetReference<WeaponDefsBlob>(Allocator.Persistent);
                }
                Em.CreateSingleton(new WeaponDefsRef { Value = definitions }); Em.CreateSingleton(Hash); clock = Em.CreateSingleton(new SimClock());
                streamOwner = Grids.World.GetOrCreateSystem<DamageStreamsSystem>(); Grids.World.GetOrCreateSystem<FxEventQueueCreateSystem>();
                Grids.World.GetOrCreateSystem<SfxEventQueueCreateSystem>();
                shieldChannel = new ShieldHitStreams { Hits = SimEventChannel<ShieldHit>.Create("charge_test_shields") }; Em.CreateSingleton(shieldChannel);
                using (var query = Em.CreateEntityQuery(typeof(DamageStreams))) streams = query.GetSingleton<DamageStreams>();
                step = Grids.World.GetOrCreateSystem<ChargeBeamStepSystem>();
                Weapon = Em.CreateEntity(typeof(FunctionalBlock), typeof(WeaponMount), typeof(WeaponAim), typeof(ChargeBeamState));
                Em.SetComponentData(Weapon, new FunctionalBlock { Grid = Source, InstanceIndex = 21 });
                Em.SetComponentData(Weapon, new WeaponMount { Initialized = 1, WeaponIndex = 1, ForwardLocal = new float3(1, 0, 0), UpLocal = new float3(0, 1, 0) });
                Aim(new float3(1, 0, 0)); Start(new float3(1, 0, 0));
                var transform = Em.GetComponentData<LocalTransform>(Target);
                Hash.Proxies.Add(new SpatialProxy { Entity = Target, Kind = ProxyKind.Ship, Center = transform.TransformPoint(new float3(1)), Radius = 8, HullRadius = 2 });
                Hash.Large.Add(0);
            }
            public void Aim(float3 direction, byte hasAim = 1) => Em.SetComponentData(Weapon, new WeaponAim { HasAim = hasAim, FireDirLocal = direction });
            /// <summary>What ChargeBeamShotHandlerSystem writes for a shot along <paramref name="direction"/>.</summary>
            public void Start(float3 direction) => Em.SetComponentData(Weapon, new ChargeBeamState { Phase = ChargeBeamPhase.Charging,
                TicksLeft = (ushort)Charge.ChargeTicks, PhaseTicks = (ushort)Charge.ChargeTicks, Weapon = 1, DirWorld = direction, Width = 1 });
            public void Step()
            {
                Em.SetComponentData(clock, new SimClock { Tick = ++tick }); streamOwner.Update(Grids.World.Unmanaged); shieldChannel.Hits.BeginTick(tick);
                step.Update(Grids.World.Unmanaged); Em.CompleteAllTrackedJobs();
                ref var state = ref Grids.World.Unmanaged.ResolveSystemStateRef(step);
                Capture(streams.Damage, Damage, ref state); Capture(shieldChannel.Hits, Shields, ref state);
                using var query = Em.CreateEntityQuery(typeof(FxEventQueue)); var effects = query.GetSingleton<FxEventQueue>(); effects.Access.CompleteAccess();
                while (effects.Queue.TryDequeue(out _)) { }
                using var soundQuery = Em.CreateEntityQuery(typeof(SfxEventQueue)); var sounds = soundQuery.GetSingleton<SfxEventQueue>(); sounds.Access.CompleteAccess();
                Sounds.Clear(); while (sounds.Queue.TryDequeue(out var sound)) Sounds.Add(sound);
            }
            static void Capture<T>(SimEventChannel<T> channel, List<T> output, ref SystemState state) where T : unmanaged
            {
                using var captured = new NativeList<T>(Allocator.TempJob); var set = channel.BeginRead(ref state);
                var handle = SimEventGather.Schedule(set, captured, state.Dependency); channel.EndRead(handle); handle.Complete();
                output.Clear(); foreach (var item in captured) output.Add(item);
            }
            public void Dispose() { Grids.Dispose(); shieldChannel.Hits.Dispose(); Hash.Dispose(); definitions.Dispose(); }
        }

        [Test] public void Sequence_MarksTargetThenBurns()
        {
            using var f = new Fixture();
            for (int i = 0; i < ChargeTicks; i++)
            {
                f.Step();
                Assert.AreEqual(0, f.Damage.Count, "charging never damages"); Assert.AreEqual(0, f.Shields.Count);
                Assert.AreEqual(f.Target, f.State.Target, "the target is marked while charging"); Assert.AreEqual(1, f.State.HitSomething);
                Assert.That(math.distance(new float3(0, .5f, .5f), f.State.EndWorld), Is.LessThan(.01f));
                Assert.AreEqual(i == ChargeTicks - 1 ? 1 : 0, f.Sounds.Count, "the discharge sound plays once, as the main beam fires");
            }
            Assert.AreEqual(f.Charge.FireCue, f.Sounds[0].Cue);
            Assert.AreEqual(ChargeBeamPhase.Firing, f.State.Phase); Assert.AreEqual(PulseTicks, f.State.TicksLeft); Assert.AreEqual(PulseTicks, f.State.PhaseTicks);
            float total = 0; float3 impulse = default;
            for (int i = 0; i < PulseTicks; i++)
            {
                f.Step(); Assert.AreEqual(1, f.Damage.Count); Assert.AreEqual(0, f.Sounds.Count);
                var hit = f.Damage[0]; total += hit.Amount; impulse += hit.WorldImpulse;
                Assert.AreEqual(f.Target, hit.Grid); Assert.AreEqual(f.Source, hit.Source); Assert.AreEqual(ProducerKind.Beam, hit.Producer);
                Assert.AreEqual(21u, hit.Sequence); Assert.AreEqual(2, hit.Penetration); Assert.AreEqual(DamageType.Energy, hit.Type);
            }
            Assert.AreEqual(f.Definition.Damage, total, .001f); Assert.That(math.distance(new float3(f.Definition.ImpulsePerHit, 0, 0), impulse), Is.LessThan(.01f));
            Assert.AreEqual(ChargeBeamPhase.Idle, f.State.Phase); Assert.AreEqual(Entity.Null, f.State.Target); Assert.AreEqual(0, f.State.HitSomething);
            f.Step(); Assert.AreEqual(0, f.Damage.Count, "idle after the burn");
        }

        [Test] public void Tracking_LagsAtTrackRate()
        {
            using var f = new Fixture(400);   // charging throughout: tracking is the same in both phases
            f.Aim(new float3(0, 0, 1));       // 90° away; the track rate is 60°/s = 1° per tick
            f.Step(); Assert.That(math.degrees(Angle(f.State.DirWorld, new float3(1, 0, 0))), Is.EqualTo(1f).Within(.01f));
            for (int i = 1; i < 30; i++) f.Step();
            Assert.That(math.degrees(Angle(f.State.DirWorld, new float3(1, 0, 0))), Is.EqualTo(30f).Within(.05f));
            Assert.AreEqual(Entity.Null, f.State.Target, "the beam has swung off the target");
            for (int i = 30; i < 95; i++) f.Step();
            Assert.That(math.distance(f.State.DirWorld, new float3(0, 0, 1)), Is.LessThan(1e-4f), "reaches the aim without overshoot");
            f.Aim(new float3(1, 0, 0), 0); f.Step();
            Assert.That(math.distance(f.State.DirWorld, new float3(0, 0, 1)), Is.LessThan(1e-4f), "no aim: the beam holds");
            f.Aim(new float3(-1, 0, 0)); var before = f.State.DirWorld; f.Step();
            Assert.That(math.degrees(Angle(before, f.State.DirWorld)), Is.EqualTo(1f).Within(.01f), "a reversal also turns at the track rate");
            // Opposite directions still turn at the rate (any perpendicular axis).
            float3 turned = ChargeBeamStepJob.RotateToward(new float3(1, 0, 0), new float3(-1, 0, 0), math.radians(10f));
            Assert.That(math.degrees(Angle(turned, new float3(1, 0, 0))), Is.EqualTo(10f).Within(.01f));
        }

        [Test] public void Cancels_OnPowerHulkOrWeaponChange()
        {
            using var f = new Fixture();
            f.Step(); Assert.AreEqual(ChargeBeamPhase.Charging, f.State.Phase);
            f.Em.AddComponent<Powered>(f.Weapon); f.Em.SetComponentEnabled<Powered>(f.Weapon, false); f.Step(); AssertIdle(f);
            f.Em.SetComponentEnabled<Powered>(f.Weapon, true); f.Start(new float3(1, 0, 0)); f.Step(); Assert.AreEqual(ChargeBeamPhase.Charging, f.State.Phase);
            f.Em.AddComponent<Hulk>(f.Source); f.Step(); AssertIdle(f); f.Em.RemoveComponent<Hulk>(f.Source);
            f.Start(new float3(1, 0, 0)); var mount = f.Em.GetComponentData<WeaponMount>(f.Weapon); mount.WeaponIndex = 0; f.Em.SetComponentData(f.Weapon, mount);
            f.Step(); AssertIdle(f);
        }

        [Test] public void Handler_StartsOnlyIdleChargeBeams()
        {
            using var f = new Fixture();
            f.Em.SetComponentData(f.Weapon, default(ChargeBeamState));
            var shots = new WeaponShots { Shots = new NativeList<WeaponShot>(4, Allocator.Persistent), Access = GuardedHandles.Create() };
            try
            {
                f.Em.CreateSingleton(shots);
                var handler = f.Grids.World.GetOrCreateSystem<ChargeBeamShotHandlerSystem>();
                var shot = new WeaponShot { Weapon = f.Weapon, Grid = f.Source, Def = 1, Delivery = WeaponDelivery.ChargeBeam,
                    MuzzleWorld = new float3(-5, .5f, .5f), DirWorld = new float3(0, 0, 2) };
                shots.Shots.Add(new WeaponShot { Weapon = f.Weapon, Def = 1, Delivery = WeaponDelivery.Beam });   // another delivery: ignored
                handler.Update(f.Grids.World.Unmanaged); f.Em.CompleteAllTrackedJobs();
                Assert.AreEqual(ChargeBeamPhase.Idle, f.State.Phase);
                shots.Access.CompleteAccess(); shots.Shots.Clear(); shots.Shots.Add(shot);
                handler.Update(f.Grids.World.Unmanaged); f.Em.CompleteAllTrackedJobs();
                var started = f.State;
                Assert.AreEqual(ChargeBeamPhase.Charging, started.Phase); Assert.AreEqual(ChargeTicks, started.TicksLeft); Assert.AreEqual(ChargeTicks, started.PhaseTicks);
                Assert.AreEqual(1, started.Weapon); Assert.AreEqual(new float3(0, 0, 1), started.DirWorld); Assert.AreEqual(f.Definition.BeamWidth, started.Width);
                Assert.AreEqual(f.Definition.HitFx, started.Fx);
                // A shot during a running sequence does not restart it.
                var running = started; running.TicksLeft = 2; f.Em.SetComponentData(f.Weapon, running);
                shots.Access.CompleteAccess(); shot.DirWorld = new float3(1, 0, 0); shots.Shots.Clear(); shots.Shots.Add(shot);
                handler.Update(f.Grids.World.Unmanaged); f.Em.CompleteAllTrackedJobs();
                Assert.AreEqual(2, f.State.TicksLeft); Assert.AreEqual(new float3(0, 0, 1), f.State.DirWorld);
            }
            finally { shots.Access.CompleteAccess(); shots.Access.Dispose(); shots.Shots.Dispose(); }
        }

        static float Angle(float3 a, float3 b) => math.acos(math.clamp(math.dot(math.normalize(a), math.normalize(b)), -1f, 1f));
        static void AssertIdle(Fixture f)
        { Assert.AreEqual(ChargeBeamPhase.Idle, f.State.Phase); Assert.AreEqual(0, f.State.TicksLeft); Assert.AreEqual(0, f.State.HitSomething); Assert.AreEqual(0, f.Damage.Count); }
    }
}
