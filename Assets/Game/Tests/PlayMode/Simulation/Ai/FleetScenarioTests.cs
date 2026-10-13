using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Game.Core;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Definitions;
using Game.Simulation;
using Game.Simulation.Ai;
using Game.Simulation.Commands;
using Game.Simulation.Flight;
using Game.Simulation.Power;
using Game.Simulation.Spatial;
using Game.Simulation.Weapons;
using Game.Tests.Common;
using Game.Tests.Common.Perf;
using Game.Tests.PlayMode.Simulation.Weapons;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.PerformanceTesting;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using Unity.Transforms;
using UnityEngine;
using ReferenceScope = Game.Tests.PlayMode.Voxel.Physics.Perf.PhysicsPerfScope;

namespace Game.Tests.PlayMode.Simulation.Ai
{
    /// <summary>AI-015: two explicitly selected combat measurements; direct ticks, no real-time combat waits or tuning.</summary>
    [Category("Scenario"), Category("Performance")]
    public sealed class FleetScenarioTests : GameTestBase
    {
        const int Warmup = 60, Samples = 300;
        static readonly ProfilerMarker AiStage = new ProfilerMarker(ProfilerCategory.Scripts, "FleetScenarioTests.AiStage");
        static readonly ProfilerMarker FixedTick = new ProfilerMarker(ProfilerCategory.Scripts, "FleetScenarioTests.FixedTick");

        [Test, Performance] public void TwoFleets_Under1_5ms() => Measure(false);
        [Test, Performance] public void Swarm_30Ships100Craft() => Measure(true);

        static void Measure(bool swarm)
        {
            using var reference = new ReferenceScope(); using var f = new Fixture(swarm);
            f.Ready();
            using var ai = new Cost("FleetScenarioTests.AiStage"); using var main = new Cost("FleetScenarioTests.FixedTick");
            var aiSamples = new double[Samples]; var mainSamples = new double[Samples];
            var alive = new double[Samples]; var formation = new double[Samples];
            double deadline = Time.realtimeSinceStartupAsDouble + 45;
            for (int tick = 0; tick < Warmup + Samples; tick++)
            {
                if (Time.realtimeSinceStartupAsDouble > deadline)
                {
                    f.WriteReadback(TestContext.CurrentContext.Test.Name);
                    new PerfRun(TestContext.CurrentContext.Test.FullName).AddSamples("Unavailable", new[] { double.NaN }).Write("Unavailable", 1.5);
                    Assert.Inconclusive("AI-015 explicit measurement stopped at its 45s work bound; no repeated profiling or timeout increase.");
                }
                double a = ai.CurrentMs, m = main.CurrentMs; f.Tick();
                double aiMs = ai.CurrentMs - a, mainMs = main.CurrentMs - m;
                f.Observe(); // Assertions, per-root readback and sample bookkeeping are outside both recorded scopes.
                if (tick < Warmup) continue;
                int index = tick - Warmup; aiSamples[index] = ai.Available ? aiMs : double.NaN;
                mainSamples[index] = main.Available ? mainMs : double.NaN;
                alive[index] = f.Alive; formation[index] = f.FormationError;
            }
            f.CheckCombat(); f.WriteReadback(TestContext.CurrentContext.Test.Name);
            var run = new PerfRun(TestContext.CurrentContext.Test.FullName).AddSamples("AiTotal", aiSamples)
                .AddSamples("FixedTickMainThread", mainSamples).AddSamples("AliveAiRoots", alive, "roots")
                .AddSamples("MaxFormationErrorPerTick", formation, "m");
            var frameRun = swarm ? new PerfRun(TestContext.CurrentContext.Test.FullName + ".MainThread")
                .AddSamples("FixedTickMainThread", mainSamples).AddSamples("AliveAiRoots", alive, "roots") : null;
            bool population = Array.TrueForAll(alive, value => value == f.Roots.Count);
            bool timings = ai.Available && main.Available && run.Get("AiTotal").Median > 0 && run.Get("FixedTickMainThread").Median > 0;
            if (!population || !timings)
            {
                run.AddSamples("Unavailable", new[] { double.NaN }).Write("Unavailable", 1.5);
                frameRun?.AddSamples("Unavailable", new[] { double.NaN }).Write("Unavailable", 10);
                TestContext.WriteLine("AI-015 measurements deferred: " + (!population ? "casualties reduced the stated population" : "profiler readings unavailable") + "; samples/readback retained.");
            }
            else
            {
                run.Write("AiTotal", 1.5); run.AssertMedian("AiTotal", 1.5);
                if (swarm)
                {
                    frameRun.Write("FixedTickMainThread", 10); frameRun.AssertMedian("FixedTickMainThread", 10);
                }
            }
            TestContext.WriteLine($"AI-015: roots={f.Roots.Count}, ships={(swarm ? 30 : 50)}, craft={(swarm ? 100 : 0)}, fleets={(swarm ? 10 : 2)}, " +
                $"warmup={Warmup}, samples={Samples}, AI median/p95={run.Get("AiTotal").Median:G8}/{run.Get("AiTotal").P95:G8}ms, " +
                $"fixed-tick main median/p95={run.Get("FixedTickMainThread").Median:G8}/{run.Get("FixedTickMainThread").P95:G8}ms; " +
                "CPU fixture includes real damage/physics; NoRender, no GPU or whole PlayerLoop claim; zero tuning passes.");
        }

