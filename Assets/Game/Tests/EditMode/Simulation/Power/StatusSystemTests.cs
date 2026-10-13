using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Blocks;
using Game.Data.Combat;
using Game.Data.Streams;
using Game.Simulation.Power;
using Game.Tests.Common;
using Game.Tests.EditMode.Voxel;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;

namespace Game.Tests.EditMode.Simulation.Power
{
    /// <summary>Six PWR-010 scenarios use the production channel/status/allocation/aggregation pipeline (SPEC-22 §5.3b).</summary>
    public sealed class StatusSystemTests
    {
        VoxelTestContent content;
        EcsTestWorld world;
        EntityManager Em => world.EntityManager;
        [SetUp] public void SetUp() => content = new VoxelTestContent();
        [TearDown] public void TearDown() { world?.Dispose(); content?.Dispose(); }
        void Start(bool producers = false)
        {
            world = producers
                ? EcsTestWorld.Create(typeof(GridSpawnSystem), typeof(PowerRegistrationSystem), typeof(ProducerA), typeof(ProducerB), typeof(StatusSystem), typeof(PowerAllocationSystem), typeof(ShipStatsAggregateSystem))
                : EcsTestWorld.Create(typeof(GridSpawnSystem), typeof(PowerRegistrationSystem), typeof(StatusSystem), typeof(PowerAllocationSystem), typeof(ShipStatsAggregateSystem));
            content.Service.CreateSingletons(Em);
            world.AddGameplayActive(); world.AddSimClock(); world.AddSingleton(PowerTuning.Defaults);
        }
        void Step() => world.StepTicks(1);
        void SetTick(uint tick)
        {
            using var query = Em.CreateEntityQuery(ComponentType.ReadOnly<SimClock>());
            var clock = query.GetSingleton<SimClock>(); clock.Tick = tick; Em.SetComponentData(query.GetSingletonEntity(), clock);
        }
        StatusChannels Channels => world.GetSingleton<StatusChannels>();
        Entity Root(out Entity core)
        {
            var root = Em.CreateEntity(typeof(VoxelGrid));
            Em.AddComponent(root, PowerGridSetup.RootTypes); Em.AddBuffer<BlockInstance>(root);
            Em.SetComponentData(root, new VoxelGrid { CellSize = .5f, Kind = GridKind.Ship, CellMax = new Unity.Mathematics.int3(10), AnchorInstance = 0 });
            Em.SetComponentData(root, new ShipPower { Stored = 200, Capacity = 200, Generation = 40, Flags = ShipPowerFlags.Initialized });
            core = Em.CreateEntity(typeof(FunctionalBlock), typeof(Inhibited), typeof(PowerLink), typeof(Powered));
            Em.SetComponentData(core, new FunctionalBlock { Grid = root, InstanceIndex = 0 });
            Em.GetBuffer<BlockInstance>(root).Add(new BlockInstance { Block = content.Index("block_core_cross"), Functional = core, CellsAlive = 7 });
            return root;
        }
        Entity Consumer(Entity root)
        {
            int instance = Em.GetBuffer<BlockInstance>(root).Length, index = Em.GetBuffer<PowerConsumer>(root).Length;
            var entity = Em.CreateEntity(typeof(FunctionalBlock), typeof(Inhibited), typeof(PowerLink), typeof(Powered));
            Em.SetComponentData(entity, new FunctionalBlock { Grid = root, InstanceIndex = instance });
            Em.SetComponentData(entity, new PowerLink { Bound = 1, ConsumerCount = 1 });
            Em.AddBuffer<PowerRequest>(entity).Add(new PowerRequest { Module = BlockModuleId.WeaponMount, Draw = 12, Priority = 100, Requesting = 1, ConsumerIndex = (short)index });
            Em.GetBuffer<BlockInstance>(root).Add(new BlockInstance { Block = content.Index("block_thruster_medium"), Functional = entity, CellsAlive = 7 });
            Em.GetBuffer<PowerConsumer>(root).Add(new PowerConsumer { Functional = entity, InstanceIndex = instance, Module = BlockModuleId.WeaponMount, Draw = 12, Priority = 100, RequestIndex = 0 });
            return entity;
        }
        static StatModifierRequest Modifier(Entity root, float multiplier, uint until = uint.MaxValue, ushort source = 1, uint sequence = 1,
            StatId stat = StatId.Thrust, ProducerKind producer = ProducerKind.Ability)
            => new StatModifierRequest { Root = root, Multiplier = multiplier, UntilTick = until, SourceId = source, Sequence = sequence, Stat = stat, Producer = producer };
        void Post(StatModifierRequest request) => Channels.StatModifiers.PostFromMainThread(request);
        float Stat(Entity root, StatId stat) => Em.GetComponentData<ShipStatMultipliers>(root).Get(stat);

