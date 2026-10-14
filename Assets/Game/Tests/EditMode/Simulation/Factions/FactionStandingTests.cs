using System;
using System.Collections.Generic;
using Game.Core.Components;
using Game.Core.Events;
using Game.Data.Components;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Simulation;
using Game.Simulation.Commands;
using Game.Simulation.Factions;
using Game.Tests.Common;
using Game.Tests.EditMode.Data;
using Game.Voxel.Damage;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Game.Tests.EditMode.Simulation.Factions
{
    public sealed class FactionStandingTests : GameTestBase
    {
        [TearDown] public void ContentCleanup() => TestContentFactory.DestroyAll();
        internal sealed class Fixture : IDisposable
        {
            internal readonly EcsTestWorld World = EcsTestWorld.Create(typeof(FactionStandingSystem));
            internal EntityManager Em => World.EntityManager;
            internal readonly Entity Singleton, Clock, Player;
            internal readonly ContentCatalog Catalog;
            internal readonly FactionDefinition Ark, Neutral, Locked;
            internal readonly NativeQueue<ManagedEventEnvelope<FactionRelationChanged>> Events;
            internal Fixture()
            {
                Ark = Make("faction_ark", true, true, 100); Neutral = Make("faction_neutral", false, false, 0); Locked = Make("faction_locked", false, true, -100);
                var config = TestContentFactory.Create<EncounterConfigDefinition>("config_encounters");
                Catalog = Build(Ark, Neutral, Locked, config);
                var created = new List<Entity>(); FactionContent.CreateSingletons(Em, Catalog, config, created); Singleton = created[0];
                World.AddGameplayActive(); Clock = World.AddSimClock(); Player = Em.CreateEntity(typeof(PlayerShip));
                Events = new NativeQueue<ManagedEventEnvelope<FactionRelationChanged>>(Allocator.Persistent);
                World.AddSingleton(new ManagedEventQueue<FactionRelationChanged> { Queue = Events });
            }
            internal static FactionDefinition Make(string id, bool player, bool locked, int relation) => TestContentFactory.Create<FactionDefinition>(id, so =>
            { TestContentFactory.Set(so, "isPlayerFaction", player); TestContentFactory.Set(so, "relationLocked", locked); TestContentFactory.Set(so, "basePlayerRelation", relation); });
            internal static ContentCatalog Build(params ContentDefinition[] definitions)
            {
                var all = new List<ContentDefinition>(definitions) { TestContentFactory.Create<BlockDefinition>("block_missing"), TestContentFactory.Create<ItemDefinition>("item_missing") };
                var report = new ValidationReport("Faction fixture"); var catalog = ContentCatalog.Build(all, report);
                Assert.AreEqual(0, report.ErrorCount, report.ToText()); return catalog;
            }
            internal byte Index => FactionIndexMap.Build(Catalog).IndexOf(Neutral);
            internal FactionStanding Standing(byte index) { World.CompleteAll(); return Em.GetBuffer<FactionStanding>(Singleton)[index]; }
            internal FactionRelations Matrix { get { World.CompleteAll(); return Em.GetComponentData<FactionRelations>(Singleton); } }
            internal void Request(byte index, int value, byte mode = 0) => Em.GetBuffer<FactionRelationRequest>(Singleton).Add(new FactionRelationRequest { Faction = index, Value = value, Mode = mode });
            internal void Tick(uint tick)
            { Em.SetComponentData(Clock, new SimClock { Tick = tick }); World.UpdateSystem<FactionStandingSystem>(); World.CompleteAll(); }
            internal Entity Damage(byte faction, uint tick, Entity source)
            {
                var e = Em.CreateEntity(typeof(Allegiance), typeof(GridDamageStats)); Em.SetComponentData(e, new Allegiance { Faction = faction });
                Em.SetComponentData(e, new GridDamageStats { LastDamageTick = tick, LastDamageInstigator = source }); return e;
            }
            public void Dispose()
            { World.CompleteAll(); Em.GetComponentData<FactionRelations>(Singleton).Dispose(); Events.Dispose(); World.Dispose(); }
        }
        [Test] public void IndexMap_Order()
        {
            using var f = new Fixture(); var before = FactionIndexMap.Build(f.Catalog);
            Assert.AreSame(f.Ark, before.ByIndex(0)); Assert.AreSame(f.Locked, before.ByIndex(1)); Assert.AreEqual(2, before.IndexOf(f.Neutral));
            var last = Fixture.Make("faction_zzz", false, false, 0);
            var after = FactionIndexMap.Build(Fixture.Build(last, f.Neutral, f.Ark, f.Locked));
            Assert.AreEqual(before.IndexOf(f.Neutral), after.IndexOf(f.Neutral)); Assert.AreEqual(3, after.IndexOf(last));
            Assert.AreEqual(255, before.IndexOf(last)); Assert.IsNull(before.ByIndex(255));
        }
        [Test] public void Boot_Matrix()
        {
            using var f = new Fixture(); var m = f.Matrix;
            for (byte i = 0; i < m.Count; i++) for (byte j = 0; j < m.Count; j++)
            { Assert.AreEqual(m.Get(i, j), m.Get(j, i)); if (i == j) Assert.AreEqual(FactionStance.Friendly, m.Get(i, j)); }
            Assert.AreEqual(FactionStance.Hostile, m.Get(0, 1)); Assert.AreEqual(FactionStance.Neutral, m.Get(0, 2));
            Assert.AreEqual(100, f.Standing(0).Relation); Assert.AreEqual(1, f.Standing(0).Locked);
        }
        [Test] public void Request_AppliedNextTick()
        {
            using var f = new Fixture(); f.Request(f.Index, 20); Assert.AreEqual(0, f.Standing(f.Index).Relation);
            f.Tick(1); Assert.AreEqual(20, f.Standing(f.Index).Relation); Assert.AreEqual(0, f.Em.GetBuffer<FactionRelationRequest>(f.Singleton).Length);
            Assert.AreEqual(1, f.Events.Count); Assert.IsTrue(f.Events.TryDequeue(out var changed)); Assert.AreEqual(20, changed.Payload.NewRelation);
            Assert.AreEqual(1u, f.Em.GetComponentData<FactionState>(f.Singleton).Revision);
        }
        [Test] public void StanceFlip_WritesBothCells()
        {
            using var f = new Fixture(); f.Request(f.Index, -25); f.Tick(1);
            Assert.AreEqual(FactionStance.Hostile, f.Matrix.Get(0, f.Index)); Assert.AreEqual(FactionStance.Hostile, f.Matrix.Get(f.Index, 0));
            f.Request(f.Index, 50, 1); f.Tick(2); Assert.AreEqual(FactionStance.Friendly, f.Matrix.Get(0, f.Index)); Assert.AreEqual(FactionStance.Friendly, f.Matrix.Get(f.Index, 0));
        }
        [Test] public void Provocation_RateLimited()
        {
            using var f = new Fixture(); var grid = f.Damage(f.Index, 0, f.Player); f.Tick(1); Assert.AreEqual(-10, f.Standing(f.Index).Relation);
            f.Em.SetComponentData(grid, new GridDamageStats { LastDamageTick = 1, LastDamageInstigator = f.Player }); f.Tick(2); Assert.AreEqual(-10, f.Standing(f.Index).Relation);
            f.Em.SetComponentData(grid, new GridDamageStats { LastDamageTick = 60, LastDamageInstigator = f.Player }); f.Tick(61); Assert.AreEqual(-20, f.Standing(f.Index).Relation);
            f.Request(f.Index, -100, 1); f.Tick(62);
            f.Em.SetComponentData(grid, new GridDamageStats { LastDamageTick = 121, LastDamageInstigator = f.Player }); f.Tick(122); Assert.AreEqual(-100, f.Standing(f.Index).Relation);
        }
        [Test] public void Locked_Ignored()
        { using var f = new Fixture(); f.Request(1, 80, 1); f.Damage(1, 0, f.Player); f.Tick(1); Assert.AreEqual(-100, f.Standing(1).Relation); Assert.AreEqual(0, f.Events.Count); }
        [Test] public void OtherSources_Ignored()
        { using var f = new Fixture(); f.Damage(f.Index, 0, f.Em.CreateEntity()); f.Damage(f.Index, 0, Entity.Null); f.Tick(1); Assert.AreEqual(0, f.Standing(f.Index).Relation); }
        [Test] public void NoSyncPoint()
        {
            using var f = new Fixture();
            var reader = f.World.World.GetOrCreateSystemManaged<MatrixReader>();
            using var result = new NativeArray<byte>(1, Allocator.TempJob); reader.Result = result;
            f.Request(f.Index, -100, 1); reader.Update();
            f.World.UpdateSystem<FactionStandingSystem>(); f.World.CompleteAll();
            Assert.AreEqual((byte)FactionStance.Neutral, result[0]); Assert.AreEqual(FactionStance.Hostile, f.Matrix.Get(0, f.Index));
        }
        [DisableAutoCreation] public partial class MatrixReader : SystemBase
        {
            internal NativeArray<byte> Result;
            protected override void OnUpdate() => Dependency = new ReadMatrix { Matrix = SystemAPI.GetSingleton<FactionRelations>(), Result = Result }.Schedule(Dependency);
            struct ReadMatrix : IJob
            { [ReadOnly] public FactionRelations Matrix; public NativeArray<byte> Result; public void Execute() { Result[0] = (byte)Matrix.Get(0, 2); } }
        }
    }
}
