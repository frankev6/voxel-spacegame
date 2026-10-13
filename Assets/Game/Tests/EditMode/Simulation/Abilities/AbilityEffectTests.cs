using System;
using System.Linq;
using Game.Core;
using Game.Core.Components;
using Game.Data.Blocks;
using Game.Data.Combat;
using Game.Data.Components;
using Game.Data.Definitions;
using Game.Simulation.Abilities;
using Game.Simulation.Commands;
using Game.Simulation.Power;
using Game.Tests.Common;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Game.Voxel.PresentationStreams;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

namespace Game.Tests.EditMode.Simulation.Abilities
{
    /// <summary>ABL-004's two required checks exercise the real lifecycle, effect producers and status consumer in tick order.</summary>
    public sealed class AbilityEffectTests : GameTestBase
    {
        sealed class Fixture : IDisposable
        {
            public readonly World World = new World("Ability effects");
            public readonly Entity Root;
            public EntityManager Em => World.EntityManager;
            readonly Entity clock;
            readonly SystemHandle status, ability, toggle, boost;
            readonly BlobAssetReference<AbilityDefsBlob> definitions;
            readonly BlobAssetReference<BlockDefsBlob> blocks;
            public uint Tick;
            public DynamicBuffer<AbilitySlot> Slots => Em.GetBuffer<AbilitySlot>(Root);
            public DynamicBuffer<ShipStatModifier> Modifiers => Em.GetBuffer<ShipStatModifier>(Root);
            public Fixture()
            {
                using (var builder = new BlobBuilder(Allocator.Temp))
                {
                    ref var data = ref builder.ConstructRoot<AbilityDefsBlob>(); var rows = builder.Allocate(ref data.Abilities, 4);
                    rows[1] = new AbilityDef { IdHash = new ContentHash(1), Kind = AbilityKind.ShieldToggle, MaxCharges = 1, CooldownTicks = 60, ShieldEnableThreshold = .3f };
                    rows[2] = new AbilityDef { IdHash = new ContentHash(2), Kind = AbilityKind.StatBoost, MaxCharges = 2, EnergyCost = 15,
                        CooldownTicks = 30, ChargeRechargeTicks = 180, DurationTicks = 60 };
                    var mods = builder.Allocate(ref rows[2].Mods, 2);
                    mods[0] = new AbilityStatModDef { Stat = StatId.MaxSpeed, Multiplier = 1.8f };
                    mods[1] = new AbilityStatModDef { Stat = StatId.Thrust, Multiplier = 2 };
                    rows[3] = new AbilityDef { IdHash = new ContentHash(3), Kind = AbilityKind.ShieldToggle, MaxCharges = 1, CooldownTicks = 60, ShieldEnableThreshold = .5f };
                    definitions = builder.CreateBlobAssetReference<AbilityDefsBlob>(Allocator.Persistent);
                }
                using (var builder = new BlobBuilder(Allocator.Temp))
                {
                    ref var data = ref builder.ConstructRoot<BlockDefsBlob>(); var rows = builder.Allocate(ref data.Blocks, 4);
                    for (ushort i = 1; i < rows.Length; i++) { var grants = builder.Allocate(ref rows[i].AbilityIndices, 1); grants[0] = i; }
                    blocks = builder.CreateBlobAssetReference<BlockDefsBlob>(Allocator.Persistent);
                }
                Em.CreateSingleton(new AbilityDefsRef { Value = definitions }); Em.CreateSingleton(new BlockDefsRef { Value = blocks });
                clock = Em.CreateSingleton(new SimClock()); Em.CreateEntity(typeof(GameplayActive));
                Em.AddBuffer<RootComponentRegistration>(Em.CreateEntity());
                World.GetOrCreateSystem<FxEventQueueCreateSystem>(); World.GetOrCreateSystem<SfxEventQueueCreateSystem>();
                status = World.GetOrCreateSystem<StatusSystem>(); ability = World.GetOrCreateSystem<AbilitySystem>();
                toggle = World.GetOrCreateSystem<ShieldToggleEffectSystem>(); boost = World.GetOrCreateSystem<StatBoostEffectSystem>();
                Root = Em.CreateEntity(typeof(AbilityState), typeof(ShipCommand), typeof(ShipStats), typeof(ShieldControl), typeof(ShieldState));
                Em.AddBuffer<AbilitySlot>(Root); Em.AddBuffer<BlockInstance>(Root); Em.AddBuffer<ShipStatModifier>(Root);
                Em.SetComponentData(Root, new ShieldControl { RequestedOn = 1 }); ShieldHp(100); Revision(1);
            }
            public void ShieldHp(float hp, float max = 100) => Em.SetComponentData(Root, new ShieldState { Hp = hp, MaxHp = max });
            public void Blocks(params ushort[] ids)
            { var instances = Em.GetBuffer<BlockInstance>(Root); instances.Clear(); foreach (ushort id in ids) instances.Add(new BlockInstance { Block = id }); }
            public void Revision(uint revision) => Em.SetComponentData(Root, new ShipStats { FunctionalRevision = revision, Flags = ShipStatsFlags.Initialized });
            public void Step(ShipCommandEdges edges = ShipCommandEdges.None)
            {
                Em.SetComponentData(clock, new SimClock { Tick = ++Tick }); Em.SetComponentData(Root, new ShipCommand { Edges = edges });
                // Production ordering: status precedes ability activation, so newly posted effects apply next tick.
                status.Update(World.Unmanaged); ability.Update(World.Unmanaged); toggle.Update(World.Unmanaged); boost.Update(World.Unmanaged);
                Em.CompleteAllTrackedJobs();
            }
            public void Until(uint tick) { while (Tick < tick) Step(); }
            public void SeedForeignModifier()
            {
                using var query = Em.CreateEntityQuery(typeof(StatusChannels));
                query.GetSingleton<StatusChannels>().StatModifiers.PostFromMainThread(new StatModifierRequest
                { Root = Root, Producer = ProducerKind.Status, SourceId = 17, Stat = StatId.FireRate, Multiplier = 1.25f, UntilTick = uint.MaxValue });
            }
            public void Dispose() { World.Dispose(); definitions.Dispose(); blocks.Dispose(); }
        }