        [Test]
        public void Inhibit_Entity()
        {
            Start(); var root = Root(out _); var weapon = Consumer(root); var other = Consumer(root);
            Channels.Inhibits.PostFromMainThread(new InhibitRequest { Target = weapon, DurationTicks = 120, Reason = 2, Producer = ProducerKind.Ability, Sequence = 1 });
            Step();
            Assert.AreEqual(120u, Em.GetComponentData<Inhibited>(weapon).UntilTick);
            Assert.IsFalse(Em.IsComponentEnabled<Powered>(weapon)); Assert.IsTrue(Em.IsComponentEnabled<Powered>(other));
            Assert.AreEqual(12, Em.GetComponentData<ShipPower>(root).DemandPerSecond, "Only the other consumer draws during the inhibit.");
            Channels.Inhibits.PostFromMainThread(new InhibitRequest { Target = weapon, DurationTicks = 3, Reason = 7, Producer = ProducerKind.Status, Sequence = 2 });
            for (int tick = 1; tick < 120; tick++) { Step(); Assert.IsFalse(Em.IsComponentEnabled<Powered>(weapon), "tick " + tick); }
            Assert.AreEqual(120u, Em.GetComponentData<Inhibited>(weapon).UntilTick, "A shorter refresh never shortens the deadline.");
            Assert.AreEqual(7, Em.GetComponentData<Inhibited>(weapon).Reason);
            Step(); Assert.IsTrue(Em.IsComponentEnabled<Powered>(weapon), "tick 120 re-enables without adding a brownout hold.");
            Assert.AreEqual(24, Em.GetComponentData<ShipPower>(root).DemandPerSecond);
            SetTick(uint.MaxValue - 2);
            Channels.Inhibits.PostFromMainThread(new InhibitRequest { Target = weapon, DurationTicks = 5, Producer = ProducerKind.Status });
            Step(); Assert.AreEqual(uint.MaxValue, Em.GetComponentData<Inhibited>(weapon).UntilTick, "Duration addition saturates instead of wrapping into the past.");
            Assert.IsFalse(Em.IsComponentEnabled<Powered>(weapon));
        }

