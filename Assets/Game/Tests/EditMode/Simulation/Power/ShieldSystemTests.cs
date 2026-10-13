using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Components;
using Game.Data.Blocks;
using Game.Data.Combat;
using Game.Data.Components;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Streams;
using Game.Simulation.Power;
using Game.Tests.EditMode.Voxel.Damage;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Game.Voxel.PresentationStreams;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using static Game.Tests.EditMode.Data.TestContentFactory;

namespace Game.Tests.EditMode.Simulation.Power
{
    /// <summary>Nine required PWR-007 checks use production grids, channels and shield jobs.</summary>
    public sealed class ShieldSystemTests
    {
        sealed class Fixture : IDisposable
        {
            public readonly TestGridFactory Grid = new TestGridFactory();
            public readonly Entity Root, Emitter, Clock;
            public readonly SystemHandle Shield, Owner, Demand, Allocation;
            public readonly ShieldHitStreams Hits;
            public readonly DamageStreams Damage;
            public readonly List<DamageEvent> Overflow = new List<DamageEvent>();
            readonly ContentService modules;
            readonly BlobAssetReference<DamageTableBlob> table;
            public EntityManager Em => Grid.EM;
            public ShieldState State => Em.GetComponentData<ShieldState>(Root);
            public Fixture()
            {
                Root = Grid.Spawn(Grid.Box(new int3(0), new int3(3)));
                modules = ContentService.CreateForTests(new ContentDefinition[] {
                    Block("block_missing", BlockKind.Structural), Item("item_missing"),
                    Block("block_test_shield", BlockKind.Functional, modules: new BlockModule[] {
                        new ShieldEmitterModule { Params = new ShieldEmitterParams { Capacity = 3000, RegenPerSecond = 120, RechargeDelaySeconds = 4, PowerDraw = 8 } } }) });
                using (var contentWorld = new World("Shield module fixture"))
                {
                    modules.CreateSingletons(contentWorld.EntityManager);
                    using var source = contentWorld.EntityManager.CreateEntityQuery(typeof(BlockModulesRef));
                    using var destination = Em.CreateEntityQuery(typeof(BlockModulesRef));
                    Em.SetComponentData(destination.GetSingletonEntity(), source.GetSingleton<BlockModulesRef>());
                }
                using (var builder = new BlobBuilder(Allocator.Temp))
                {
                    ref var data = ref builder.ConstructRoot<DamageTableBlob>();
                    var rows = builder.Allocate(ref data.Types, Enum.GetValues(typeof(DamageType)).Length);
                    for (int i = 0; i < rows.Length; i++) rows[i] = new DamageTypeRow { ShieldMultiplier = i == (int)DamageType.Energy ? 1.25f : 1 };
                    table = builder.CreateBlobAssetReference<DamageTableBlob>(Allocator.Persistent);
                }
                Clock = Em.CreateSingleton(new SimClock()); Em.CreateSingleton(PowerTuning.Defaults);
                Owner = Grid.World.CreateSystem<DamageStreamsSystem>();
                using (var q = Em.CreateEntityQuery(typeof(DamageConfig)))
                { var cfg = q.GetSingleton<DamageConfig>(); cfg.Table = table; Em.SetComponentData(q.GetSingletonEntity(), cfg); }
                Grid.World.CreateSystem<FxEventQueueCreateSystem>();
                Shield = Grid.World.CreateSystem<ShieldSystem>(); Demand = Grid.World.CreateSystem<ShieldPowerDemandSystem>();
                Allocation = Grid.World.CreateSystem<PowerAllocationSystem>();
                using (var q = Em.CreateEntityQuery(typeof(ShieldHitStreams))) Hits = q.GetSingleton<ShieldHitStreams>();
                using (var q = Em.CreateEntityQuery(typeof(DamageStreams))) Damage = q.GetSingleton<DamageStreams>();
                Em.AddComponentData(Root, new ShieldState { Flags = ShieldFlags.FillOnNextUpdate });
                Em.AddComponentData(Root, new ShieldControl { RequestedOn = 1 }); Em.AddBuffer<ShieldRipple>(Root);
                Em.AddComponentData(Root, new ShipStats { BoundsMin = 0, BoundsMax = 2, BoundsCenter = 1, FunctionalRevision = 1 });
                Em.AddComponentData(Root, new ShipPower { Stored = 100, Capacity = 100, Flags = ShipPowerFlags.Initialized });
                Em.AddComponentData(Root, new PowerStats());
                Emitter = Em.CreateEntity(typeof(FunctionalBlock));
                int index = Em.GetBuffer<BlockInstance>(Root).Length;
                Em.SetComponentData(Emitter, new FunctionalBlock { Grid = Root, InstanceIndex = index });
                ushort block = modules.Catalog.GetBlockIndex(modules.Catalog.Get<BlockDefinition>(new ContentId("block_test_shield")));
                Em.GetBuffer<BlockInstance>(Root).Add(new BlockInstance { Block = block, Functional = Emitter });
                Em.AddBuffer<PowerRequest>(Emitter).Add(new PowerRequest { Module = BlockModuleId.ShieldEmitter, Draw = 8, Priority = 200, Requesting = 1 });
                Em.AddBuffer<PowerConsumer>(Root).Add(new PowerConsumer { Functional = Emitter, InstanceIndex = index,
                    Module = BlockModuleId.ShieldEmitter, Draw = 8, Priority = 200, Flags = PowerConsumerFlags.Alive | PowerConsumerFlags.Granted });
                Step(0);
            }
            public void Step(uint tick)
            {
                Em.SetComponentData(Clock, new SimClock { Tick = tick }); Owner.Update(Grid.World.Unmanaged);
                Shield.Update(Grid.World.Unmanaged); Em.CompleteAllTrackedJobs();
                ref var state = ref Grid.World.Unmanaged.ResolveSystemStateRef(Shield);
                using var captured = new NativeList<DamageEvent>(Allocator.TempJob);
                var set = Damage.Damage.BeginRead(ref state);
                var h = SimEventGather.Schedule(set, captured, state.Dependency); Damage.Damage.EndRead(h); h.Complete();
                Overflow.Clear(); foreach (var e in captured) Overflow.Add(e);
            }
            public void Hit(float amount, uint sequence = 0, bool blast = false) => Hits.Hits.PostFromMainThread(MakeHit(amount, sequence, blast));
            ShieldHit MakeHit(float amount, uint sequence, bool blast)
            {
                var s = State;
                return new ShieldHit { Grid = Root, LocalPoint = s.Center - new float3(s.Radii.x, 0, 0), LocalDir = new float3(1,0,0),
                    Amount = amount, Type = DamageType.Energy, Producer = ProducerKind.Projectile, Sequence = sequence,
                    Penetration = 3, Flags = blast ? ShieldHitFlags.Blast : 0, Radius = blast ? 2 : 0, WorldImpulse = new float3(4,0,0), Instigator = Emitter };
            }
            public void Stream(int sequence)
            {
                ref var state = ref Grid.World.Unmanaged.ResolveSystemStateRef(Shield);
                var writer = Hits.Hits.BeginWrite(ref state, ProducerKind.Projectile, 1);
                writer.BeginForEachIndex(0); writer.Write(MakeHit(260 + sequence * 10, (uint)sequence, false)); writer.EndForEachIndex();
                Hits.Hits.EndWrite(default);
            }
            public void Dispose() { Grid.Dispose(); modules.Dispose(); if (table.IsCreated) table.Dispose(); }
        }
        [Test] public void Init_FillsAndFits()
        {
            using var f = new Fixture(); var s = f.State;
            Assert.AreEqual(3000, s.MaxHp); Assert.AreEqual(3000, s.Hp); Assert.AreEqual(1, s.Active);
            Assert.IsTrue(math.all(s.Radii >= 3));
            // Include a large cube whose corners would escape the uncorrected padded ellipsoid.
            f.Em.SetComponentData(f.Root, new ShipStats { BoundsMin = -100, BoundsMax = 100, BoundsCenter = 0, FunctionalRevision = 2 }); f.Step(1); s = f.State;
            for (int i = 0; i < 8; i++)
            {
                var corner = new float3((i & 1) == 0 ? -100 : 100, (i & 2) == 0 ? -100 : 100, (i & 4) == 0 ? -100 : 100);
                Assert.LessOrEqual(math.length((corner - s.Center) / s.Radii), 1.000001f);
            }
        }
        [Test] public void Hits_CollapseAndOverflow()
        {
            using var f = new Fixture(); for (uint i = 0; i < 10; i++) f.Hit(300, i); f.Step(1);
            Assert.AreEqual(0, f.State.Hp); Assert.AreEqual(0, f.State.Active); Assert.AreEqual(2, f.Overflow.Count);
            foreach (var e in f.Overflow) { Assert.AreEqual(300, e.Amount); Assert.AreEqual(f.Emitter, e.Instigator); Assert.AreEqual(ProducerKind.ShieldOverflow, e.Producer); }
        }
        [Test] public void Overflow_HitsHullCell()
        {
            using var f = new Fixture(); f.Hit(3000); f.Step(1); Assert.AreEqual(1, f.Overflow.Count);
            var p = f.Overflow[0].LocalPoint; Assert.That(p.x, Is.InRange(0, .5f));
            using var writer = f.Grid.Writer(f.Root);
            Assert.IsTrue(writer.Access.TryGetCell((int3)math.floor(p / .5f), out var cell, out _, out _)); Assert.IsFalse(cell.IsAir);
        }
        [Test] public void Regen_AfterDelay()
        {
            using var f = new Fixture(); f.Hit(80); f.Step(10); Assert.AreEqual(2900, f.State.Hp); Assert.AreEqual(250u, f.State.RegenResumeTick);
            f.Step(249); Assert.AreEqual(2900, f.State.Hp); f.Step(250); Assert.AreEqual(2902, f.State.Hp);
        }
        [Test] public void Restart_AtQuarter()
        {
            using var f = new Fixture(); f.Hit(2400); f.Step(1);
            for (uint t = 241; t < 615; t++) f.Step(t);
            Assert.AreEqual(748, f.State.Hp); Assert.AreEqual(0, f.State.Active);
            f.Step(615); Assert.AreEqual(750, f.State.Hp); Assert.AreEqual(1, f.State.Active);
        }
        [Test] public void RippleRing_Wraps()
        {
            using var f = new Fixture(); for (uint i = 0; i < 10; i++) f.Hit(1, i); f.Step(0);
            Assert.AreEqual(2, f.State.RippleHead); var ring = f.Em.GetBuffer<ShieldRipple>(f.Root); Assert.AreEqual(8, ring.Length);
            foreach (var ripple in ring) Assert.AreEqual(1u, ripple.StartTick);
        }
        [Test] public void Order_IndependentOfStreamOrder()
        {
            ShieldState first; DamageEvent[] damage;
            using (var f = new Fixture())
            {
                for (int i = 0; i < 10; i++) f.Stream(i); f.Step(7); first = f.State; damage = f.Overflow.ToArray();
            }
            using (var f = new Fixture())
            {
                for (int i = 9; i >= 0; i--) f.Stream(i); f.Step(7);
                Assert.AreEqual(first, f.State); CollectionAssert.AreEqual(damage, f.Overflow);
            }
        }
        [Test] public void Hulk_AllOverflow()
        {
            using var f = new Fixture(); f.Em.AddComponent<Hulk>(f.Root); f.Hit(300); f.Hit(200, 1, true); f.Step(1);
            Assert.AreEqual(0, f.State.Hp); Assert.AreEqual(0, f.State.Active); Assert.AreEqual(2, f.Overflow.Count);
            Assert.AreEqual(300, f.Overflow[0].Amount); Assert.AreEqual(200, f.Overflow[1].Amount); Assert.AreEqual(2, f.Overflow[1].Radius);
        }
        [Test] public void ToggleOff_NoDrawNoBlock()
        {
            using var f = new Fixture(); f.Em.SetComponentData(f.Root, new ShieldControl());
            f.Demand.Update(f.Grid.World.Unmanaged); f.Allocation.Update(f.Grid.World.Unmanaged); f.Em.CompleteAllTrackedJobs();
            Assert.AreEqual(0, f.Em.GetBuffer<PowerRequest>(f.Emitter)[0].Requesting);
            Assert.AreEqual(0, f.Em.GetComponentData<ShipPower>(f.Root).GrantedPerSecond);
            f.Hit(300); f.Step(1); Assert.AreEqual(3000, f.State.Hp); Assert.AreEqual(0, f.State.Active); Assert.AreEqual(300, f.Overflow[0].Amount);
        }
    }
}