        sealed class Cost : IDisposable
        {
            ProfilerRecorder recorder;
            public bool Available => recorder.Valid && recorder.IsRunning;
            public double CurrentMs => Available ? recorder.CurrentValue * 1e-6 : 0;
            public Cost(string name)
            {
                var handles = new List<ProfilerRecorderHandle>(); ProfilerRecorderHandle.GetAvailable(handles);
                foreach (var handle in handles)
                    if (ProfilerRecorderHandle.GetDescription(handle).Name == name)
                    { recorder = new ProfilerRecorder(handle, 1, ProfilerRecorderOptions.Default | ProfilerRecorderOptions.StartImmediately); break; }
            }
            public void Dispose() { if (recorder.Valid) recorder.Dispose(); }
        }

        internal sealed class Fixture : IDisposable
        {
            readonly CombatScenarioTests.Fixture combat;
            BlobAssetReference<AiProfilesBlob> profiles;
            readonly Dictionary<Entity, int> indices = new Dictionary<Entity, int>();
            readonly List<Entity> mounts = new List<Entity>();
            readonly List<float3> starts = new List<float3>();
            readonly List<bool> fired = new List<bool>(), moved = new List<bool>(), engaged = new List<bool>(), formed = new List<bool>();
            readonly List<bool> thrust = new List<bool>();
            readonly List<bool> craft = new List<bool>();
            readonly List<LocalTransform> before = new List<LocalTransform>();
            readonly SystemHandle anchors, decisions, fleets, steering, status;
            public readonly List<Entity> Roots = new List<Entity>();
            public EntityManager Em => combat.Em;
            public EcsTestWorld World => combat.World;
            public int Alive { get; private set; }
            public float FormationError { get; private set; }
            ushort formationIndex;
            public Fixture(bool swarm)
            {
                // These NUnit measurements execute synchronously; finish any previous world's outstanding work before creating the isolated world.
                if (Unity.Entities.World.DefaultGameObjectInjectionWorld != null && Unity.Entities.World.DefaultGameObjectInjectionWorld.IsCreated)
                    Unity.Entities.World.DefaultGameObjectInjectionWorld.EntityManager.CompleteAllTrackedJobs();
                try
                {
                    combat = new CombatScenarioTests.Fixture();
                    AiContent.CreateSingletons(Em, combat.Flight.Catalog, new List<Entity>()); profiles = World.GetSingleton<AiConfig>().Profiles;
                    World.AddSingleton(default(OriginShiftRecord));
                    // Explicitly execute the actual AI systems before the real fixed flight/combat tick. They are not added to the group a second time.
                    // Completion is outside FixedStep/OnUpdate, so the AI marker includes every scheduled job, gather and disposal without guessing job names.
                    anchors = World.World.GetOrCreateSystem<AiAnchorShiftSystem>(); decisions = World.World.GetOrCreateSystem<AiDecisionSystem>();
                    fleets = World.World.GetOrCreateSystem<FleetSystem>(); steering = World.World.GetOrCreateSystem<AiSteeringSystem>();
                    status = World.World.GetOrCreateSystem<PlayerCombatStatusSystem>();
                    var pre = World.World.GetExistingSystemManaged<GameplayPrePhysicsGroup>();
                    pre.AddSystemToUpdateList(World.World.GetOrCreateSystem<PropulsionStatsSystem>()); pre.SortSystems();
                    var demand = World.World.GetExistingSystemManaged<PowerDemandGroup>();
                    demand.AddSystemToUpdateList(World.World.GetOrCreateSystem<ThrusterPowerDemandSystem>()); demand.SortSystems();
                    formationIndex = combat.Flight.Catalog.IndexOf(combat.Flight.Catalog.Get<FormationDefinition>(new ContentId("formation_line")));
                    Assert.Greater(formationIndex, 0);
                    using var large = Build(false); using var small = Build(true);
                    if (swarm)
                    {
                        for (byte side = 0; side < 2; side++) for (int group = 0; group < 5; group++)
                        {
                            float x = (group - 2) * 800; Fleet(small, side, x, 10, true);
                            foreach (float offset in new[] { -350f, 350f, 450f }) Ship(large, side, new float3(x + offset, 0, side == 0 ? -120 : 120), false);
                        }
                    }
                    else
                    {
                        for (byte side = 0; side < 2; side++)
                        {
                            Fleet(large, side, 0, 8, false);
                            for (int i = 0; i < 17; i++) Ship(large, side, new float3(i < 8 ? -320 - i * 140 : 320 + (i - 8) * 140, 0, side == 0 ? -120 : 120), false);
                        }
                    }
                    Assert.AreEqual(swarm ? 130 : 50, Roots.Count);
                    // A noncombat celestial observer gives the status system a real player proxy to scan against, without removing an AI root.
                    var observer = Em.CreateEntity(typeof(PlayerShip), typeof(SpatialBody), typeof(LocalTransform), typeof(Allegiance));
                    Em.SetComponentData(observer, new SpatialBody { Radius = .5f }); Em.SetComponentData(observer, LocalTransform.FromPosition(0, 500, 0));
                    Em.SetComponentData(observer, Allegiance.None);
                }
                catch { Dispose(); throw; }
            }
            VoxelStructure Build(bool small)
            {
                var source = new VoxelStructure(.5f);
                try
                {
                    for (int z = 0; z < 12; z++) for (int x = 0; x < (small ? 12 : 32); x++) Place(source, "block_hull", new int3(x, 0, z));
                    Place(source, "block_core_small", new int3(6, 1, 5)); Place(source, "block_reactor_small", new int3(2, 1, 5));
                    Place(source, "block_capacitor_small", new int3(8, 1, 5)); Place(source, "weapon_plasma_turret_medium", new int3(5, 1, 8));
                    var positions = new[] { new int3(8, 1, 10), new int3(10, 1, 3), new int3(3, 1, 1), new int3(1, 1, 8) };
                    for (int i = 0; i < positions.Length; i++) Place(source, "block_thruster_small", positions[i], combat.Flight.Content.Orientations.Value.AxisTurn(1, i));
                    return source;
                }
                catch { source.Dispose(); throw; }
            }
            void Place(VoxelStructure source, string id, int3 origin, byte orientation = 0)
                => Assert.AreEqual(PlaceResult.Ok, source.TryPlaceBlock(new BlockPlacement { Block = combat.Flight.Index(id), Origin = origin, Orient = orientation }, combat.Flight.Content), id);
            Entity Ship(VoxelStructure prototype, byte side, float3 position, bool small)
            {
                var root = combat.Flight.SpawnSource(prototype.Clone(), position, profile: small ? GridProfile.SmallCraft : GridProfile.NpcShip);
                var request = Em.GetComponentData<GridSpawnRequest>(root);
                request.PersistentId = (ulong)(150000 + Roots.Count); request.Pose.rot = quaternion.RotateY(side == 0 ? 0 : math.PI);
                request.Flags |= GridSpawnFlags.PlanarInertia;
                Em.SetComponentData(root, request); Em.AddComponentData(root, new Allegiance { Faction = side });
                Em.AddComponentData(root, new AiBrain { ProfileIndex = 0 });
                indices.Add(root, Roots.Count); Roots.Add(root); starts.Add(position); before.Add(default); mounts.Add(Entity.Null);
                fired.Add(false); moved.Add(false); engaged.Add(false); formed.Add(false); thrust.Add(false); craft.Add(small); return root;
            }
            void Fleet(VoxelStructure prototype, byte side, float x, int count, bool small)
            {
                var center = new float3(x, 0, side == 0 ? -120 : 120); Entity leader = Ship(prototype, side, center, small);
                ref var slots = ref profiles.Value.Formations[formationIndex].Slots;
                quaternion rotation = quaternion.RotateY(side == 0 ? 0 : math.PI);
                for (int i = 1; i < count; i++)
                {
                    float2 slot = slots[i - 1]; var root = Ship(prototype, side, center + math.rotate(rotation, new float3(slot.x, 0, slot.y)), small);
                    Em.AddComponentData(root, new FleetMember { Leader = leader, Slot = (byte)(i - 1), FormationIndex = formationIndex });
                }
            }
            public void Ready()
            {
                for (int tick = 0; tick < 30; tick++)
                {
                    Tick(); bool ready = true;
                    for (int i = 0; i < Roots.Count; i++)
                    {
                        Entity root = Roots[i];
                        if (!Em.HasComponent<GridColliderState>(root) || Em.IsComponentEnabled<GridSpawning>(root)
                            || Em.GetComponentData<GridColliderState>(root).Phase != GridColliderPhase.Ready
                            || (Em.GetComponentData<FlightState>(root).Flags & FlightFlags.Initialized) == 0) { ready = false; continue; }
                        if (mounts[i] == Entity.Null) mounts[i] = combat.Mount(root);
                        if (Em.GetComponentData<WeaponMount>(mounts[i]).Initialized == 0) ready = false;
                    }
                    if (!ready) { ObserveReady(); continue; }
                    for (int i = 0; i < Roots.Count; i++)
                    {
                        var grid = Em.GetComponentData<VoxelGrid>(Roots[i]);
                        if (craft[i]) Assert.AreEqual(new int3(1), grid.BrickDims, "Every SmallCraft uses exactly one occupied brick.");
                        Assert.AreEqual(4, Em.GetBuffer<FlightThruster>(Roots[i]).Length);
                        Em.SetName(Roots[i], "AI015 " + (craft[i] ? "craft " : "ship ") + Em.GetComponentData<PersistentId>(Roots[i]).Value);
                    }
                    Observe(); return;
                }
                Assert.Fail("AI-015 compact fixtures did not initialize in 30 updates; do not extend the preparation bound.");
            }
            void ObserveReady()
            {
                // Preserve a shot/movement witness even if combat starts while the other compact roots finish colliders.
                for (int i = 0; i < Roots.Count; i++)
                {
                    if (mounts[i] != Entity.Null && Em.Exists(mounts[i])) fired[i] |= Em.GetComponentData<WeaponState>(mounts[i]).ShotCounter > 0;
                    if (Em.HasComponent<LocalTransform>(Roots[i])) moved[i] |= math.distance(Em.GetComponentData<LocalTransform>(Roots[i]).Position, starts[i]) > .5f;
                    if (Em.HasComponent<WeaponControl>(Roots[i])) engaged[i] |= Em.GetComponentData<WeaponControl>(Roots[i]).Autofire != 0;
                    if (Em.HasComponent<FlightState>(Roots[i])) thrust[i] |= (Em.GetComponentData<FlightState>(Roots[i]).Flags & FlightFlags.Firing) != 0;
                }
            }
            public void Tick()
            {
                for (int i = 0; i < Roots.Count; i++) if (Em.HasComponent<LocalTransform>(Roots[i])) before[i] = Em.GetComponentData<LocalTransform>(Roots[i]);
                using (FixedTick.Auto())
                {
                    using (AiStage.Auto())
                    {
                        anchors.Update(World.World.Unmanaged); decisions.Update(World.World.Unmanaged); fleets.Update(World.World.Unmanaged);
                        steering.Update(World.World.Unmanaged); status.Update(World.World.Unmanaged); Em.CompleteAllTrackedJobs();
                    }
                    World.StepTicks(1);
                }
            }
            public void Observe()
            {
                Alive = 0; FormationError = 0;
                for (int i = 0; i < Roots.Count; i++)
                {
                    Entity root = Roots[i]; if (!Em.Exists(root) || !Em.HasComponent<AiBrain>(root)) continue;
                    if (mounts[i] != Entity.Null && Em.Exists(mounts[i])) fired[i] |= Em.GetComponentData<WeaponState>(mounts[i]).ShotCounter > 0;
                    var pose = Em.GetComponentData<LocalTransform>(root); Assert.IsTrue(math.all(math.isfinite(pose.Position)));
                    moved[i] |= math.distance(pose.Position, starts[i]) > .5f;
                    thrust[i] |= (Em.GetComponentData<FlightState>(root).Flags & FlightFlags.Firing) != 0;
                    if (Em.HasComponent<Hulk>(root)) continue; Alive++;
                    var brain = Em.GetComponentData<AiBrain>(root); var target = Em.GetComponentData<ShipTarget>(root);
                    engaged[i] |= brain.State == AiState.Engage && target.Value != Entity.Null && Em.GetComponentData<WeaponControl>(root).Autofire != 0;
                    if (!Em.HasComponent<FleetMember>(root)) continue;
                    var member = Em.GetComponentData<FleetMember>(root);
                    if (member.Leader == Entity.Null || !indices.TryGetValue(member.Leader, out int leader) || !Em.Exists(member.Leader) || Em.HasComponent<Hulk>(member.Leader)) continue;
                    float yaw = Planar.YawFromForward(math.rotate(before[leader].Rotation, new float3(0, 0, 1)));
                    float2 slot = profiles.Value.Formations[member.FormationIndex].Slots[member.Slot];
                    float3 goal = before[leader].Position + math.rotate(quaternion.RotateY(yaw), new float3(slot.x, 0, slot.y));
                    Assert.Less(math.distance(brain.GoalPosition, goal), .001f, "Production FleetSystem must own the correct transformed slot goal.");
                    formed[i] = true; FormationError = math.max(FormationError, math.distance(pose.Position, goal));
                    Assert.AreEqual(Em.GetComponentData<ShipTarget>(member.Leader), target, "Members inherit the leader target on the actual decision/fleet path.");
                }
            }
            public void CheckCombat()
            {
                for (int i = 0; i < Roots.Count; i++)
                {
                    Assert.IsTrue(fired[i], "Every original combatant must release a real powered shot: " + i);
                    Assert.IsTrue(engaged[i], "Every combatant must engage through actual AI: " + i);
                    Assert.IsTrue(moved[i], "Every combatant must move through actual thrusters/physics: " + i);
                    Assert.IsTrue(thrust[i], "Actual propulsion must fire on every original combatant: " + i);
                    if (Em.Exists(Roots[i]) && Em.HasComponent<FleetMember>(Roots[i])) Assert.IsTrue(formed[i]);
                }
                var store = combat.Store; Assert.Greater(store.Stats.Value.Fired, 0); Assert.Greater(store.Stats.Value.Hits, 0);
                Assert.AreEqual(0, store.Stats.Value.DroppedFull);
            }
            public string Readback()
            {
                var text = new StringBuilder("id,faction,craft,alive,fired,moved,leader,slot,position,goal,target\n");
                for (int i = 0; i < Roots.Count; i++)
                {
                    Entity root = Roots[i]; if (!Em.Exists(root)) continue;
                    var pose = Em.GetComponentData<LocalTransform>(root); var brain = Em.GetComponentData<AiBrain>(root);
                    var member = Em.HasComponent<FleetMember>(root) ? Em.GetComponentData<FleetMember>(root) : default;
                    text.Append(Em.GetComponentData<PersistentId>(root).Value).Append(',').Append(Em.GetComponentData<Allegiance>(root).Faction)
                        .Append(',').Append(craft[i]).Append(',').Append(!Em.HasComponent<Hulk>(root)).Append(',').Append(fired[i]).Append(',').Append(moved[i])
                        .Append(',').Append(member.Leader).Append(',').Append(member.Slot).Append(",\"").Append(pose.Position).Append("\",\"")
                        .Append(brain.GoalPosition).Append("\",\"").Append(Em.GetComponentData<ShipTarget>(root).Value).Append("\"\n");
                }
                return text.ToString();
            }
            public string WriteReadback(string name)
            {
                string directory = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Logs", "playtest", "AI015"); Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, name + ".csv"); File.WriteAllText(path, Readback()); TestContext.WriteLine(path); return path;
            }
            public void Dispose() { combat?.Dispose(); if (profiles.IsCreated) profiles.Dispose(); }
        }

#if UNITY_EDITOR
        static Fixture visual;
        static double nextVisualTick;
        static UnityEditor.SceneView visualScene;
        static Vector3 previousPivot;
        static Quaternion previousRotation;
        static float previousSize;
        static bool previousOrthographic;
        /// <summary>Lead-only diagnostic view of real simulation poses/slot goals; no game UI, art or input changes.</summary>
        public static string BeginVisual(bool swarm = false)
        {
            EndVisual();
            try
            {
                visual = new Fixture(swarm); visual.Ready(); nextVisualTick = UnityEditor.EditorApplication.timeSinceStartup;
                UnityEditor.EditorApplication.update += AdvanceVisual; UnityEditor.SceneView.duringSceneGui += DrawVisual;
                UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += EndVisual; UnityEditor.EditorApplication.playModeStateChanged += VisualState;
                var scene = UnityEditor.EditorWindow.GetWindow<UnityEditor.SceneView>();
                visualScene = scene; previousPivot = scene.pivot; previousRotation = scene.rotation;
                previousSize = scene.size; previousOrthographic = scene.orthographic;
                scene.rotation = Quaternion.Euler(90, 0, 0); scene.orthographic = true;
                scene.Frame(new Bounds(Vector3.zero, new Vector3(swarm ? 4200 : 3400, 30, 1200)), false);
                return "AI015 visual started; Scene view wires show actual hull bounds, fleet slots and goals. EndVisual releases the fixture.";
            }
            catch { EndVisual(); throw; }
        }
        public static string ReadVisual() => visual == null ? "No AI015 visual fixture" : visual.Readback();
        public static void EndVisual()
        {
            UnityEditor.EditorApplication.update -= AdvanceVisual; UnityEditor.SceneView.duringSceneGui -= DrawVisual;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= EndVisual; UnityEditor.EditorApplication.playModeStateChanged -= VisualState;
            visual?.Dispose(); visual = null;
            if (visualScene != null)
            {
                visualScene.pivot = previousPivot; visualScene.rotation = previousRotation;
                visualScene.size = previousSize; visualScene.orthographic = previousOrthographic; visualScene.Repaint();
            }
            visualScene = null;
        }
        static void VisualState(UnityEditor.PlayModeStateChange state) { if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode) EndVisual(); }
        static void AdvanceVisual()
        {
            if (visual == null || UnityEditor.EditorApplication.timeSinceStartup < nextVisualTick) return;
            nextVisualTick = UnityEditor.EditorApplication.timeSinceStartup + 1.0 / 60;
            try { visual.Tick(); visual.Observe(); UnityEditor.SceneView.RepaintAll(); }
            catch { EndVisual(); throw; }
        }
        static void DrawVisual(UnityEditor.SceneView scene)
        {
            if (visual == null) return;
            Color previous = UnityEditor.Handles.color; Matrix4x4 matrix = UnityEditor.Handles.matrix;
            try
            {
                foreach (Entity root in visual.Roots)
                {
                    if (!visual.Em.Exists(root)) continue;
                    var pose = visual.Em.GetComponentData<LocalTransform>(root); var grid = visual.Em.GetComponentData<VoxelGrid>(root);
                    UnityEditor.Handles.color = visual.Em.HasComponent<Hulk>(root) ? new Color32(87, 90, 82, 255)
                        : visual.Em.GetComponentData<Allegiance>(root).Faction == 0 ? new Color32(57, 213, 255, 255) : new Color32(255, 85, 48, 255);
                    UnityEditor.Handles.matrix = Matrix4x4.TRS(pose.Position, pose.Rotation, Vector3.one);
                    UnityEditor.Handles.DrawWireCube(((float3)grid.CellMin + (float3)(grid.CellMax + 1)) * (.5f * grid.CellSize),
                        ((float3)(grid.CellMax - grid.CellMin + 1)) * grid.CellSize);
                    UnityEditor.Handles.matrix = Matrix4x4.identity;
                    if (!visual.Em.HasComponent<FleetMember>(root) || visual.Em.HasComponent<Hulk>(root)) continue;
                    var goal = visual.Em.GetComponentData<AiBrain>(root).GoalPosition;
                    UnityEditor.Handles.DrawLine(pose.Position, goal); UnityEditor.Handles.DrawWireDisc(goal, Vector3.up, 5);
                }
            }
            finally { UnityEditor.Handles.color = previous; UnityEditor.Handles.matrix = matrix; }
        }
#endif
    }
}