        [Test]
        public void Inhibit_Root()
        {
            Start(); var root = Root(out var core); var a = Consumer(root); var b = Consumer(root); var dead = Consumer(root);
            var functional = Em.GetComponentData<FunctionalBlock>(dead); var instances = Em.GetBuffer<BlockInstance>(root);
            var entry = instances[functional.InstanceIndex]; entry.Flags = BlockInstanceFlags.Destroyed; instances[functional.InstanceIndex] = entry;
            using (var q = Em.CreateEntityQuery(ComponentType.ReadOnly<FunctionalComponentRegistration>()))
            {
                var registration = q.GetSingletonBuffer<FunctionalComponentRegistration>(true); int found = 0;
                foreach (var r in registration) if (r.Module == BlockModuleId.None && r.Type == ComponentType.ReadWrite<Inhibited>()) found++;
                Assert.AreEqual(1, found, "Production registration covers every functional module.");
            }
            Channels.Inhibits.PostFromMainThread(new InhibitRequest { Target = root, DurationTicks = 5, Reason = 8, Producer = ProducerKind.Environment });
            Step();
            foreach (var entity in new[] { core, a, b })
            { Assert.AreEqual(5u, Em.GetComponentData<Inhibited>(entity).UntilTick); Assert.IsFalse(Em.IsComponentEnabled<Powered>(entity)); }
            Assert.AreEqual(0u, Em.GetComponentData<Inhibited>(dead).UntilTick, "Destroyed instances are skipped.");
            Assert.AreEqual(0, Em.GetComponentData<ShipPower>(root).DemandPerSecond);
            for (int i = 1; i < 5; i++) Step();
            Step(); foreach (var entity in new[] { core, a, b }) Assert.IsTrue(Em.IsComponentEnabled<Powered>(entity));
        }

        [Test]
        public void Modifier_Upsert()
        {
            Start(); var root = Root(out _);
            Post(Modifier(root, 3, sequence: 20)); Post(Modifier(root, 2, sequence: 10));
            Step(); Assert.AreEqual(1, Em.GetBuffer<ShipStatModifier>(root).Length); Assert.AreEqual(3, Stat(root, StatId.Thrust));
            Post(Modifier(root, 4, producer: ProducerKind.Environment));
            Post(Modifier(root, .5f, source: 2, stat: StatId.MaxSpeed));
            Post(Modifier(root, float.NaN, source: 99)); Post(Modifier(root, 0, source: 98)); Post(Modifier(root, 2, source: 97, stat: (StatId)255));
            Step(); Assert.AreEqual(3, Em.GetBuffer<ShipStatModifier>(root).Length); Assert.AreEqual(12, Stat(root, StatId.Thrust)); Assert.AreEqual(.5f, Stat(root, StatId.MaxSpeed));
            // Equal complete keys use stable gather order; the last inbox request wins without creating another element.
            Post(Modifier(root, 5, until: 300, sequence: 30)); Post(Modifier(root, 6, until: 400, sequence: 30));
            Step(); Assert.AreEqual(3, Em.GetBuffer<ShipStatModifier>(root).Length); Assert.AreEqual(24, Stat(root, StatId.Thrust));
            Assert.AreEqual(400u, Em.GetBuffer<ShipStatModifier>(root)[0].UntilTick);
        }

        [Test]
        public void Modifier_Cancel()
        {
            Start(); var root = Root(out _);
            Post(Modifier(root, 3)); Post(Modifier(root, 2, source: 2)); Post(Modifier(root, .5f, source: 3, stat: StatId.MaxSpeed)); Step();
            Post(Modifier(root, 1, source: 2)); Post(Modifier(root, 1, source: 99)); Step();
            var buffer = Em.GetBuffer<ShipStatModifier>(root); Assert.AreEqual(2, buffer.Length);
            Assert.AreEqual(1, buffer[0].SourceId); Assert.AreEqual(3, buffer[1].SourceId); Assert.AreEqual(3, Stat(root, StatId.Thrust));
            Post(Modifier(root, 5, until: world.GetSingleton<SimClock>().Tick)); Step();
            Assert.AreEqual(1, Em.GetBuffer<ShipStatModifier>(root).Length); Assert.AreEqual(1, Stat(root, StatId.Thrust)); Assert.AreEqual(.5f, Stat(root, StatId.MaxSpeed));
            // Missing/dead targets are dropped without touching another root or creating components.
            Post(Modifier(Entity.Null, 2)); Step(); Assert.AreEqual(1, Em.GetBuffer<ShipStatModifier>(root).Length);
        }

