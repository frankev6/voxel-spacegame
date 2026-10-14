using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Components;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Galaxy.Components;
using Game.Galaxy.Encounters;
using Game.Galaxy.Services;
using Game.Simulation;
using Game.Simulation.Ai;
using Game.Simulation.Factions;
using Game.Tests.Common;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.Galaxy
{
    /// <summary>ENC-007: actual boot, loader, faction and spawn owners; no simulated director facade.</summary>
    public sealed class EncounterDirectorTests : GameFlowFixture
    {
        EncounterDirectorService Director => Service<EncounterDirectorService>();
        EncounterDefinition Raiders => Service<ContentService>().Catalog.Get<EncounterDefinition>(new ContentId("encounter_raiders"));
        void Tick(double time) => Director.Tick(new GalaxyFrameContext(Em, time, 0, 0, 0));
        [UnityTest] public IEnumerator Spawn_TagsAndAllegiance()
        {
            yield return NewGame(42); Director.Enabled = false;
            Assert.AreEqual(1, Director.SpawnEncounter(Raiders, EncounterAnchor.Player(), 1, EncounterSource.Narrative));
            using var query = Em.CreateEntityQuery(typeof(EncounterTag));
            using var roots = query.ToEntityArray(Allocator.Temp);
            Assert.AreEqual(Director.AliveShipCount, roots.Length); Assert.Greater(roots.Length, 0);
            using var originQuery = Em.CreateEntityQuery(typeof(FrameOrigin));
            var origin = originQuery.GetSingleton<FrameOrigin>().OriginInStarSystem;
            var player = origin + (double3)Em.GetComponentData<Unity.Transforms.LocalTransform>(PlayerRoot).Position;
            uint instance = Em.GetComponentData<EncounterTag>(roots[0]).Instance;
            foreach (var root in roots)
            {
                var tag = Em.GetComponentData<EncounterTag>(root);
                Assert.AreEqual(instance, tag.Instance); Assert.AreEqual(Raiders.Id.Hash, tag.Encounter);
                Assert.AreEqual(Service<FactionService>().Indices.IndexOf(Raiders.Faction), Em.GetComponentData<Allegiance>(root).Faction);
                Assert.GreaterOrEqual(math.length((origin + (double3)Em.GetComponentData<GridSpawnRequest>(root).Pose.pos - player).xz), 800);
            }
            Assert.IsTrue(Director.TryGetInstance(instance, out var info)); Assert.AreEqual(roots.Length, info.AliveMembers);
        }
        [UnityTest] public IEnumerator Ambush_HeadsForPlayer()
        {
            yield return NewGame(42); Director.Enabled = false;
            Assert.AreEqual(1, Director.StartCombat(Raiders, EncounterAnchor.Player()));
            using var query = Em.CreateEntityQuery(typeof(EncounterTag), typeof(AiBrain));
            using var roots = query.ToEntityArray(Allocator.Temp);
            var player = Em.GetComponentData<Unity.Transforms.LocalTransform>(PlayerRoot).Position;
            Assert.Greater(roots.Length, 0);
            foreach (var root in roots)
            {
                var brain = Em.GetComponentData<AiBrain>(root);
                Assert.AreEqual(player, brain.Anchor); Assert.AreEqual(player, brain.GoalPosition);
                Assert.AreEqual(1, brain.Initialized);
            }
            // Sweep also handles not-yet-converted spawn requests without mistaking them for defeated members.
            Tick(1); Assert.AreEqual(roots.Length, Director.AliveShipCount);
        }
        [UnityTest] public IEnumerator Caps()
        {
            yield return NewGame(42); Director.Enabled = false;
            for (int i = 0; i < 5; i++) Director.SpawnEncounter(Raiders, EncounterAnchor.Player(), 10, EncounterSource.Director);
            Assert.LessOrEqual(Director.ActiveInstanceCount, 16); Assert.LessOrEqual(Director.AliveShipCount, 40);
            Assert.Greater(Director.AliveShipCount, 0);
            var instances = new List<EncounterInstanceInfo>(); Director.GetActiveInstances(instances);
            var ids = new HashSet<uint>(); foreach (var item in instances) Assert.IsTrue(ids.Add(item.Id));
        }
        internal sealed class Fields : System.IDisposable
        {
            readonly List<(object target, System.Reflection.FieldInfo field, object old)> values = new();
            internal void Set(object target, string name, object value)
            {
                var field = target.GetType().GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Assert.IsNotNull(field, name); values.Add((target, field, field.GetValue(target))); field.SetValue(target, value);
            }
            public void Dispose() { for (int i = values.Count - 1; i >= 0; i--) values[i].field.SetValue(values[i].target, values[i].old); }
        }
        double Now { get { using var q = Em.CreateEntityQuery(typeof(SimClock)); return q.GetSingleton<SimClock>().UniversalTime; } }
        uint ClockTick { get { using var q = Em.CreateEntityQuery(typeof(SimClock)); return q.GetSingleton<SimClock>().Tick; } }
        void RollFixture(Fields fields)
        {
            var galaxy = Service<GalaxyService>(); var loaded = Service<StarSystemLoader>().Loaded;
            Assert.IsTrue(galaxy.Galaxy.TryGetIndex(loaded, out int index));
            var system = galaxy.Galaxy.StarSystems[index]; system.Danger = 2; system.Flags &= ~Game.Galaxy.Data.StarSystemFlags.NoEncounters;
            galaxy.Galaxy.StarSystems[index] = system;
            fields.Set(Director.Config, "firstRollDelaySeconds", .125f); fields.Set(Director.Config, "rollIntervalSeconds", .25f);
            fields.Set(Director.Config, "dangerSpawnChance", new float[] { 1, 1, 1, 1 });
            foreach (var def in Service<ContentService>().Catalog.All<EncounterDefinition>()) fields.Set(def, "directorWeight", def == Raiders ? 1f : 0f);
            Game.Core.Events.EventBus<Game.Core.Events.StarSystemEntered>.Publish(new(loaded.Value, Game.Core.Events.StarSystemEntryReason.NewGame, 1));
            Game.Core.Events.EventBus<Game.Core.Events.StarSystemEntered>.Drain(); Director.Enabled = true;
        }
        [UnityTest] public IEnumerator Roll_Schedule()
        {
            yield return NewGame(42); using var fields = new Fields(); using var pause = HoldSimulation("director schedule"); RollFixture(fields);
            double start = Now; Tick(start + .124); Assert.AreEqual(0, Director.ActiveInstanceCount);
            Tick(start + .125); Assert.AreEqual(1, Director.ActiveInstanceCount);
            Tick(start + .25); Assert.AreEqual(1, Director.ActiveInstanceCount);
            Tick(start + .375); Assert.AreEqual(2, Director.ActiveInstanceCount);
        }
        [UnityTest] public IEnumerator Roll_Deterministic()
        {
            var results = new List<(uint id, ContentId definition, int count)>();
            for (int run = 0; run < 2; run++)
            {
                if (run != 0)
                {
                    var exit = Service<Game.Core.Flow.IGameFlow>().QuitToMainMenuAsync(false).GetAwaiter();
                    yield return WaitUntil(() => exit.IsCompleted, 10, "Reset deterministic visit"); Assert.AreEqual(Game.Core.Flow.FlowResult.Ok, exit.GetResult());
                }
                yield return NewGame(42); using var fields = new Fields(); using var pause = HoldSimulation("deterministic visit"); RollFixture(fields);
                double start = Now; Tick(start + .125); Tick(start + .375);
                var instances = new List<EncounterInstanceInfo>(); Director.GetActiveInstances(instances); Assert.AreEqual(2, instances.Count);
                for (int i = 0; i < instances.Count; i++)
                { var value = (instances[i].Id, instances[i].Definition.Id, instances[i].Members); if (run == 0) results.Add(value); else Assert.AreEqual(results[i], value); }
            }
        }
        [UnityTest] public IEnumerator Roll_Preconditions()
        {
            yield return NewGame(42); using var fields = new Fields(); RollFixture(fields); double due = Now + .125;
            var docking = Service<DockingService>(); var galaxy = Service<GalaxyService>(); var content = galaxy.GetContent(Service<StarSystemLoader>().Loaded);
            docking.Dock(content.Bodies[0].Id); Tick(due); Assert.AreEqual(0, Director.ActiveInstanceCount); docking.Undock();
            using (var q = Em.CreateEntityQuery(typeof(PlayerCombatStatus)))
            { var value = q.GetSingleton<PlayerCombatStatus>(); value.HostilesEngaging = 1; q.SetSingleton(value); Tick(due); Assert.AreEqual(0, Director.ActiveInstanceCount); value.HostilesEngaging = 0; q.SetSingleton(value); }
            var travel = Service<TravelService>(); Assert.IsTrue(travel.RequestJump(OtherSystem(), Game.Core.Events.JumpSource.Narrative).Accepted);
            Tick(due); Assert.AreEqual(0, Director.ActiveInstanceCount, "Spooling does not consume the scheduled roll.");
            yield return FinishJump();
        }
        [UnityTest] public IEnumerator Defeat_OnceWithLoot()
        {
            yield return NewGame(42); Director.Enabled = false; using var fields = new Fields();
            var catalog = Service<ContentService>().Catalog; var loot = catalog.Get<LootTableDefinition>(new ContentId("loot_base"));
            var item = catalog.Get<ItemDefinition>(new ContentId("resource_fuel"));
            fields.Set(loot, "entries", new List<LootEntry> { new LootEntry { item = item, min = 1, max = 1, chancePercent = 100 } });
            fields.Set(loot, "defaultPicks", 0); fields.Set(Raiders, "defeatLoot", loot);
            var economy = Service<Game.Economy.EconomyService>(); int before = economy.Inventory.CountOf(item.Id), events = 0;
            System.Action<Game.Core.Events.EncounterDefeated> handler = _ => events++;
            Game.Core.Events.EventBus<Game.Core.Events.EncounterDefeated>.Subscribe(handler);
            try
            {
                Assert.AreEqual(1, Director.SpawnEncounter(Raiders, EncounterAnchor.Player(), 1, EncounterSource.Narrative));
                using var query = Em.CreateEntityQuery(typeof(EncounterTag)); using var roots = query.ToEntityArray(Allocator.Temp);
                foreach (var root in roots) Em.AddComponent<Game.Voxel.Damage.Hulk>(root);
                Tick(Now); Game.Core.Events.EventBus<Game.Core.Events.EncounterDefeated>.Drain();
                Assert.AreEqual(1, events); Assert.AreEqual(before + 1, economy.Inventory.CountOf(item.Id)); Assert.AreEqual(0, Director.ActiveInstanceCount);
                Tick(Now + 1); Game.Core.Events.EventBus<Game.Core.Events.EncounterDefeated>.Drain();
                Assert.AreEqual(1, events); Assert.AreEqual(before + 1, economy.Inventory.CountOf(item.Id));
                foreach (var root in roots) Assert.IsTrue(Em.Exists(root), "Hulks remain for salvage.");
            }
            finally { Game.Core.Events.EventBus<Game.Core.Events.EncounterDefeated>.Unsubscribe(handler); }
        }
        [UnityTest] public IEnumerator Despawn_DirectorOnly()
        {
            yield return NewGame(42); Director.Enabled = false;
            Assert.AreEqual(1, Director.SpawnEncounter(Raiders, EncounterAnchor.Player(), 1, EncounterSource.Director));
            Assert.AreEqual(1, Director.SpawnEncounter(Raiders, EncounterAnchor.Player(), 1, EncounterSource.Narrative));
            using var query = Em.CreateEntityQuery(typeof(EncounterTag));
            yield return WaitUntil(() => { using var entities = query.ToEntityArray(Allocator.Temp); foreach (var e in entities) if (!Em.HasComponent<Unity.Transforms.LocalTransform>(e)) return false; return true; }, 10, "Encounter grids converted");
            using var pause = HoldSimulation("despawn interval");
            using (var roots = query.ToEntityArray(Allocator.Temp)) foreach (var root in roots)
            {
                var pose = Em.GetComponentData<Unity.Transforms.LocalTransform>(root); pose.Position = new float3(50000, 0, 50000); Em.SetComponentData(root, pose);
                var brain = Em.GetComponentData<AiBrain>(root); brain.State = AiState.Idle; Em.SetComponentData(root, brain);
            }
            Tick(100); Assert.AreEqual(2, Director.ActiveInstanceCount); Tick(129); Assert.AreEqual(2, Director.ActiveInstanceCount);
            Tick(130); Assert.AreEqual(1, Director.ActiveInstanceCount);
            var instances = new List<EncounterInstanceInfo>(); Director.GetActiveInstances(instances); Assert.AreEqual(EncounterSource.Narrative, instances[0].Source);
        }
        Game.Galaxy.Data.StarSystemId OtherSystem()
        {
            foreach (var system in Service<GalaxyService>().Galaxy.StarSystems)
                if (system.Id.IsAuthored && !system.Id.Equals(Service<StarSystemLoader>().Loaded)) return system.Id;
            throw new System.InvalidOperationException("Authored travel destination missing.");
        }
        IEnumerator FinishJump()
        {
            var travel = Service<TravelService>();
            uint tick = ClockTick + (uint)travel.Config.SpoolTicks + (uint)travel.Config.ArrivalTicks + 1;
            yield return WaitUntil(() => { travel.Tick(new GalaxyFrameContext(Em, Now, tick, .1f, 0)); return travel.Phase == Game.Galaxy.Travel.TravelPhase.Idle && Service<StarSystemLoader>().IsReady; }, 10, "Real travel/load/arrival completes");
        }
        [UnityTest] public IEnumerator Pending_AcrossJump()
        {
            yield return NewGame(42); Director.Enabled = false;
            var start = Service<StarSystemLoader>().Loaded;
            Assert.AreEqual(1, Director.SpawnEncounter(Raiders, EncounterAnchor.Player(), 1, EncounterSource.Narrative));
            var instances = new List<EncounterInstanceInfo>(); Director.GetActiveInstances(instances); uint id = instances[0].Id;
            Assert.IsTrue(Service<TravelService>().RequestJump(OtherSystem(), Game.Core.Events.JumpSource.Narrative).Accepted); yield return FinishJump();
            Tick(Now); Assert.AreEqual(0, Director.ActiveInstanceCount);
            Assert.IsTrue(Service<TravelService>().RequestJump(start, Game.Core.Events.JumpSource.Narrative).Accepted); yield return FinishJump();
            Tick(Now); Assert.IsTrue(Director.TryGetInstance(id, out var restored)); Assert.AreEqual(start, restored.StarSystem); Assert.Greater(restored.AliveMembers, 0);
        }
        [UnityTest] public IEnumerator Pending_DuringTransit()
        {
            yield return NewGame(42); Director.Enabled = false; var travel = Service<TravelService>(); var target = OtherSystem();
            Assert.IsTrue(travel.RequestJump(target, Game.Core.Events.JumpSource.Narrative).Accepted);
            travel.Tick(new GalaxyFrameContext(Em, Now, ClockTick + (uint)travel.Config.SpoolTicks, 0, 0));
            Assert.AreEqual(Game.Galaxy.Travel.TravelPhase.Transit, travel.Phase);
            Assert.AreEqual(0, Director.SpawnEncounter(Raiders, EncounterAnchor.Player(), 1, EncounterSource.Narrative));
            yield return FinishJump(); Tick(Now);
            var instances = new List<EncounterInstanceInfo>(); Director.GetActiveInstances(instances);
            Assert.AreEqual(1, instances.Count); Assert.AreEqual(target, instances[0].StarSystem);
        }
    }
    // Separate name filter keeps default live validation from dispatching advisory benchmarks.
    public sealed class EncounterDirectorPerfTests : GameFlowFixture
    {
        EncounterDirectorService Director => Service<EncounterDirectorService>();
        EncounterDefinition Raiders => Service<ContentService>().Catalog.Get<EncounterDefinition>(new ContentId("encounter_raiders"));
        void Tick(double time) => Director.Tick(new GalaxyFrameContext(Em, time, 0, 0, 0));
        [UnityTest, Category("Performance")] public IEnumerator Tick_Steady_NoAlloc()
        {
            yield return NewGame(42); Director.Enabled = false; using var fields = new EncounterDirectorTests.Fields();
            var entry = Raiders.Ships[0]; entry.countMin = entry.countMax = 2; fields.Set(Raiders, "ships", new List<EncounterShipEntry> { entry });
            Director.SpawnEncounter(Raiders, EncounterAnchor.Player(), 8, EncounterSource.Narrative);
            entry.countMin = entry.countMax = 3; fields.Set(Raiders, "ships", new List<EncounterShipEntry> { entry });
            Director.SpawnEncounter(Raiders, EncounterAnchor.Player(), 8, EncounterSource.Narrative);
            Assert.AreEqual(16, Director.ActiveInstanceCount); Assert.AreEqual(40, Director.AliveShipCount);
            for (int i = 0; i < 60; i++) Tick(0);
            var times = new double[300]; long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < times.Length; i++) { long start = System.Diagnostics.Stopwatch.GetTimestamp(); Tick(0); times[i] = (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency; }
            long bytes = System.GC.GetAllocatedBytesForCurrentThread() - before;
            Game.Tests.Common.Perf.PerfRun.Gate("EncounterDirector.Tick", times, .05);
            Game.Tests.Common.Perf.PerfRun.Gate("EncounterDirector.GC", new double[] { bytes / 300.0 }, 0, "B");
        }
        [UnityTest, Category("Performance")] public IEnumerator Spawn_TenShips_Time()
        {
            yield return NewGame(42); Director.Enabled = false; using var fields = new EncounterDirectorTests.Fields();
            var entry = Raiders.Ships[0]; entry.countMin = entry.countMax = 10; fields.Set(Raiders, "ships", new List<EncounterShipEntry> { entry });
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            int spawned = Director.SpawnEncounter(Raiders, EncounterAnchor.Player(), 1, EncounterSource.Narrative);
            double elapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            Assert.AreEqual(1, spawned); Assert.AreEqual(10, Director.AliveShipCount);
            Game.Tests.Common.Perf.PerfRun.Gate("EncounterDirector.SpawnTen", new[] { elapsed }, 1.5);
        }
    }
}
