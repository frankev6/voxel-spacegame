using System;
using Game.Core;
using Game.Core.Components;
using Game.Data.Blocks;
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
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode.Simulation.Abilities
{
    /// <summary>ABL-003's nine specified checks run the production lifecycle and power allocator.</summary>
    public sealed class AbilitySystemTests : GameTestBase
    {
        sealed class Fixture : IDisposable
        {
            public readonly World World = new World("Ability lifecycle");
            public EntityManager Em => World.EntityManager;
            public readonly Entity Root;
            readonly Entity clock;
            readonly SystemHandle ability, power;
            readonly BlobAssetReference<BlockDefsBlob> blocks;
            readonly BlobAssetReference<AbilityDefsBlob> definitions;
            public uint Tick;
            public int FxCount, SfxCount;
            public FxEvent LastFx;
            public SfxEvent LastSfx;
            public DynamicBuffer<AbilitySlot> Slots => Em.GetBuffer<AbilitySlot>(Root);
            public Fixture(bool player = true, float? energy = null)
            {
                using (var builder = new BlobBuilder(Allocator.Temp))
                {
                    ref var data = ref builder.ConstructRoot<AbilityDefsBlob>(); var rows = builder.Allocate(ref data.Abilities, 11);
                    for (int i = 1; i < rows.Length; i++) rows[i] = new AbilityDef { IdHash = new ContentHash((ulong)i), MaxCharges = 1, Kind = AbilityKind.ShieldToggle, CooldownTicks = 60 };
                    rows[1] = new AbilityDef { IdHash = new ContentHash(1), Kind = AbilityKind.StatBoost, MaxCharges = 2,
                        EnergyCost = 15, CooldownTicks = 30, ChargeRechargeTicks = 180, DurationTicks = 60,
                        ActivationCue = new ContentHash(101), ActivationFx = new ContentHash(102) };
                    definitions = builder.CreateBlobAssetReference<AbilityDefsBlob>(Allocator.Persistent);
                }
                using (var builder = new BlobBuilder(Allocator.Temp))
                {
                    ref var data = ref builder.ConstructRoot<BlockDefsBlob>(); var rows = builder.Allocate(ref data.Blocks, 11);
                    for (ushort i = 1; i < rows.Length; i++) builder.Allocate(ref rows[i].AbilityIndices, 1)[0] = i;
                    blocks = builder.CreateBlobAssetReference<BlockDefsBlob>(Allocator.Persistent);
                }
                Em.CreateSingleton(new AbilityDefsRef { Value = definitions }); Em.CreateSingleton(new BlockDefsRef { Value = blocks });
                Em.CreateSingleton(default(BlockModulesRef)); Em.CreateSingleton(PowerTuning.Defaults);
                clock = Em.CreateSingleton(new SimClock()); Em.CreateEntity(typeof(GameplayActive));
                Em.AddBuffer<RootComponentRegistration>(Em.CreateEntity());
                World.GetOrCreateSystem<FxEventQueueCreateSystem>(); World.GetOrCreateSystem<SfxEventQueueCreateSystem>();
                power = World.GetOrCreateSystem<PowerAllocationSystem>(); ability = World.GetOrCreateSystem<AbilitySystem>();
                Root = Em.CreateEntity(typeof(AbilityState), typeof(ShipCommand), typeof(LocalTransform));
                Em.SetComponentData(Root, LocalTransform.Identity); Em.AddBuffer<AbilitySlot>(Root); Em.AddBuffer<BlockInstance>(Root);
                if (player) Em.AddComponent<PlayerShip>(Root);
                if (energy.HasValue)
                {
                    Em.AddComponentData(Root, new ShipPower { Stored = energy.Value, Capacity = 100, Flags = ShipPowerFlags.Initialized });
                    Em.AddComponent<PowerStats>(Root); Em.AddBuffer<PowerConsumer>(Root);
                }
            }
            public void Blocks(params ushort[] ids)
            { var rows = Em.GetBuffer<BlockInstance>(Root); rows.Clear(); foreach (var id in ids) rows.Add(new BlockInstance { Block = id }); }
            public void Revision(uint value)
            {
                if (!Em.HasComponent<ShipStats>(Root)) Em.AddComponent<ShipStats>(Root);
                Em.SetComponentData(Root, new ShipStats { FunctionalRevision = value, Flags = ShipStatsFlags.Initialized });
            }
            public void Step(ShipCommandEdges edges = ShipCommandEdges.None)
            {
                Em.SetComponentData(clock, new SimClock { Tick = ++Tick }); Em.SetComponentData(Root, new ShipCommand { Edges = edges });
                power.Update(World.Unmanaged); ability.Update(World.Unmanaged); Em.CompleteAllTrackedJobs();
                using var fq = Em.CreateEntityQuery(typeof(FxEventQueue)); var fx = fq.GetSingleton<FxEventQueue>(); fx.Access.CompleteAccess();
                using var sq = Em.CreateEntityQuery(typeof(SfxEventQueue)); var sfx = sq.GetSingleton<SfxEventQueue>(); sfx.Access.CompleteAccess();
                FxCount = SfxCount = 0;
                while (fx.Queue.TryDequeue(out var f)) { FxCount++; LastFx = f; }
                while (sfx.Queue.TryDequeue(out var s)) { SfxCount++; LastSfx = s; }
            }
            public void Until(uint tick) { while (Tick < tick) Step(); }
            public void Dispose() { World.Dispose(); definitions.Dispose(); blocks.Dispose(); }
        }
        [Test] public void Slots_FromBlocks_Ordered()
        {
            using var f = new Fixture(); f.Blocks(2, 1, 2, 0, 3); var instances = f.Em.GetBuffer<BlockInstance>(f.Root);
            var destroyed = instances[4]; destroyed.Flags = BlockInstanceFlags.Destroyed; instances[4] = destroyed;
            f.Step(); Assert.AreEqual(2, f.Slots.Length); Assert.AreEqual(2, f.Slots[0].AbilityIndex); Assert.AreEqual(1, f.Slots[1].AbilityIndex);
            Assert.AreEqual(0, f.Slots[0].FirstInstanceIndex); Assert.AreEqual(1, f.Slots[1].FirstInstanceIndex);
            f.Blocks(3); f.Step(); Assert.AreEqual(2, f.Slots.Length, "Without stats, sync occurs only once.");
            f.Revision(2); f.Blocks(1,2,3,4,5,6,7,8,9,10);
            LogAssert.Expect(LogType.Warning, "Ability slots exceed 8; retaining the first eight in block instance order.");
            f.Step(); Assert.AreEqual(8, f.Slots.Length); for (int i = 0; i < 8; i++) Assert.AreEqual(i + 1, f.Slots[i].AbilityIndex);
            f.Revision(3); f.Step(); Assert.AreEqual(1, f.Em.GetComponentData<AbilityState>(f.Root).OverflowWarned);
        }
        [Test] public void Resync_KeepsTimers()
        {
            using var f = new Fixture(); f.Blocks(2, 1); f.Revision(1); f.Step(ShipCommandEdges.Ability2); f.Step();
            var before = f.Slots[1]; f.Blocks(1, 2); f.Revision(2); f.Step(); var after = f.Slots[0];
            Assert.AreEqual(before.Charges, after.Charges); Assert.AreEqual(before.ActiveUntilTick, after.ActiveUntilTick);
            Assert.AreEqual(before.CooldownRemainingTicks - 1, after.CooldownRemainingTicks); Assert.AreEqual(before.ChargeRechargeRemainingTicks - 1, after.ChargeRechargeRemainingTicks);
            Assert.AreEqual(0, after.FirstInstanceIndex); Assert.AreNotEqual(0, after.Flags & AbilitySlotFlags.Resynced);
            f.Blocks(); f.Revision(3); f.Step(); Assert.AreEqual(0, f.Slots.Length); Assert.AreEqual(1, f.Em.GetComponentData<AbilityState>(f.Root).Resynced);
            f.Step(); Assert.AreEqual(0, f.Em.GetComponentData<AbilityState>(f.Root).Resynced);
        }
        [Test] public void Boost_ChargesAndRecharge()
        {
            using var f = new Fixture(); f.Blocks(1); f.Step(ShipCommandEdges.Ability1); f.Step(); uint first = f.Tick;
            Assert.AreEqual(1, f.Slots[0].Charges); f.Until(first + 60); f.Step(ShipCommandEdges.Ability1); f.Step(); Assert.AreEqual(0, f.Slots[0].Charges);
            f.Step(ShipCommandEdges.Ability1); Assert.AreNotEqual(0, f.Slots[0].Flags & AbilitySlotFlags.DeniedThisTick);
            f.Until(first + 179); Assert.AreEqual(0, f.Slots[0].Charges); f.Step(); Assert.AreEqual(1, f.Slots[0].Charges);
        }
        [Test] public void Boost_Duration()
        {
            using var f = new Fixture(); f.Blocks(1); f.Step(ShipCommandEdges.Ability1); f.Step(); uint began = f.Tick;
            Assert.AreEqual(1, f.Slots[0].Active); Assert.AreEqual(began + 60, f.Slots[0].ActiveUntilTick);
            f.Until(began + 59); Assert.AreEqual(1, f.Slots[0].Active); f.Step(); Assert.AreEqual(0, f.Slots[0].Active); Assert.AreEqual(AbilitySlotState.Ready, f.Slots[0].State);
        }
        [Test] public void Grant_OneTickLatency()
        {
            using var f = new Fixture(energy: 30); f.Blocks(2,1); f.Revision(1); f.Step(ShipCommandEdges.Ability2);
            Assert.AreEqual(AbilitySlotState.PendingPower, f.Slots[1].State); Assert.AreEqual(30, f.Em.GetComponentData<ShipPower>(f.Root).Stored);
            Assert.AreEqual(0, f.FxCount); f.Blocks(1,2); f.Revision(2); f.Step();
            Assert.AreEqual(AbilitySlotState.Active, f.Slots[0].State); Assert.AreEqual(15, f.Em.GetComponentData<ShipPower>(f.Root).Stored);
            Assert.AreEqual(1, f.Slots[0].Charges); Assert.AreEqual(0, f.Slots[0].PendingCost);
            Assert.AreEqual(1, f.FxCount); Assert.AreEqual(1, f.SfxCount); Assert.AreEqual(FxEventFlags.PlayerSource, f.LastFx.Flags); Assert.AreEqual(SfxEventFlags.PlayerSource, f.LastSfx.Flags);
            f.Step(); Assert.AreEqual(15, f.Em.GetComponentData<ShipPower>(f.Root).Stored); Assert.AreEqual(0, f.FxCount);
        }
        [Test] public void Grant_Denied()
        {
            using var f = new Fixture(energy: 14); f.Blocks(1); f.Step(ShipCommandEdges.Ability1); f.Step();
            Assert.AreEqual(AbilitySlotState.Ready, f.Slots[0].State); Assert.AreEqual(2, f.Slots[0].Charges); Assert.AreEqual(0, f.Slots[0].PendingCost);
            Assert.AreNotEqual(0, f.Slots[0].Flags & AbilitySlotFlags.DeniedThisTick); Assert.AreEqual(14, f.Em.GetComponentData<ShipPower>(f.Root).Stored); Assert.AreEqual(0, f.FxCount);
        }
        [Test] public void ShieldToggle_Threshold()
        {
            using var f = new Fixture(); f.Blocks(2); f.Step();
            // ABL-004 owns the threshold calculation; ABL-003 must obey its Blocked input without writing it.
            var slots = f.Slots; var slot = slots[0]; slot.Blocked = 1; slots[0] = slot; f.Step(ShipCommandEdges.Ability1);
            Assert.AreEqual(1, f.Slots[0].Charges); Assert.AreNotEqual(0, f.Slots[0].Flags & AbilitySlotFlags.DeniedThisTick);
            slots = f.Slots; slot = slots[0]; slot.Blocked = 0; slots[0] = slot; f.Step(ShipCommandEdges.Ability1);
            Assert.AreEqual(0, f.Slots[0].Charges); Assert.AreNotEqual(0, f.Slots[0].Flags & AbilitySlotFlags.ActivatedThisTick); Assert.AreEqual(0, f.Slots[0].Active);
        }
        [Test] public void Hulk_Unavailable()
        {
            using var f = new Fixture(); f.Blocks(1,2); f.Step(ShipCommandEdges.Ability1); f.Step(); f.Em.AddComponent<Hulk>(f.Root); f.Step();
            for (int i = 0; i < f.Slots.Length; i++) { Assert.AreEqual(AbilitySlotState.Unavailable, f.Slots[i].State); Assert.AreEqual(0, f.Slots[i].Active); }
            Assert.AreEqual(1, f.Em.GetComponentData<AbilityState>(f.Root).Resynced); f.Step(ShipCommandEdges.Ability1);
            Assert.AreEqual(0, f.Em.GetComponentData<AbilityState>(f.Root).Resynced); Assert.AreEqual(0, f.FxCount);
        }
        [Test] public void AiEdge_Activates()
        {
            using var f = new Fixture(player: false, energy: 30); f.Blocks(1); f.Step(ShipCommandEdges.Ability1); f.Step();
            Assert.AreEqual(AbilitySlotState.Active, f.Slots[0].State); Assert.AreEqual(1, f.Slots[0].Charges); Assert.AreEqual(15, f.Em.GetComponentData<ShipPower>(f.Root).Stored);
            Assert.AreEqual(FxEventFlags.None, f.LastFx.Flags); Assert.AreEqual(SfxEventFlags.None, f.LastSfx.Flags); Assert.AreEqual(1, f.FxCount);
        }
    }
}