        [Test]
        public void Modifier_Expires()
        {
            Start(); var root = Root(out _);
            Post(Modifier(root, 3, until: 60)); Post(Modifier(root, 2, source: 2)); Post(Modifier(root, 4, until: 60, source: 3, stat: StatId.MaxSpeed)); Step();
            for (int tick = 1; tick < 60; tick++) { Assert.AreEqual(6, Stat(root, StatId.Thrust)); Step(); }
            Step(); var buffer = Em.GetBuffer<ShipStatModifier>(root);
            Assert.AreEqual(1, buffer.Length); Assert.AreEqual(2, buffer[0].SourceId);
            Assert.AreEqual(2, Stat(root, StatId.Thrust)); Assert.AreEqual(1, Stat(root, StatId.MaxSpeed), "Expiry reaches aggregation in this tick.");
            SetTick(uint.MaxValue); Step(); Assert.AreEqual(1, Em.GetBuffer<ShipStatModifier>(root).Length);
            Assert.AreEqual(2, Stat(root, StatId.Thrust), "The until-cancelled sentinel survives the final representable tick.");
        }

        [Test]
        public void Deterministic()
        {
            int previous = JobsUtility.JobWorkerCount; Snapshot reference = null;
            try
            {
                foreach (int workers in new[] { 1, System.Math.Max(1, previous) })
                    foreach (bool reverse in new[] { false, true })
                    {
                        JobsUtility.JobWorkerCount = workers; var actual = RunParallel(reverse);
                        if (reference == null) reference = actual;
                        else { Assert.AreEqual(reference.Inhibit, actual.Inhibit); Assert.AreEqual(reference.Power, actual.Power); CollectionAssert.AreEqual(reference.Modifiers, actual.Modifiers); Assert.AreEqual(reference.Thrust, actual.Thrust); Assert.AreEqual(reference.Speed, actual.Speed); }
                    }
            }
            finally { JobsUtility.JobWorkerCount = previous; }
        }
        Snapshot RunParallel(bool reverse)
        {
            Start(true); var root = Root(out _); var weapon = Consumer(root);
            var i = new[]
            {
                new InhibitRequest { Target = weapon, Producer = ProducerKind.Ability, Sequence = 2, DurationTicks = 20, Reason = 7 },
                new InhibitRequest { Target = weapon, Producer = ProducerKind.Ability, Sequence = 1, DurationTicks = 40, Reason = 3 },
                new InhibitRequest { Target = root, Producer = ProducerKind.Environment, Sequence = 1, DurationTicks = 5, Reason = 8 }
            };
            var m = new[] { Modifier(root, 3, until: 100, sequence: 2), Modifier(root, 2, until: 100, sequence: 1),
                Modifier(root, .25f, source: 2, stat: StatId.MaxSpeed, producer: ProducerKind.Environment), Modifier(root, .5f, until: 100, source: 3, producer: ProducerKind.Status) };
            using var ia = new NativeArray<InhibitRequest>(reverse ? new[] { i[2] } : new[] { i[0], i[1] }, Allocator.TempJob);
            using var ib = new NativeArray<InhibitRequest>(reverse ? new[] { i[0], i[1] } : new[] { i[2] }, Allocator.TempJob);
            using var ma = new NativeArray<StatModifierRequest>(reverse ? new[] { m[2], m[3] } : new[] { m[0], m[1] }, Allocator.TempJob);
            using var mb = new NativeArray<StatModifierRequest>(reverse ? new[] { m[0], m[1] } : new[] { m[2], m[3] }, Allocator.TempJob);
            world.AddSingleton(new BatchA { Inhibits = ia, Modifiers = ma }); world.AddSingleton(new BatchB { Inhibits = ib, Modifiers = mb });
            try
            {
                Step(); var snapshot = new Snapshot { Inhibit = Em.GetComponentData<Inhibited>(weapon), Power = Em.GetComponentData<ShipPower>(root),
                    Thrust = Stat(root, StatId.Thrust), Speed = Stat(root, StatId.MaxSpeed), Modifiers = new ShipStatModifier[Em.GetBuffer<ShipStatModifier>(root).Length] };
                Em.GetBuffer<ShipStatModifier>(root).AsNativeArray().CopyTo(snapshot.Modifiers);
                Assert.AreEqual(40u, snapshot.Inhibit.UntilTick); Assert.AreEqual(7, snapshot.Inhibit.Reason); Assert.AreEqual(1.5f, snapshot.Thrust); Assert.AreEqual(.25f, snapshot.Speed);
                return snapshot;
            }
            finally { world.Dispose(); world = null; }
        }
        sealed class Snapshot { public Inhibited Inhibit; public ShipPower Power; public ShipStatModifier[] Modifiers; public float Thrust, Speed; }

