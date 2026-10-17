using System;
using System.Collections.Generic;
using Game.App.Systems;
using Game.Core;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Combat;
using Game.Data.Definitions;
using Game.Simulation.Abilities;
using Game.Simulation.Commands;
using Game.Simulation.Flight;
using Game.Simulation.Power;
using Game.Tests.Common;
using Game.Tests.PlayMode.Simulation.Weapons;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Tests.PlayMode.Simulation.Abilities
{
    /// <summary>ABL-005: authored blocks/abilities through real flight, power, effects, status, physics and destruction.</summary>
    [Category("Scenario")]
    public partial class AbilityScenarioTests : GameTestBase
    {
        sealed class Fixture : IDisposable
        {
            readonly CombatScenarioTests.Fixture combat = new CombatScenarioTests.Fixture();
            readonly BlobAssetReference<AbilityDefsBlob> abilities;
            PlayerEdgeCounters edges;
            public Entity Root { get; }
            public EntityManager Em => combat.Em;
            public EcsTestWorld World => combat.World;
            public DynamicBuffer<AbilitySlot> Slots => Em.GetBuffer<AbilitySlot>(Root);
            public Fixture()
            {
                AbilityContent.CreateSingletons(Em, combat.Flight.Catalog, new List<Entity>());
                using (var query = Em.CreateEntityQuery(typeof(AbilityDefsRef))) abilities = query.GetSingleton<AbilityDefsRef>().Value;
                var pre = World.World.GetExistingSystemManaged<GameplayPrePhysicsGroup>();
                pre.AddSystemToUpdateList(World.World.GetOrCreateSystem<StatusSystem>());
                pre.AddSystemToUpdateList(World.World.GetOrCreateSystem<PropulsionStatsSystem>());
                World.World.GetExistingSystemManaged<PowerDemandGroup>().AddSystemToUpdateList(World.World.GetOrCreateSystem<ThrusterPowerDemandSystem>());
                var activation = World.World.GetExistingSystemManaged<AbilityActivationGroup>();
                activation.AddSystemToUpdateList(World.World.GetOrCreateSystem<AbilityTimerSystem>());
                activation.AddSystemToUpdateList(World.World.GetOrCreateSystem<AbilitySystem>());
                activation.AddSystemToUpdateList(World.World.GetOrCreateSystem<AbilityFeedbackSystem>());
                activation.AddSystemToUpdateList(World.World.GetOrCreateSystem<ShieldToggleEffectSystem>());
                activation.AddSystemToUpdateList(World.World.GetOrCreateSystem<StatBoostEffectSystem>());
                activation.SortSystems(); World.World.GetExistingSystemManaged<PowerDemandGroup>().SortSystems(); pre.SortSystems();
                var source = new VoxelStructure(.5f);
                for (int x = -3; x <= 3; x++) { Place(source, "block_hull", new int3(x, 0, 0)); Place(source, "block_hull", new int3(x, 0, 2)); }
                Place(source, "block_hull", new int3(0, 0, 1)); Place(source, "block_hull", new int3(0, 0, 3));
                Place(source, "block_core_small", new int3(0, 1, 1)); Place(source, "block_reactor_small", new int3(-1, 1, 3));
                Place(source, "block_capacitor_small", new int3(0, 1, 0)); Place(source, "block_shield_small", new int3(2, 1, 2));
                byte forward = combat.Flight.Content.Orientations.Value.AxisTurn(1, 2);
                foreach (int x in new[] { -2, 2 }) Place(source, "block_thruster_small", new int3(x, 1, 0), forward);
                foreach (int x in new[] { -1, 1 }) Place(source, "block_thruster_small", new int3(x, 1, 2), forward);
                foreach (int x in new[] { -3, 3 }) Place(source, "block_thruster_small", new int3(x, 1, 2));
                Root = combat.Flight.SpawnSource(source, profile: GridProfile.PlayerShip); combat.Ready(Root);
                Assert.AreEqual(2, Slots.Length); Assert.AreEqual(6, Em.GetComponentData<PropulsionStats>(Root).AliveThrusterCount);
                Assert.AreEqual(1, Em.GetComponentData<ShieldState>(Root).Active); Assert.Greater(Em.GetComponentData<ShipPower>(Root).Stored, 15);
            }
            void Place(VoxelStructure source, string id, int3 origin, byte orientation = 0)
                => Assert.AreEqual(PlaceResult.Ok, source.TryPlaceBlock(new BlockPlacement
                { Block = combat.Flight.Index(id), Origin = origin, Orient = orientation }, combat.Flight.Content), id);
            public int Slot(AbilityKind kind)
            {
                for (int i = 0; i < Slots.Length; i++) if (abilities.Value.Abilities[Slots[i].AbilityIndex].Kind == kind) return i;
                return -1;
            }
            public AbilitySlot Ability(AbilityKind kind) { int index = Slot(kind); Assert.GreaterOrEqual(index, 0); return Slots[index]; }
            public void Input(bool forward = false, AbilityKind? press = null)
            {
                if (press.HasValue)
                {
                    int slot = Slot(press.Value); Assert.That(slot, Is.InRange(0, 3));
                    Increment(ref edges, slot);
                }
                Em.SetComponentData(combat.Flight.InputEntity, new PlayerCommand { Thrust = forward ? new float2(0, 1) : default, Edges = edges });
            }
            public void Tick(int count = 1) => combat.Tick(count);
            public float Cap => Em.GetComponentData<FlightState>(Root).ForwardCap;
            public float ForwardSpeed => math.dot(Em.GetComponentData<PhysicsVelocity>(Root).Linear,
                math.rotate(Em.GetComponentData<LocalTransform>(Root).Rotation, new float3(0, 0, 1)));
            public void DestroyThrusters()
            {
                var streams = World.GetSingleton<DamageStreams>(); uint sequence = 0; int count = 0;
                var content = combat.Flight.Content;
                ref var orientations = ref content.Orientations.Value;
                foreach (var instance in Em.GetBuffer<BlockInstance>(Root))
                {
                    if (instance.Block != combat.Flight.Thruster || (instance.Flags & BlockInstanceFlags.Destroyed) != 0) continue;
                    count++; streams.Damage.PostFromMainThread(new DamageEvent
                    {
                        Grid = Root, LocalPoint = ((float3)instance.Origin + .5f) * .5f,
                        LocalDir = math.rotate(orientations.Rotations[instance.Orient], new float3(0, 0, 1)),
                        Amount = 100000, Penetration = 2, Type = DamageType.Energy, Producer = ProducerKind.Test, Sequence = sequence++,
                        Flags = DamageEventFlags.NoImpulse | DamageEventFlags.NoKick
                    });
                }
                Assert.AreEqual(6, count, "Every authored granting thruster must receive real resolver damage.");
            }
            public Entity InputEntity => combat.Flight.InputEntity;
            public ushort ThrusterBlock => combat.Flight.Thruster;
            public void Dispose() { combat.Dispose(); abilities.Dispose(); }
        }

        [Test] public void Boost_RaisesTopSpeed()
        {
            using var f = new Fixture(); f.Input(forward: true); f.Tick(240);
            float baseline = f.Cap; Assert.AreEqual(120, baseline, .001f); Assert.That(f.ForwardSpeed, Is.EqualTo(baseline).Within(baseline * .02f));
            var originalFlight = f.Em.GetComponentData<FlightState>(f.Root);
            float originalAcceleration = f.Em.GetComponentData<PropulsionStats>(f.Root).ForwardAccel;
            f.Input(forward: true, press: AbilityKind.StatBoost); f.Tick();
            Assert.AreEqual(AbilitySlotState.PendingPower, f.Ability(AbilityKind.StatBoost).State); Assert.AreEqual(baseline, f.Cap, .001f);
            f.Tick(); var active = f.Ability(AbilityKind.StatBoost); Assert.AreEqual(1, active.Active);
            uint began = f.World.GetSingleton<SimClock>().Tick - 1; Assert.AreEqual(began + 60, active.ActiveUntilTick);
            float peak = f.ForwardSpeed; int boostedTicks = 0;
            while (f.World.GetSingleton<SimClock>().Tick <= active.ActiveUntilTick)
            {
                f.Tick(); uint processed = f.World.GetSingleton<SimClock>().Tick - 1;
                if (processed < active.ActiveUntilTick)
                {
                    var flight = f.Em.GetComponentData<FlightState>(f.Root);
                    Assert.AreEqual(baseline * 1.8f, flight.ForwardCap, .001f);
                    Assert.AreEqual(originalFlight.ReverseCap * 1.8f, flight.ReverseCap, .001f);
                    Assert.AreEqual(originalFlight.StrafeCap * 1.8f, flight.StrafeCap, .001f);
                    Assert.AreEqual(originalAcceleration * 2, f.Em.GetComponentData<PropulsionStats>(f.Root).ForwardAccel, .001f);
                    boostedTicks++; peak = math.max(peak, f.ForwardSpeed);
                }
                else { Assert.AreEqual(baseline, f.Cap, .001f); Assert.AreEqual(0, f.Ability(AbilityKind.StatBoost).Active); }
            }
            Assert.AreEqual(59, boostedTicks, "The 60-tick ability posts modifiers for next tick with its original expiry deadline.");
            // Since QA-001's flight handling the one-second pulse no longer reaches the boosted cap (199 of 216 m/s, before any co-op
            // code); it must still pass the normal cap clearly and never the boosted one.
            Assert.Greater(peak, baseline * 1.5f, "Real impulses must pass the normal top speed during this pulse.");
            Assert.LessOrEqual(peak, baseline * 1.8f * 1.02f, "The boosted cap still limits the pulse.");
            // Caps are targets, not velocity assignments. Installed reverse force bounds the physical settling time;
            // allow half of its axial capacity for the existing coupled force/yaw allocation, retaining the same speed tolerance.
            var propulsion = f.Em.GetComponentData<PropulsionStats>(f.Root);
            float mass = 1 / f.Em.GetComponentData<PhysicsMass>(f.Root).InverseMass;
            float expiredSpeed = f.ForwardSpeed, tolerance = baseline * .02f;
            Assert.Greater(propulsion.ReverseThrust, 0);
            Assert.AreEqual(propulsion.ReverseThrust / mass, propulsion.ReverseAccel, .001f);
            int limit = (int)math.ceil(math.max(0, expiredSpeed - baseline - tolerance) /
                (propulsion.ReverseAccel * .5f * SimTime.FixedTimestep)) + 1;
            int settledTicks = 0; float brakingImpulse = 0;
            while (f.ForwardSpeed > baseline + tolerance && settledTicks < limit)
            {
                f.Tick(); settledTicks++;
                float axialForce = 0;
                foreach (var thruster in f.Em.GetBuffer<FlightThruster>(f.Root))
                    axialForce += thruster.MaxForce * thruster.Throttle * thruster.ForceDirLocal.z;
                brakingImpulse -= axialForce * SimTime.FixedTimestep;
                Assert.AreEqual(baseline, f.Cap, .001f);
            }
            TestContext.WriteLine($"ABL-005 settling: mass={mass:G9} kg, reverse={propulsion.ReverseThrust:G9} N, " +
                $"capacity acceleration={propulsion.ReverseAccel:G9} m/s², expiry speed={expiredSpeed:G9}, " +
                $"ticks={settledTicks}/{limit}, allocated braking impulse={brakingImpulse:G9} N·s, final speed={f.ForwardSpeed:G9}");
            Assert.Greater(brakingImpulse, 0, "Production thruster impulses must physically remove the excess speed.");
            Assert.That(f.ForwardSpeed, Is.EqualTo(baseline).Within(tolerance), "The installed reverse thrusters return physical speed to the original cap within the capacity-derived bound.");
        }
        [Test] public void Shield_Toggle()
        {
            using var f = new Fixture(); var charged = f.Em.GetComponentData<ShieldState>(f.Root);
            float hp = charged.Hp;
            f.Input(press: AbilityKind.ShieldToggle); f.Tick(); Assert.AreEqual(0, f.Em.GetComponentData<ShieldControl>(f.Root).RequestedOn);
            Assert.AreEqual(0, f.Em.GetComponentData<ShieldState>(f.Root).Active); Assert.AreEqual(0, f.Ability(AbilityKind.ShieldToggle).Active);
            f.Tick(60); Assert.AreEqual(0, f.Em.GetComponentData<ShieldState>(f.Root).Active);
            Assert.AreEqual(hp, f.Em.GetComponentData<ShieldState>(f.Root).Hp, .001f, "Manual off preserves stored charge.");
            f.Input(press: AbilityKind.ShieldToggle); f.Tick(); Assert.AreEqual(1, f.Em.GetComponentData<ShieldControl>(f.Root).RequestedOn);
            Assert.AreEqual(1, f.Ability(AbilityKind.ShieldToggle).Active, "The ability slot reflects requested state immediately.");
            var enabling = f.Em.GetComponentData<ShieldState>(f.Root);
            Assert.AreEqual(0, enabling.Active, "Power demand sampled the previous off state before this activation.");
            Assert.AreEqual(charged.MaxHp, enabling.MaxHp, .001f, "A non-requesting emitter's manual transition retains capacity.");
            Assert.AreEqual(hp, enabling.Hp, .001f, "Re-enable must retain charge while awaiting next tick's power demand.");
            f.Tick();
            var enabled = f.Em.GetComponentData<ShieldState>(f.Root);
            Assert.AreEqual(1, enabled.Active, "The next tick's emitter grant must reactivate the shield.");
            Assert.AreEqual(charged.MaxHp, enabled.MaxHp, .001f);
            Assert.AreEqual(hp, enabled.Hp, .001f, "Re-enable must preserve the manually retained charge across demand latency.");
        }
        [Test] public void ThrustersLost_RemovesBoost()
        {
            using var f = new Fixture(); f.Input(press: AbilityKind.StatBoost); f.Tick(3);
            Assert.AreEqual(1, f.Ability(AbilityKind.StatBoost).Active); Assert.AreEqual(2, f.Em.GetBuffer<ShipStatModifier>(f.Root).Length);
            ulong before = CellStateHasher.ComputeGridHash(f.Em, f.Root); f.DestroyThrusters(); f.Tick();
            Assert.AreNotEqual(before, CellStateHasher.ComputeGridHash(f.Em, f.Root)); int destroyed = 0;
            foreach (var instance in f.Em.GetBuffer<BlockInstance>(f.Root))
            {
                bool lost = (instance.Flags & BlockInstanceFlags.Destroyed) != 0;
                if (instance.Block == f.ThrusterBlock)
                { Assert.IsTrue(lost); Assert.AreEqual(0, instance.CellsAlive); destroyed++; }
                else Assert.IsFalse(lost, "Targeted damage must preserve every non-thruster block, including core and shield.");
            }
            Assert.AreEqual(6, destroyed); Assert.IsFalse(f.Em.HasComponent<Hulk>(f.Root), "The core and shield survive targeted thruster loss.");
            f.Tick(2); Assert.AreEqual(-1, f.Slot(AbilityKind.StatBoost)); Assert.AreEqual(1, f.Slots.Length);
            Assert.AreEqual(0, f.Em.GetBuffer<ShipStatModifier>(f.Root).Length); Assert.AreEqual(0, f.Em.GetComponentData<PropulsionStats>(f.Root).AliveThrusterCount);
            Assert.AreEqual(0, f.Em.GetBuffer<FlightThruster>(f.Root).Length); Assert.AreEqual(1, f.Em.GetComponentData<ShieldState>(f.Root).Active);
        }
        [Test] public void FrameRateIndependent()
        {
            History[] expected = null;
            foreach (int fps in new[] { 30, 60, 144 })
            {
                using var f = new Fixture();
                f.Em.AddComponentData(f.Root, new Script { Input = f.InputEntity, BoostSlot = f.Slot(AbilityKind.StatBoost), ShieldSlot = f.Slot(AbilityKind.ShieldToggle) });
                var history = f.Em.AddBuffer<History>(f.Root); history.Capacity = 240;
                var command = f.World.World.GetExistingSystemManaged<ShipCommandGroup>(); command.AddSystemToUpdateList(f.World.World.GetOrCreateSystem<ScriptSystem>()); command.SortSystems();
                var post = f.World.World.GetExistingSystemManaged<GameplayPostPhysicsGroup>(); post.AddSystemToUpdateList(f.World.World.GetOrCreateSystem<RecordSystem>()); post.SortSystems();
                uint start = f.World.TicksRun; int frames = 0;
                while (f.World.TicksRun - start < 240) { f.World.StepFrame(1f / fps); Assert.Less(++frames, fps * 5); }
                Assert.AreEqual(240u, f.World.TicksRun - start);
                var actual = f.Em.GetBuffer<History>(f.Root).AsNativeArray().ToArray(); Assert.AreEqual(240, actual.Length);
                int activated = 0, denied = 0, pending = 0;
                for (int tick = 0; tick < actual.Length; tick++)
                { Assert.AreEqual(tick, actual[tick].Tick); activated += actual[tick].Activated; denied += actual[tick].Denied; pending += actual[tick].Pending; }
                Assert.AreEqual(5, activated, "Three granted boosts and two shield toggles."); Assert.AreEqual(1, denied); Assert.AreEqual(3, pending);
                Assert.AreEqual(AbilitySlotState.PendingPower, actual[0].Boost.State);
                Assert.AreEqual(1, actual[1].Boost.Active); Assert.AreEqual(1, actual[1].SpeedMultiplier);
                Assert.AreEqual(1.8f, actual[2].SpeedMultiplier); Assert.AreEqual(2, actual[2].ModifierCount);
                Assert.AreEqual(0, actual[61].Boost.Active); Assert.AreEqual(1, actual[61].SpeedMultiplier);
                Assert.AreEqual(0, actual[20].Control.RequestedOn); Assert.AreEqual(1, actual[80].Control.RequestedOn);
                Assert.AreEqual(1, actual[101].Denied); Assert.AreEqual(1, actual[221].Boost.Active);
                if (expected == null) expected = actual; else CollectionAssert.AreEqual(expected, actual, $"Every tick's complete slot/state/status history must match at {fps} FPS.");
            }
        }
        static void Increment(ref PlayerEdgeCounters edges, int slot)
        {
            switch (slot) { case 0: edges.Ability1++; break; case 1: edges.Ability2++; break; case 2: edges.Ability3++; break; case 3: edges.Ability4++; break; default: throw new ArgumentOutOfRangeException(nameof(slot)); }
        }
        struct Script : IComponentData { public Entity Input; public int Tick, BoostSlot, ShieldSlot; public PlayerEdgeCounters Edges; }
        struct History : IBufferElementData
        {
            public int Tick, Activated, Denied, Pending, ModifierCount;
            public AbilityState Ability;
            public AbilitySlot Boost, Shield;
            public ShieldControl Control;
            public byte ShieldActive;
            public float ShieldHp, ForwardCap, ReverseCap, StrafeCap, SpeedMultiplier, ThrustMultiplier;
            public ShipStatModifier SpeedModifier, ThrustModifier;
        }
        [DisableAutoCreation, UpdateInGroup(typeof(ShipCommandGroup), OrderFirst = true), UpdateBefore(typeof(PlayerCommandApplySystem))]
        internal partial struct ScriptSystem : ISystem
        {
            public void OnUpdate(ref SystemState state)
            {
                foreach (var script in SystemAPI.Query<RefRW<Script>>())
                {
                    ref var value = ref script.ValueRW; int tick = value.Tick++;
                    if (tick == 0 || tick == 100 || tick == 101 || tick == 220) Increment(ref value.Edges, value.BoostSlot);
                    if (tick == 20 || tick == 80) Increment(ref value.Edges, value.ShieldSlot);
                    state.EntityManager.SetComponentData(value.Input, new PlayerCommand { Thrust = new float2(0, 1), Edges = value.Edges });
                }
            }
        }
        [DisableAutoCreation, UpdateInGroup(typeof(GameplayPostPhysicsGroup), OrderLast = true), UpdateBefore(typeof(SimClockAdvanceSystem))]
        internal partial struct RecordSystem : ISystem
        {
            public void OnUpdate(ref SystemState state)
            {
                foreach (var (script, ability, slots, records, entity) in SystemAPI.Query<RefRO<Script>, RefRO<AbilityState>, DynamicBuffer<AbilitySlot>, DynamicBuffer<History>>().WithEntityAccess())
                {
                    Assert.AreEqual(2, slots.Length);
                    var record = new History { Tick = script.ValueRO.Tick - 1, Ability = ability.ValueRO,
                        Boost = slots[script.ValueRO.BoostSlot], Shield = slots[script.ValueRO.ShieldSlot] };
                    foreach (var slot in slots)
                    {
                        if ((slot.Flags & AbilitySlotFlags.ActivatedThisTick) != 0) record.Activated++;
                        if ((slot.Flags & AbilitySlotFlags.DeniedThisTick) != 0) record.Denied++;
                        if (slot.State == AbilitySlotState.PendingPower) record.Pending++;
                    }
                    record.Control = state.EntityManager.GetComponentData<ShieldControl>(entity);
                    var shield = state.EntityManager.GetComponentData<ShieldState>(entity); record.ShieldActive = shield.Active; record.ShieldHp = shield.Hp;
                    var flight = state.EntityManager.GetComponentData<FlightState>(entity);
                    record.ForwardCap = flight.ForwardCap; record.ReverseCap = flight.ReverseCap; record.StrafeCap = flight.StrafeCap;
                    var multipliers = state.EntityManager.GetComponentData<ShipStatMultipliers>(entity);
                    record.SpeedMultiplier = multipliers.Get(StatId.MaxSpeed); record.ThrustMultiplier = multipliers.Get(StatId.Thrust);
                    var modifiers = state.EntityManager.GetBuffer<ShipStatModifier>(entity); record.ModifierCount = modifiers.Length;
                    foreach (var modifier in modifiers)
                    {
                        Assert.AreEqual(ProducerKind.Ability, modifier.Producer);
                        if (modifier.Stat == StatId.MaxSpeed) record.SpeedModifier = modifier;
                        else { Assert.AreEqual(StatId.Thrust, modifier.Stat); record.ThrustModifier = modifier; }
                    }
                    records.Add(record);
                }
            }
        }
    }
}
