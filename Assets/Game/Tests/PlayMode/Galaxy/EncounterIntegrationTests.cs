using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Game.App.DevRuntime;
using Game.Core;
using Game.Core.Components;
using Game.Core.Diagnostics;
using Game.Core.Events;
using Game.Core.Flow;
using Game.Core.Save;
using Game.Data.Combat;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Galaxy.Bodies;
using Game.Galaxy.Components;
using Game.Galaxy.Data;
using Game.Galaxy.Encounters;
using Game.Galaxy.Services;
using Game.Galaxy.Travel;
using Game.Simulation;
using Game.Simulation.Ai;
using Game.Simulation.Commands;
using Game.Simulation.Factions;
using Game.Simulation.Power;
using Game.Simulation.Weapons;
using Game.Tests.Common;
using Game.Tests.Common.Perf;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Profiling;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;

namespace Game.Tests.PlayMode.Galaxy
{
    /// <summary>ENC-011: actual M3 content, production boot/streaming/spawning, fixed-step AI/weapons and ENCT round trip.</summary>
    public abstract class EncounterScenarioFixture : GameFlowFixture
    {
        protected ContentCatalog Catalog => Service<ContentService>().Catalog;
        protected EncounterDirectorService Director => Service<EncounterDirectorService>();
        protected EncounterDefinition Raiders => Catalog.Get<EncounterDefinition>(new ContentId("encounter_raiders"));
        protected SimClock Clock { get { using var q = Em.CreateEntityQuery(typeof(SimClock)); return q.GetSingleton<SimClock>(); } }
        protected double3 Origin { get { using var q = Em.CreateEntityQuery(typeof(FrameOrigin)); return q.GetSingleton<FrameOrigin>().OriginInStarSystem; } }
        protected GalaxyFrameContext Context(float seconds = 0) => new(Em, Clock.UniversalTime, Clock.Tick, seconds, Time.frameCount);