        public struct BatchA : IComponentData { public NativeArray<InhibitRequest> Inhibits; public NativeArray<StatModifierRequest> Modifiers; }
        public struct BatchB : IComponentData { public NativeArray<InhibitRequest> Inhibits; public NativeArray<StatModifierRequest> Modifiers; }
        [BurstCompile, UpdateInGroup(typeof(PowerDemandGroup)), UpdateBefore(typeof(ProducerB))]
        public partial struct ProducerA : ISystem
        {
            public void OnCreate(ref SystemState state) { state.RequireForUpdate<StatusChannels>(); state.RequireForUpdate<BatchA>(); }
            [BurstCompile] public void OnUpdate(ref SystemState state)
            { var batch = SystemAPI.GetSingleton<BatchA>(); Submit(ref state, SystemAPI.GetSingleton<StatusChannels>(), batch.Inhibits, batch.Modifiers); state.Enabled = false; }
        }
        [BurstCompile, UpdateInGroup(typeof(PowerDemandGroup))]
        public partial struct ProducerB : ISystem
        {
            public void OnCreate(ref SystemState state) { state.RequireForUpdate<StatusChannels>(); state.RequireForUpdate<BatchB>(); }
            [BurstCompile] public void OnUpdate(ref SystemState state)
            { var batch = SystemAPI.GetSingleton<BatchB>(); Submit(ref state, SystemAPI.GetSingleton<StatusChannels>(), batch.Inhibits, batch.Modifiers); state.Enabled = false; }
        }
        static void Submit(ref SystemState state, StatusChannels channels, NativeArray<InhibitRequest> inhibits, NativeArray<StatModifierRequest> modifiers)
        {
            if (inhibits.Length > 0)
            {
                var writer = channels.Inhibits.BeginWrite(ref state, ProducerKind.Test, inhibits.Length);
                state.Dependency = new InhibitWriter { Input = inhibits, Writer = writer }.Schedule(inhibits.Length, 1, state.Dependency);
                channels.Inhibits.EndWrite(state.Dependency);
            }
            if (modifiers.Length > 0)
            {
                var writer = channels.StatModifiers.BeginWrite(ref state, ProducerKind.Test, modifiers.Length);
                state.Dependency = new ModifierWriter { Input = modifiers, Writer = writer }.Schedule(modifiers.Length, 1, state.Dependency);
                channels.StatModifiers.EndWrite(state.Dependency);
            }
        }
        [BurstCompile] struct InhibitWriter : IJobParallelFor
        {
            [ReadOnly] public NativeArray<InhibitRequest> Input; public NativeStream.Writer Writer;
            public void Execute(int i) { Writer.BeginForEachIndex(i); Writer.Write(Input[i]); Writer.EndForEachIndex(); }
        }
        [BurstCompile] struct ModifierWriter : IJobParallelFor
        {
            [ReadOnly] public NativeArray<StatModifierRequest> Input; public NativeStream.Writer Writer;
            public void Execute(int i) { Writer.BeginForEachIndex(i); Writer.Write(Input[i]); Writer.EndForEachIndex(); }
        }
    }
}
