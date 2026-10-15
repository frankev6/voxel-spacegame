using System;
using Game.Core;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Game.Voxel.PresentationStreams;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Voxel.Damage
{
    /// <summary>DMG-013 required acceptance: death transition, bounded dissolve and final ECB ownership.</summary>
    public unsafe sealed class ShipDeathTests
    {
        sealed class Fixture : IDisposable
        {
            public readonly TestGridFactory F = new TestGridFactory();
            public EntityManager Em => F.EM;
            public DamageStreams Streams;
            public readonly Entity Clock;
            readonly SystemHandle death, destroy;
            readonly GameplayEndCommandBufferSystem end;
            public Fixture()
            {
                F.World.GetOrCreateSystem<DamageStreamsSystem>();
                F.World.GetOrCreateSystem<FxEventQueueCreateSystem>();
                F.World.GetOrCreateSystem<SfxEventQueueCreateSystem>();
                Clock = Em.CreateSingleton(new SimClock { Tick = 10 });
                end = F.World.GetOrCreateSystemManaged<GameplayEndCommandBufferSystem>();
                death = F.World.GetOrCreateSystem<ShipDeathSystem>();
                destroy = F.World.GetOrCreateSystem<StructuralDestroySystem>();   // ADR-0117: the final destroy pass runs after it
                using var q = Em.CreateEntityQuery(typeof(DamageStreams)); Streams = q.GetSingleton<DamageStreams>();
            }
            public Entity Request(Entity root, GridDissolveMode mode)
            { var e = Em.CreateEntity(); Em.AddComponentData(e, new GridDissolveRequest { Grid = root, Mode = mode }); return e; }
            public void Step()
            {
                death.Update(F.World.Unmanaged); destroy.Update(F.World.Unmanaged); Em.CompleteAllTrackedJobs(); Streams.Data->AllJobs(default).Complete(); end.Update();
                var clock = Em.GetComponentData<SimClock>(Clock); clock.Tick++; Em.SetComponentData(Clock, clock);
            }
            public int DrainFx()
            {
                using var q = Em.CreateEntityQuery(typeof(FxEventQueue)); var fx = q.GetSingleton<FxEventQueue>();
                int count = 0; while (fx.Queue.TryDequeue(out var item)) { Assert.AreEqual(Game.Data.Definitions.FxKind.Debris, item.Kind); count++; }
                return count;
            }
            public int DrainExplosions()
            {
                ref var state = ref F.World.Unmanaged.ResolveSystemStateRef(death);
                var set = Streams.Explosions.BeginRead(ref state); state.CompleteDependency(); int count = 0;
                for (int i = 0; i < set.Count; i++)
                {
                    var reader = set.Reader(i);
                    for (int j = 0; j < set.ForEachCounts[i]; j++)
                    {
                        int n = reader.BeginForEachIndex(j);
                        for (int k = 0; k < n; k++) { var e = reader.Read<ExplosionRequest>(); Assert.Greater(e.Amount, 0); count++; }
                        reader.EndForEachIndex();
                    }
                }
                Streams.Explosions.EndRead(default); return count;
            }
            public Entity[] Members(Entity root)
            {
                var group = Em.GetBuffer<LinkedEntityGroup>(root); var members = new Entity[group.Length];
                for (int i = 0; i < group.Length; i++) members[i] = group[i].Value;
                return members;
            }
            public void Dispose() { Em.CompleteAllTrackedJobs(); Streams.Data->AllJobs(default).Complete(); F.Dispose(); }
        }
        [Test] public void CoreLost_BecomesHulk_NothingDeleted()
        {
            using var f = new Fixture(); var root = f.F.Spawn(f.F.CoreAndReactor()); var members = f.Members(root);
            var original = f.Em.GetComponentData<VoxelGrid>(root);
            using (var q = f.Em.CreateEntityQuery(typeof(DamageConfig)))
            { var config = q.GetSingleton<DamageConfig>(); config.CueCoreLost = new ContentId("sfx_core_lost").Hash; f.Em.SetComponentData(q.GetSingletonEntity(), config); }
            f.Streams.Data->GridResults.Add(new DamageGridResult { Grid = root, CoreLost = 1, CoreWorldPos = new float3(1,2,3) });
            f.Step(); Assert.IsTrue(f.Em.HasComponent<Hulk>(root)); Assert.AreEqual(GridKind.Hulk, f.Em.GetComponentData<VoxelGrid>(root).Kind);
            var info = f.Em.GetComponentData<HulkInfo>(root); Assert.AreEqual(HulkCause.CoreLost, info.Cause); Assert.AreEqual(original.Kind, info.FormerKind);
            foreach (var member in members) Assert.IsTrue(f.Em.Exists(member));
            Assert.AreEqual(1, f.Streams.Data->CoreLost.Length); Assert.AreEqual(root, f.Streams.Data->CoreLost[0].Grid);
            Assert.AreEqual(1, f.DrainExplosions());
            using (var q = f.Em.CreateEntityQuery(typeof(SfxEventQueue))) Assert.AreEqual(1, q.GetSingleton<SfxEventQueue>().Queue.Count);
            f.Step(); Assert.AreEqual(0, f.Streams.Data->CoreLost.Length); Assert.AreEqual(0, f.DrainExplosions());
        }
        [Test] public void Dissolve_EmitsThenDestroys()
        {
            using var f = new Fixture();
            // A one-cell sheet exposes every one of its 20,000 cells and spans multiple bricks/budgets.
            var root = f.F.Spawn(f.F.Box(0, new int3(199,0,99))); var members = f.Members(root);
            var request = f.Request(root, GridDissolveMode.Dissolve); int total = 0;
            for (int tick = 0; tick < 8 && f.Em.Exists(root); tick++)
            {
                f.Step(); int count = f.DrainFx(); Assert.LessOrEqual(count, 1500); total += count;
                if (f.Em.Exists(root))
                {
                    int cells = 0; foreach (var slot in f.Em.GetBuffer<BrickSlot>(root)) if (slot.Brick != Entity.Null) cells += f.Em.GetComponentData<Brick>(slot.Brick).SolidCount;
                    Assert.AreEqual(20000, cells, "Dissolve presentation does not remove physical cells.");
                }
            }
            Assert.Greater(total, 0); Assert.LessOrEqual(total, 12000); Assert.IsFalse(f.Em.Exists(request));
            foreach (var member in members) Assert.IsFalse(f.Em.Exists(member), "Root and every linked member must be gone.");
        }
        [Test] public void DestroyPass_DedupesAndChecksExistence()
        {
            using var f = new Fixture(); var root = f.F.Spawn(f.F.CoreAndReactor()); var members = f.Members(root);
            var stale = f.Em.CreateEntity(); f.Em.DestroyEntity(stale);
            f.Streams.Data->DestroyList.Add(root); f.Streams.Data->DestroyList.Add(root); f.Streams.Data->DestroyList.Add(stale);
            foreach (var member in members) { f.Streams.Data->DestroyList.Add(member); f.Streams.Data->DestroyList.Add(member); }
            var request = f.Request(root, GridDissolveMode.Silent);
            Assert.DoesNotThrow(f.Step);
            foreach (var member in members) Assert.IsFalse(f.Em.Exists(member)); Assert.IsFalse(f.Em.Exists(request));
        }
    }
}