        // The production fixed group, including physics, advances exactly once per request.
        // Automatic fixed updates stay disabled while streaming/init frames are yielded.
        // Borrow/restore the group's time and rewindable allocator as Entities rate managers do.
        protected sealed unsafe class FixedTicks : IDisposable
        {
            sealed class Rate : IRateManager
            {
                internal int Remaining;
                double elapsed;
                bool pushed;
                DoubleRewindableAllocators* previous;
                public float Timestep { get; set; } = SimTime.FixedTimestep;
                internal Rate(double elapsed) { this.elapsed = elapsed; }
                internal void Restore(ComponentSystemGroup group)
                {
                    if (pushed)
                    { group.World.PopTime(); group.World.RestoreGroupAllocator(previous); pushed = false; }
                }
                public bool ShouldGroupUpdate(ComponentSystemGroup group)
                {
                    Restore(group);
                    if (Remaining == 0) return false;
                    Remaining--;
                    group.World.PushTime(new TimeData(elapsed, Timestep)); elapsed += Timestep;
                    previous = group.World.CurrentGroupAllocators; group.World.SetGroupAllocator(group.RateGroupAllocators);
                    pushed = true; return true;
                }
            }
            readonly World world;
            readonly FixedStepSimulationSystemGroup group;
            readonly IRateManager previous;
            readonly bool enabled;
            readonly Rate rate;
            readonly List<double> samples = new(512);
            internal FixedTicks(World world)
            {
                this.world = world; group = world.GetExistingSystemManaged<FixedStepSimulationSystemGroup>();
                previous = group.RateManager; enabled = group.Enabled; rate = new Rate(world.Time.ElapsedTime);
                group.Enabled = false; group.RateManager = rate;
            }
            internal void Step()
            {
                long start = Stopwatch.GetTimestamp();
                rate.Remaining = 1; group.Enabled = true;
                try { group.Update(); world.EntityManager.CompleteAllTrackedJobs(); }
                finally { rate.Restore(group); group.Enabled = false; }
                samples.Add((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
                EventHub.DrainAll();
            }
            internal void Report(string scenario)
            {
                var stat = PerfStat.From("FixedTick", "ms", samples.ToArray());
                Debug.Log($"[ENC-011] {scenario}: {stat.Samples} fixed ticks, median {stat.Median:F3} ms, p95 {stat.P95:F3} ms; last observed frame {Time.unscaledDeltaTime * 1000:F3} ms. Functional fixture; no reference-machine gate claim.");
            }
            public void Dispose()
            { world.EntityManager.CompleteAllTrackedJobs(); rate.Restore(group); group.RateManager = previous; group.Enabled = enabled; }
        }
        private protected IEnumerator StartIn(EncounterDirectorTests.Fields fields, string id, string bodyKey)
        {
            Director.Enabled = false;
            var galaxy = Service<GalaxyService>(); var definition = Catalog.Get<AuthoredStarSystemDefinition>(new ContentId(id));
            // M3 Whisper/Drift have no authored new-game start body. Only this fixture's start changes.
            fields.Set(galaxy.Definition, "startStarSystem", definition);
            fields.Set(definition, "startBodyKey", bodyKey);
            fields.Set(definition, "startOffsetM", new Vector3(0, 0, -400));
            yield return NewGame(42);
            Assert.AreEqual(StarSystemId.FromAuthored(definition.Id.Hash), Service<StarSystemLoader>().Loaded);
        }
        private protected void OneShip(EncounterDirectorTests.Fields fields, EncounterDefinition definition)
        {
            var ship = definition.Ships[0]; ship.countMin = ship.countMax = 1;
            fields.Set(definition, "ships", new List<EncounterShipEntry> { ship });
        }
        protected List<Entity> Members(ContentHash encounter)
        {
            var result = new List<Entity>();
            using var q = Em.CreateEntityQuery(typeof(EncounterTag)); using var roots = q.ToEntityArray(Allocator.Temp);
            foreach (var root in roots) if (Em.GetComponentData<EncounterTag>(root).Encounter == encounter) result.Add(root);
            return result;
        }
        protected bool Ready(Entity root)
            => Em.Exists(root) && Em.HasComponent<LocalTransform>(root) && Em.HasBuffer<BlockInstance>(root) &&
                Em.HasComponent<PhysicsCollider>(root) && Em.GetComponentData<PhysicsCollider>(root).IsValid &&
                Em.HasComponent<GridSpawning>(root) && !Em.IsComponentEnabled<GridSpawning>(root);
        protected IEnumerator Ready(List<Entity> roots)
        {
            yield return WaitUntil(() => { foreach (var root in roots) if (!Ready(root)) return false; return true; }, 10,
                "Real encounter grids become collidable");
        }
        protected Entity Station(BodyId body)
        {
            using var q = Em.CreateEntityQuery(typeof(BodyGridOwner)); using var roots = q.ToEntityArray(Allocator.Temp);
            foreach (var root in roots) if (Em.GetComponentData<BodyGridOwner>(root).Body.Equals(body)) return root;
            return Entity.Null;
        }
        protected BodyId Outpost()
        {
            Assert.IsTrue(Service<GalaxyService>().TryResolveBody(new ContentId("starsys_whisper"), "whisper_outpost", out var body));
            return body;
        }
        protected void Place(Entity root, float3 position, float yaw)
        {
            // Initial fixture placement goes through the real request before conversion, or initializes a ready pose.
            if (!Em.HasComponent<LocalTransform>(root))
            {
                var request = Em.GetComponentData<GridSpawnRequest>(root);
                request.Pose = new RigidTransform(quaternion.RotateY(yaw), position); Em.SetComponentData(root, request);
            }
            else Em.SetComponentData(root, LocalTransform.FromPositionRotation(position, quaternion.RotateY(yaw)));
            if (Em.HasComponent<UniversePosition>(root)) Em.SetComponentData(root, new UniversePosition { Value = Origin + (double3)position });
        }
        protected void Freeze(Entity root)
        {
            Assert.IsTrue(Ready(root));
            if (!Em.HasComponent<PhysicsVelocity>(root)) return; // The real outpost is static.
            Em.SetComponentData(root, default(PhysicsVelocity));
            if (Em.HasComponent<PhysicsMassOverride>(root)) Em.SetComponentData(root, new PhysicsMassOverride { IsKinematic = 1, SetVelocityToZero = 1 });
            else Em.AddComponentData(root, new PhysicsMassOverride { IsKinematic = 1, SetVelocityToZero = 1 });
        }
        protected void EmptyShield(Entity root)
        {
            if (!Em.HasComponent<ShieldState>(root)) return;
            var shield = Em.GetComponentData<ShieldState>(root);
            shield.Hp = 0; shield.Active = 0; shield.RegenResumeTick = uint.MaxValue;
            shield.Flags = (shield.Flags & ~ShieldFlags.FillOnNextUpdate) | ShieldFlags.Collapsed;
            Em.SetComponentData(root, shield); // Initial weak-target fixture charge; subsequent damage is real weapon damage.
        }
        protected void FireRate(Entity root, float multiplier)
        {
            using var q = Em.CreateEntityQuery(typeof(StatusChannels));
            q.GetSingleton<StatusChannels>().StatModifiers.PostFromMainThread(new StatModifierRequest
            { Root = root, Stat = StatId.FireRate, Producer = ProducerKind.Test, SourceId = 11, Multiplier = multiplier, UntilTick = uint.MaxValue, Sequence = 1 });
        }
        protected uint Shots(Entity root)
        {
            if (!Em.Exists(root) || !Em.HasBuffer<BlockInstance>(root)) return 0;
            uint shots = 0;
            foreach (var instance in Em.GetBuffer<BlockInstance>(root))
                if (instance.Functional != Entity.Null && Em.Exists(instance.Functional) && Em.HasComponent<WeaponState>(instance.Functional))
                    shots += Em.GetComponentData<WeaponState>(instance.Functional).ShotCounter;
            return shots;
        }
        protected float3 Center(Entity root)
        {
            var grid = Em.GetComponentData<VoxelGrid>(root);
            return Em.GetComponentData<LocalTransform>(root).TransformPoint(((float3)grid.CellMin + (float3)(grid.CellMax + 1)) * (.5f * grid.CellSize));
        }
        protected void PlayerFire(Entity target, bool firing)
        {
            using var q = Em.CreateEntityQuery(typeof(PlayerCommand));
            q.SetSingleton(new PlayerCommand { AimWorld = Center(target), AimValid = firing ? (byte)1 : (byte)0, FireGroups = firing ? (byte)7 : (byte)0 });
        }
        protected bool DamagedBy(Entity target, Entity attacker, float before)
        {
            if (!Em.Exists(target) || !Em.HasComponent<GridDamageStats>(target)) return false;
            var damage = Em.GetComponentData<GridDamageStats>(target);
            return damage.DamageTaken > before && damage.LastDamageInstigator == attacker;
        }
        protected void Until(FixedTicks ticks, Func<bool> completed, int limit, string what)
        {
            for (int i = 0; i < limit && !completed(); i++)
            {
                uint before = Clock.Tick; ticks.Step(); Assert.AreEqual(before + 1, Clock.Tick, "One requested real fixed tick.");
                Director.Tick(Context());
            }
            Assert.IsTrue(completed(), what + "; bounded at " + limit + " actual fixed ticks");
        }
        protected IEnumerator Jump(StarSystemId target, FixedTicks ticks)
        {
            var travel = Service<TravelService>(); var result = travel.RequestJump(target, JumpSource.Narrative);
            Assert.IsTrue(result.Accepted, result.Reason);
            for (int i = 0; i <= travel.Config.SpoolTicks && travel.Phase == TravelPhase.Spooling; i++)
            { ticks.Step(); travel.Tick(Context()); }
            Assert.AreEqual(TravelPhase.Transit, travel.Phase);
            yield return WaitUntil(() =>
            {
                // Loading yields real init/streaming frames; only active spool/arrival advances fixed time.
                if (travel.Phase == TravelPhase.Arriving)
                    for (int i = 0; i <= travel.Config.ArrivalTicks && travel.Phase == TravelPhase.Arriving; i++) { ticks.Step(); travel.Tick(Context()); }
                else travel.Tick(Context(.1f));
                return travel.Phase == TravelPhase.Idle && Service<StarSystemLoader>().IsReady;
            }, 10, "Real jump finishes streaming and arrival");
            Director.Tick(Context()); EventHub.DrainAll();
            Assert.AreEqual(target, Service<StarSystemLoader>().Loaded);
        }
        protected IEnumerator Flow(Awaitable<FlowResult> operation, string what)
        {
            var awaiter = operation.GetAwaiter(); yield return WaitUntil(() => awaiter.IsCompleted, 10, what);
            Assert.AreEqual(FlowResult.Ok, awaiter.GetResult(), what);
        }

        protected EncounterInstanceInfo Instance(Entity root)
        {
            Assert.IsTrue(Director.TryGetInstanceByMember(MemberId(root), out var info)); return info;
        }
        protected ulong MemberId(Entity root) => Em.HasComponent<PersistentId>(root)
            ? Em.GetComponentData<PersistentId>(root).Value : Em.GetComponentData<GridSpawnRequest>(root).PersistentId;
    }
    public sealed class EncounterIntegrationTests : EncounterScenarioFixture
    {
        [UnityTest] public IEnumerator Raiders_Engage()
        {
            using var fields = new EncounterDirectorTests.Fields(); OneShip(fields, Raiders);
            fields.Set(Director.Config, "firstRollDelaySeconds", .125f); fields.Set(Director.Config, "dangerSpawnChance", new float[] { 1, 1, 1, 1 });
            fields.Set(Director.Config, "minPlayerDistanceM", 100f);
            fields.Set(Raiders, "spawnDistanceMinM", 300f); fields.Set(Raiders, "spawnDistanceMaxM", 300f);
            foreach (var def in Catalog.All<EncounterDefinition>()) fields.Set(def, "directorWeight", def == Raiders ? 1f : 0f);
            yield return StartIn(fields, "starsys_drift", "drift_derelict");
            using var ticks = new FixedTicks(World.DefaultGameObjectInjectionWorld);
            Director.Enabled = true; Director.Tick(new GalaxyFrameContext(Em, Clock.UniversalTime + Director.Config.FirstRollDelaySeconds, Clock.Tick, 0, 0)); Director.Enabled = false;
            Assert.AreEqual(1, Director.ActiveInstanceCount);
            var roots = Members(Raiders.Id.Hash); Assert.AreEqual(1, roots.Count);
            var player = PlayerRoot; var pose = Em.GetComponentData<LocalTransform>(player);
            // Clear the nearby neutral derelict's hull so real friendly-fire avoidance can permit this shot.
            pose.Position += new float3(2000, 0, 0); Place(player, pose.Position, 0);
            Place(roots[0], pose.Position + new float3(0, 0, 100), math.PI);
            yield return Ready(roots); Freeze(player); Freeze(roots[0]); ticks.Step(); ticks.Step();
            EmptyShield(player); FireRate(roots[0], 4);
            uint shots = Shots(roots[0]); float damage = Em.GetComponentData<GridDamageStats>(player).DamageTaken;
            Until(ticks, () => Em.GetComponentData<AiBrain>(roots[0]).State == AiState.Engage &&
                Em.GetComponentData<ShipTarget>(roots[0]).Value == player && Shots(roots[0]) > shots && DamagedBy(player, roots[0], damage), 180,
                "A real director raider targets, fires at and damages the player");
            Assert.AreEqual(EncounterSource.Director, Instance(roots[0]).Source); ticks.Report(nameof(Raiders_Engage));
        }
        [UnityTest] public IEnumerator StationDefends()
        {
            using var fields = new EncounterDirectorTests.Fields(); OneShip(fields, Raiders);
            yield return StartIn(fields, "starsys_whisper", "whisper_outpost");
            using var ticks = new FixedTicks(World.DefaultGameObjectInjectionWorld);
            Entity station = Entity.Null; var body = Outpost();
            yield return WaitUntil(() => { station = Station(body); return station != Entity.Null && Ready(station); }, 10, "Real whisper_outpost grid streams");
            var factions = Service<FactionService>(); byte vestari = factions.Indices.IndexOf(Catalog.Get<FactionDefinition>(new ContentId("faction_vestari")));
            Assert.AreEqual(vestari, Em.GetComponentData<Allegiance>(station).Faction);
            Assert.AreEqual(FactionStance.Hostile, factions.GetStance(vestari, factions.Indices.IndexOf(Raiders.Faction)));
            var command = Service<DevRuntimeHost>().Commands.Execute("encounter spawn=encounter_raiders count=1 anchor=station", Service<DevRuntimeHost>().Context);
            Assert.AreEqual(DevResultKind.Ok, command.Kind, command.Text);
            var roots = Members(Raiders.Id.Hash); Assert.AreEqual(1, roots.Count);
            var pose = Em.GetComponentData<LocalTransform>(station);
            Place(roots[0], pose.Position + new float3(0, 0, 100), math.PI); yield return Ready(roots);
            Freeze(roots[0]); Freeze(PlayerRoot); ticks.Step(); ticks.Step(); EmptyShield(roots[0]); FireRate(station, 4);
            Assert.Greater(Em.GetComponentData<WeaponStats>(station).MaxWeaponRange, 0, "The streamed outpost has real turrets.");
            uint shots = Shots(station); float damage = Em.GetComponentData<GridDamageStats>(roots[0]).DamageTaken;
            Until(ticks, () => Em.GetComponentData<ShipTarget>(station).Value == roots[0] && Shots(station) > shots && DamagedBy(roots[0], station, damage),
                180, "Vestari outpost turrets select, fire at and damage the real raider");
            ticks.Report(nameof(StationDefends));
        }
        [UnityTest] public IEnumerator Provocation_StationRetaliates()
        {
            using var fields = new EncounterDirectorTests.Fields(); yield return StartIn(fields, "starsys_whisper", "whisper_outpost");
            using var ticks = new FixedTicks(World.DefaultGameObjectInjectionWorld);
            Entity station = Entity.Null; var body = Outpost();
            yield return WaitUntil(() => { station = Station(body); return station != Entity.Null && Ready(station); }, 10, "Provocation outpost streams");
            var player = PlayerRoot; var pose = Em.GetComponentData<LocalTransform>(station);
            Place(player, pose.Position + new float3(0, 0, 100), math.PI); Freeze(player); EmptyShield(station); EmptyShield(player);
            var factions = Service<FactionService>(); var vestari = Catalog.Get<FactionDefinition>(new ContentId("faction_vestari")); byte index = factions.Indices.IndexOf(vestari);
            Assert.AreEqual(0, factions.GetRelation(vestari)); Assert.AreEqual(FactionStance.Neutral, factions.GetStance(0, index));
            using var stateQuery = Em.CreateEntityQuery(typeof(FactionState)); var original = stateQuery.GetSingleton<FactionState>();
            var fast = original; fast.ProvokeIntervalTicks = 1; stateQuery.SetSingleton(fast);
            try
            {
                // Only the incidental cooldown is shortened. Three actual player hits still cause -10 each and cross -25.
                FireRate(player, 4); FireRate(station, 4); PlayerFire(station, true);
                float before = Em.GetComponentData<GridDamageStats>(station).DamageTaken;
                Until(ticks, () => factions.GetStance(0, index) == FactionStance.Hostile, 180, "Actual player fire provokes Vestari through the fixed-step writer");
                PlayerFire(station, false);
                Assert.IsTrue(DamagedBy(station, player, before)); Assert.LessOrEqual(factions.GetRelation(vestari), -30);
                Assert.AreEqual(FactionStance.Hostile, factions.GetStance(index, 0));
                uint shots = Shots(station); float damage = Em.GetComponentData<GridDamageStats>(player).DamageTaken;
                Until(ticks, () => Em.GetComponentData<ShipTarget>(station).Value == player && Shots(station) > shots && DamagedBy(player, station, damage),
                    180, "The provoked outpost's real turret AI fires back and damages the player");
            }
            finally
            {
                PlayerFire(station, false); var state = stateQuery.GetSingleton<FactionState>(); state.ProvokeIntervalTicks = original.ProvokeIntervalTicks; stateQuery.SetSingleton(state);
            }
            ticks.Report(nameof(Provocation_StationRetaliates));
        }
        [UnityTest, Ignore("TODO(ENC-011): QST-008 production dialog/beat and world-event composition is not integrated.")]
        public IEnumerator StartCombat_DefeatSetsFact()
        {
            // TODO(ENC-011): after QST-008 composes the accepted DLG-007 host and BeatService, run
            // start_combat encounter_whisper_hunter from a compiled test dialog through that actual host.
            // Check one spawn at 300–600 m, destroy its core with real weapons,
            // then observe EncounterDefeated and the live bridge's defeated_encounter_whisper_hunter counter.
            // A direct adapter/effect invocation or a locally installed no-op beat bridge cannot satisfy this scenario.
            yield break;
        }
        [UnityTest] public IEnumerator PendingStory_SurvivesSaveLoad()
        {
            yield return NewGame(42); Director.Enabled = false;
            var galaxy = Service<GalaxyService>(); var start = Service<StarSystemLoader>().Loaded;
            var away = StarSystemId.FromAuthored(new ContentId("starsys_whisper").Hash);
            var story = Catalog.Get<EncounterDefinition>(new ContentId("encounter_whisper_hunter"));
            uint id; ulong previousMember;
            using (var ticks = new FixedTicks(World.DefaultGameObjectInjectionWorld))
            {
                Assert.AreEqual(1, Director.SpawnEncounter(story, EncounterAnchor.Player(), 1, EncounterSource.Narrative));
                var roots = Members(story.Id.Hash); Assert.AreEqual(1, roots.Count);
                id = Instance(roots[0]).Id; previousMember = MemberId(roots[0]);
                // Real unload/entry makes the old-system story pending; this is not an active-only capture fixture.
                yield return Jump(away, ticks); Assert.AreEqual(0, Director.ActiveInstanceCount); Assert.IsFalse(Director.TryGetInstance(id, out _));
                Assert.IsFalse(Service<IShipSpawner>().TryGetRoot(previousMember, out _));
                ticks.Report(nameof(PendingStory_SurvivesSaveLoad) + ".outbound");
            }
            var flow = Service<IGameFlow>();
            yield return Flow(flow.SaveGameAsync("manual_001", SaveReason.Manual), "Pending story saves through the actual ENCT participant");
            yield return Flow(flow.LoadGameAsync("manual_001"), "Actual session loads the pending story");
            yield return WaitUntil(() => PlayerReady, 10, "Loaded player is playable");
            Assert.AreEqual(away, galaxy.State.CurrentStarSystem); Assert.AreEqual(0, Director.ActiveInstanceCount);
            using (var ticks = new FixedTicks(World.DefaultGameObjectInjectionWorld))
            {
                yield return Jump(start, ticks);
                yield return WaitUntil(() => { Director.Tick(Context()); return Director.TryGetInstance(id, out _); }, 10, "Saved pending story respawns on actual return");
                var roots = Members(story.Id.Hash); Assert.AreEqual(1, roots.Count); yield return Ready(roots);
                Assert.IsTrue(Director.TryGetInstance(id, out var restored)); Assert.AreSame(story, restored.Definition);
                Assert.AreEqual(start, restored.StarSystem); Assert.IsTrue(restored.Persistent); Assert.AreEqual(1, restored.AliveMembers);
                Assert.AreNotEqual(previousMember, Em.GetComponentData<PersistentId>(roots[0]).Value, "Same instance, newly allocated real ship.");
                Director.Tick(Context()); Director.Tick(Context()); Assert.AreEqual(1, Director.ActiveInstanceCount);
                ticks.Report(nameof(PendingStory_SurvivesSaveLoad) + ".return");
            }
        }
    }
    public sealed class EncounterBattlePerformanceTests : EncounterScenarioFixture
    {
        [UnityTest, Category("Performance")] public IEnumerator ThirtyShips_CombatGate()
        {
            // Advisory, opt-in only: actual render frames after real M3 spawns, 60 warm-up + 300 measured frames.
            using var fields = new EncounterDirectorTests.Fields();
            var xeno = Catalog.Get<EncounterDefinition>(new ContentId("encounter_xeno_probe_tutorial"));
            var entry = Raiders.Ships[0]; entry.countMin = entry.countMax = 3; fields.Set(Raiders, "ships", new List<EncounterShipEntry> { entry });
            yield return NewGame(42); Director.Enabled = false;
            List<Entity> raiders, probes;
            using (var ticks = new FixedTicks(World.DefaultGameObjectInjectionWorld))
            {
                Assert.AreEqual(5, Director.SpawnEncounter(Raiders, EncounterAnchor.Player(), 5, EncounterSource.Director));
                Assert.AreEqual(5, Director.SpawnEncounter(xeno, EncounterAnchor.Player(), 5, EncounterSource.Narrative));
                var factions = Service<FactionService>();
                Assert.AreEqual(FactionStance.Hostile, factions.GetStance(factions.Indices.IndexOf(Raiders.Faction), factions.Indices.IndexOf(xeno.Faction)));
                Assert.AreEqual(30, Director.AliveShipCount); raiders = Members(Raiders.Id.Hash); probes = Members(xeno.Id.Hash);
                Assert.AreEqual(15, raiders.Count); Assert.AreEqual(15, probes.Count);
                float3 center = Em.GetComponentData<LocalTransform>(PlayerRoot).Position;
                for (int i = 0; i < 15; i++)
                { Place(raiders[i], center + new float3(-150, 0, i * 40), math.PI / 2); Place(probes[i], center + new float3(150, 0, i * 40), -math.PI / 2); }
                yield return Ready(raiders); yield return Ready(probes);
                Place(PlayerRoot, center + new float3(0, 0, -4000), 0); Freeze(PlayerRoot);
                Until(ticks, () => AllFired(raiders) && AllFired(probes), 180, "All thirty real hostile encounter ships engage and fire");
            }
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Inconclusive("ThirtyShips reference combat gate needs a rendering device; fixture authored, measurement deferred.");
            var previousLeakMode = NativeLeakDetection.Mode; NativeLeakDetection.Mode = NativeLeakDetectionMode.Disabled;
            try
            {
                PerfSettings.AssertReference();
                using var main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
                if (!main.Valid) Assert.Inconclusive("Reference Main Thread profiler counter unavailable.");
                var run = new PerfRun(TestContext.CurrentContext.Test.FullName).FrameTime().MarkerPrefix("Ai")
                    .Custom("MainThread", () => main.LastValue * 1e-6).Custom("ShipsAlive", () => Director.AliveShipCount, "ships");
                int minimumAlive = Director.AliveShipCount;
                yield return run.RunFrames(() => minimumAlive = math.min(minimumAlive, Director.AliveShipCount));
                run.AddSamples("MinimumShipsAlive", new[] { (double)minimumAlive }, "ships"); run.Write("MainThread", 10, 14);
                Assert.Greater(run.Get("MainThread").Median, 0, "Measure the actual main thread.");
                run.AssertMedian("MainThread", 10); run.AssertP95("MainThread", 14); run.AssertMedian("Ai*", 1.5); run.AssertMedian("FrameTime", 16.6);
                // Natural casualties are recorded, not replenished or hidden behind immune/dummy ships.
                if (minimumAlive < 30) Assert.Inconclusive("ENC-011: natural casualties reduced the population below 30; timing/census written, steady thirty-ship gate remains unverified.");
            }
            finally { NativeLeakDetection.Mode = previousLeakMode; }
        }
        bool AllFired(List<Entity> roots)
        { foreach (var root in roots) if (Shots(root) == 0) return false; return roots.Count != 0; }
    }
}