        [Test] public void Toggle_FlipsOnce()
        {
            using var f = new Fixture(); f.Blocks(1, 3); f.Step();
            Assert.AreEqual(2, f.Slots.Length); foreach (var slot in f.Slots) { Assert.AreEqual(1, slot.Active); Assert.AreEqual(0, slot.Blocked); }
            f.Step(ShipCommandEdges.Ability1 | ShipCommandEdges.Ability2);
            Assert.AreEqual(0, f.Em.GetComponentData<ShieldControl>(f.Root).RequestedOn, "Two activated toggle slots flip the shared control once.");
            foreach (var slot in f.Slots) Assert.AreEqual(0, slot.Active);
            f.Step(); Assert.AreEqual(0, f.Em.GetComponentData<ShieldControl>(f.Root).RequestedOn, "Cleared activation flags cannot toggle again.");
            f.ShieldHp(29); f.Until(62); foreach (var slot in f.Slots) Assert.AreEqual(1, slot.Blocked);
            f.Step(ShipCommandEdges.Ability1); Assert.AreEqual(0, f.Em.GetComponentData<ShieldControl>(f.Root).RequestedOn);
            Assert.AreNotEqual(0, f.Slots[0].Flags & AbilitySlotFlags.DeniedThisTick);
            f.ShieldHp(.3f * 100); f.Step(); Assert.AreEqual(0, f.Slots[0].Blocked); Assert.AreEqual(1, f.Slots[1].Blocked);
            f.Step(ShipCommandEdges.Ability1); Assert.AreEqual(1, f.Em.GetComponentData<ShieldControl>(f.Root).RequestedOn);
            foreach (var slot in f.Slots) Assert.AreEqual(1, slot.Active);
            uint on = f.Tick; f.ShieldHp(1); f.Until(on + 60);
            foreach (var slot in f.Slots) Assert.AreEqual(0, slot.Blocked, "Switching off is always available, including below the enabling threshold.");
            f.Step(ShipCommandEdges.Ability1); Assert.AreEqual(0, f.Em.GetComponentData<ShieldControl>(f.Root).RequestedOn);
            f.ShieldHp(0, 0); f.Step(); foreach (var slot in f.Slots) Assert.AreEqual(1, slot.Blocked);
            f.Em.AddComponent<Hulk>(f.Root); f.Step();
            foreach (var slot in f.Slots) { Assert.AreEqual(AbilitySlotState.Unavailable, slot.State); Assert.AreEqual(0, slot.Active); }
        }
        [Test] public void Boost_ModifiersAddedAndRemoved()
        {
            using var f = new Fixture(); f.Blocks(1, 2); f.SeedForeignModifier(); f.Step(); AssertForeignOnly(f);
            f.Step(ShipCommandEdges.Ability2); Assert.AreEqual(AbilitySlotState.PendingPower, f.Slots[1].State); AssertForeignOnly(f);
            // Blocked belongs to the kind's effect writer; boosts clear a stale value before their next request.
            var slots = f.Slots; var slot = slots[1]; slot.Blocked = 1; slots[1] = slot;
            f.Step(); uint until = f.Slots[1].ActiveUntilTick; Assert.AreEqual(1, f.Slots[1].Active); Assert.AreEqual(0, f.Slots[1].Blocked);
            AssertForeignOnly(f, "Activation posts requests but never edits ShipStatModifier directly.");
            f.Step(); AssertBoost(f, 1, until);
            f.Blocks(2); f.Revision(2); f.Step(); Assert.AreEqual(1, f.Slots.Length); Assert.AreEqual(until, f.Slots[0].ActiveUntilTick);
            AssertBoost(f, 1, until, "Slot relocation applies through status on the following tick.");
            f.Step(); AssertBoost(f, 0, until, "Cancels sort before re-posts for the same key; moved slots do not leave old modifiers.");
            f.Until(until - 1); AssertBoost(f, 0, until); f.Step(); AssertForeignOnly(f); Assert.AreEqual(0, f.Slots[0].Active);

            f.Step(ShipCommandEdges.Ability1); f.Step(); until = f.Slots[0].ActiveUntilTick; f.Step(); AssertBoost(f, 0, until);
            var instances = f.Em.GetBuffer<BlockInstance>(f.Root); var instance = instances[0]; instance.Flags |= BlockInstanceFlags.Destroyed; instances[0] = instance;
            f.Revision(3); f.Step(); Assert.AreEqual(0, f.Slots.Length); Assert.AreEqual(1, f.Em.GetComponentData<AbilityState>(f.Root).Resynced);
            AssertBoost(f, 0, until, "Removal is a next-tick request even when there is no surviving slot.");
            f.Step(); AssertForeignOnly(f, "ADR-0069's root signal cancels the last removed slot.");

            f.Blocks(2); f.Revision(4); f.Step(); f.Step(ShipCommandEdges.Ability1); f.Step(); until = f.Slots[0].ActiveUntilTick;
            f.Step(); AssertBoost(f, 0, until); f.Em.AddComponent<Hulk>(f.Root); f.Step();
            Assert.AreEqual(AbilitySlotState.Unavailable, f.Slots[0].State); Assert.AreEqual(0, f.Slots[0].Active);
            f.Step(); AssertForeignOnly(f, "Hulk resync cancels ability modifiers while other producers keep their own keys.");
        }
        static void AssertForeignOnly(Fixture f, string message = null)
        {
            var rows = f.Modifiers.ToNativeArray(Allocator.Temp); using (rows)
            {
                Assert.AreEqual(1, rows.Length, message); var row = rows[0]; Assert.AreEqual(ProducerKind.Status, row.Producer);
                Assert.AreEqual(17, row.SourceId); Assert.AreEqual(StatId.FireRate, row.Stat); Assert.AreEqual(1.25f, row.Multiplier); Assert.AreEqual(uint.MaxValue, row.UntilTick);
            }
        }
        static void AssertBoost(Fixture f, ushort source, uint until, string message = null)
        {
            using var rows = f.Modifiers.ToNativeArray(Allocator.Temp);
            Assert.AreEqual(3, rows.Length, message);
            foreach (var row in rows) if (row.Producer == ProducerKind.Ability) { Assert.AreEqual(source, row.SourceId); Assert.AreEqual(until, row.UntilTick); }
            var abilities = rows.ToArray().Where(row => row.Producer == ProducerKind.Ability).ToArray(); Assert.AreEqual(2, abilities.Length);
            Assert.AreEqual(1.8f, abilities.Single(row => row.Stat == StatId.MaxSpeed).Multiplier); Assert.AreEqual(2, abilities.Single(row => row.Stat == StatId.Thrust).Multiplier);
            Assert.AreEqual(1, rows.ToArray().Count(row => row.Producer == ProducerKind.Status), "Another producer's modifier must remain unchanged.");
        }
    }
}
