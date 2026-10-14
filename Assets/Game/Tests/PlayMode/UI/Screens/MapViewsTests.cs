using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.App.Services;
using Game.App.Session;
using Game.Core;
using Game.Core.Audio;
using Game.Core.Components;
using Game.Core.Events;
using Game.Core.Flow;
using Game.Core.Settings;
using Game.Data.Components;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Logic.Compiler;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Economy;
using Game.Galaxy.Components;
using Game.Galaxy.Data;
using Game.Galaxy.Services;
using Game.Galaxy.Systems;
using Game.Input;
using Game.Narrative;
using Game.Narrative.Dialog;
using Game.Narrative.ShipLog;
using Game.Presentation.Audio;
using Game.Presentation.Bridges;
using Game.Presentation.Presenters;
using Game.Simulation;
using Game.Simulation.Commands;
using Game.Simulation.Factions;
using Game.Tests.Common;
using Game.UI.Controls;
using Game.UI.Data;
using Game.UI.Services;
using Game.UI.ViewModels;
using Game.UI.Views;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.UI.Screens
{
    /// <summary>HUD-016's seven cases: real galaxy/loader/travel/territory/query owners and rendered production views.</summary>
    public sealed class MapViewsTests
    {
        [SetUp] public void Setup() { Services.Reset(); EventHub.Reset(); }
        [TearDown] public void Cleanup() { EventHub.Reset(); Services.Reset(); }
        static void Field(object target, string name, object value)
        { target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value); }
        sealed class Fixture : IDisposable
        {
            readonly List<Object> assets = new();
            readonly List<ContentDefinition> definitions = new();
            readonly List<Entity> factionEntities = new();
            internal readonly EcsTestWorld Ecs = EcsTestWorld.Create(typeof(OriginRebaseSystem));
            internal EntityManager Em => Ecs.EntityManager;
            internal readonly UITestPanel Panel;
            internal readonly UIRoot UI;
            internal readonly GalaxyService Galaxy;
            internal readonly TravelService Travel;
            internal readonly StarSystemLoader Loader;
            internal readonly GalaxyMapPresenter GalaxyPresenter;
            internal readonly StarSystemMapPresenter SystemPresenter;
            internal readonly GalaxyMapViewModel GalaxyModel = new();
            internal readonly StarSystemMapViewModel SystemModel = new();
            internal readonly GalaxyMapView GalaxyView;
            internal readonly StarSystemMapView SystemView;
            internal readonly ContentCatalog Catalog;
            internal readonly NarrativeService Narrative;
            internal readonly NarrativeBootstrap.StoryRuntime Story;
            internal readonly RumorDefinition Rumor;
            internal readonly QuestDefinition Quest;
            internal readonly AuthoredStarSystemDefinition A, B, C;
            internal readonly Entity Player;
            readonly GalaxyState state = new();
            readonly PauseService pause = new();
            readonly GameSession session;
            readonly FactionService factions;
            readonly TerritoryService territory;
            readonly BlueprintLibrary blueprints;
            readonly VoxelStructureRegistry registry;
            readonly AudioService audio;
            readonly LogicWorldAdapter logicWorld;
            readonly DialogService dialog;
            readonly DialogPresenter dialogPresenter;
            readonly InputService input;
            readonly InputServiceBridge inputBridge;
            readonly NativeQueue<ManagedEventEnvelope<OriginShifted>> originEvents;
            internal Fixture(int width = 1280, int height = 720, bool hideRumorTarget = false)
            {
                try
                {
                    Create<BlockDefinition>("block_missing"); var fuel = Create<ItemDefinition>("item_missing");
                    var mesh = new Mesh(); assets.Add(mesh); var material = new Material(Shader.Find("Hidden/InternalErrorShader")); assets.Add(material);
                    var visual = Create<BodyVisualDefinition>("bodyvis_maps", so => { so.FindProperty("mesh").objectReferenceValue = mesh; so.FindProperty("material").objectReferenceValue = material; });
                    BodyTemplateDefinition Body(string id, BodyRole role) => Create<BodyTemplateDefinition>(id, so =>
                    {
                        so.FindProperty("displayName").stringValue = id; so.FindProperty("role").intValue = (int)role;
                        so.FindProperty("visual").objectReferenceValue = visual; so.FindProperty("hasCollider").boolValue = false;
                        so.FindProperty("storyTag").stringValue = role == BodyRole.Planet ? "survey" : "";
                    });
                    var star = Body("body_map_star", BodyRole.Star); var planet = Body("body_map_planet", BodyRole.Planet); var gate = Body("body_map_gate", BodyRole.LaneGate);
                    var template = Create<StarSystemTemplateDefinition>("systpl_maps", so =>
                    {
                        var pool = so.FindProperty("starPool"); pool.arraySize = 1;
                        pool.GetArrayElementAtIndex(0).FindPropertyRelative("template").objectReferenceValue = star;
                        pool.GetArrayElementAtIndex(0).FindPropertyRelative("weight").floatValue = 1;
                    });
                    AuthoredStarSystemDefinition System(string id, Vector2 position) => Create<AuthoredStarSystemDefinition>(id, so =>
                    {
                        so.FindProperty("displayName").stringValue = id; so.FindProperty("positionLy").vector2Value = position;
                        so.FindProperty("template").objectReferenceValue = template; so.FindProperty("star").objectReferenceValue = star;
                        so.FindProperty("startBodyKey").stringValue = "planet";
                        var bodies = so.FindProperty("bodies"); bodies.arraySize = 1; var body = bodies.GetArrayElementAtIndex(0);
                        body.FindPropertyRelative("key").stringValue = "planet"; body.FindPropertyRelative("role").intValue = (int)BodyRole.Planet;
                        body.FindPropertyRelative("template").objectReferenceValue = planet; body.FindPropertyRelative("mode").intValue = (int)OrbitMode.Circular;
                        body.FindPropertyRelative("orbitRadiusM").floatValue = 4000; body.FindPropertyRelative("phaseDegrees").floatValue = 45;
                    });
                    A = System("starsys_map_a", Vector2.zero); B = System("starsys_map_b", new Vector2(6, 0)); C = System("starsys_map_c", new Vector2(6, 6));
                    if (hideRumorTarget)
                    { using var so = new SerializedObject(C); so.FindProperty("hiddenUntilKnown").boolValue = true; so.ApplyModifiedPropertiesWithoutUndo(); }
                    var generation = Create<GalaxyGenerationDefinition>("galaxy_maps", so =>
                    {
                        so.FindProperty("proceduralStarSystemCount").intValue = 0; so.FindProperty("startStarSystem").objectReferenceValue = A;
                        so.FindProperty("defaultHyperlaneGate").objectReferenceValue = gate; so.FindProperty("defaultAncientGate").objectReferenceValue = gate;
                        var systems = so.FindProperty("authoredStarSystems"); systems.arraySize = 3;
                        for (int i = 0; i < 3; i++) systems.GetArrayElementAtIndex(i).objectReferenceValue = new[] { A, B, C }[i];
                        var pool = so.FindProperty("templatePool"); pool.arraySize = 1;
                        pool.GetArrayElementAtIndex(0).FindPropertyRelative("template").objectReferenceValue = template; pool.GetArrayElementAtIndex(0).FindPropertyRelative("weight").floatValue = 1;
                        var lanes = so.FindProperty("authoredLanes"); lanes.arraySize = 3;
                        for (int i = 0; i < 3; i++)
                        {
                            var row = lanes.GetArrayElementAtIndex(i); row.FindPropertyRelative("a").objectReferenceValue = i == 1 ? B : A;
                            row.FindPropertyRelative("b").objectReferenceValue = i == 0 ? B : C;
                        }
                    });
                    Field(generation.AuthoredLanes[0].condition, "source", "$gate_open"); generation.AuthoredLanes[0].lockedMessage = "Gate is asleep.";
                    Field(generation.AuthoredLanes[2].condition, "source", "false"); generation.AuthoredLanes[2].lockedMessage = "Direct lane is sealed.";
                    Create<FactionDefinition>("faction_ark", so => { so.FindProperty("isPlayerFaction").boolValue = true; so.FindProperty("relationLocked").boolValue = true; });
                    Create<FactionDefinition>("faction_vestari", so =>
                    {
                        so.FindProperty("mapColorToken").stringValue = "--color-faction-vestari";
                        var owned = so.FindProperty("ownedStarSystems"); owned.arraySize = 1; owned.GetArrayElementAtIndex(0).objectReferenceValue = B;
                    });
                    var encounterConfig = Create<EncounterConfigDefinition>("config_encounters");
                    var economyConfig = Create<EconomyConfigDefinition>("config_economy");
                    Create<TravelConfigDefinition>("config_travel", so => so.FindProperty("fuelItem").objectReferenceValue = fuel);
                    var gameConfig = Create<GameConfigDefinition>("config_map_game"); var narrativeConfig = Create<NarrativeConfigDefinition>("narrative_maps");
                    var facts = Create<FactCatalogDefinition>("facts_maps"); Field(facts, "facts", new[] {
                        new FactDeclaration { Name = "gate_open", Kind = FactKind.Bool }, new FactDeclaration { Name = "test_done", Kind = FactKind.Bool } });
                    Rumor = Create<RumorDefinition>("rumor_map_target", so => { so.FindProperty("title").stringValue = "Signal beyond the gate"; so.FindProperty("target").objectReferenceValue = C; });
                    Field(Rumor.ResolvedWhen, "source", "$test_done");
                    Quest = Create<QuestDefinition>("quest_map_survey");
                    var objective = new QuestObjective { id = "survey", text = "Survey the planet", marker = new QuestMarker { starSystem = A, bodyTag = "survey" } };
                    Field(objective.doneWhen, "source", "$test_done");
                    Field(Quest, "stages", new List<QuestStage> { new QuestStage { id = "survey", objectives = new List<QuestObjective> { objective } } });
                    var program = Create<DialogProgramDefinition>("dialog_program");
                    Field(program, "nodes", new[] { new DialogNodeRecord { Title = "unused", Start = 0, Length = 1 } });
                    Field(program, "code", new[] { new DialogInstruction { Op = DialogOp.EndNode } });
                    Create<DialogCommandTableDefinition>("dialog_command_table");
                    var report = new ValidationReport("Map views real owners"); Catalog = ContentCatalog.Build(definitions, report);
                    var refs = RefResolverRegistry.CreateDefault(Catalog); var factCatalog = FactCatalog.Build(Catalog, refs, report);
                    var compiler = new LogicCompiler(factCatalog, refs, new LogicFunctionRegistry());
                    foreach (var definition in definitions) if (definition is ILogicSourceOwner owner) owner.CompileLogic(compiler, report.For(definition.IdString));
                    Assert.IsFalse(report.HasErrors, report.ToText());
                    Ecs.AddGameplayActive(); Ecs.AddWorldSceneActive(); Ecs.AddSimClock(universalTime: 123.25); Ecs.AddSingleton(default(PlayerControlLock));
                    originEvents = new NativeQueue<ManagedEventEnvelope<OriginShifted>>(Allocator.Persistent); Ecs.AddSingleton(new ManagedEventQueue<OriginShifted> { Queue = originEvents });
                    Player = Em.CreateEntity(typeof(PlayerShip), typeof(LocalTransform), typeof(UniversePosition)); Em.SetComponentData(Player, LocalTransform.Identity);
                    FactionContent.CreateSingletons(Em, Catalog, encounterConfig, factionEntities); factions = new FactionService(Ecs.World, Catalog); Services.Register(factions);
                    Galaxy = new GalaxyService(Catalog, state, Ecs.World, new GalaxyFrameTicker());
                    territory = new TerritoryService(Galaxy, factions, Catalog); Services.Register(territory);
                    blueprints = new BlueprintLibrary(Catalog, default); registry = VoxelStructureRegistry.CreateRuntime(blueprints); session = new GameSession(Ecs.World, gameConfig);
                    var docking = new DockingService(); Loader = new StarSystemLoader(Galaxy, Ecs.World, registry, blueprints, session, Catalog, docking); Services.Register(Loader);
                    audio = new AudioService(null, null, AudioVolumes.Default, null); Travel = new TravelService(Galaxy, Loader, docking, Ecs.World, Catalog, pause, audio);
                    input = new InputService(new ControlSettingsData()); inputBridge = new InputServiceBridge(input);
                    Panel = new UITestPanel(width, height); var config = Object.Instantiate(AssetDatabase.LoadAssetAtPath<UIConfig>("Assets/Game/UI/Data/UIConfig.asset")); assets.Add(config); config.PanelSettings = Panel.Settings;
                    UI = new UIRoot(Panel.Document, config, pause, inputBridge, new NullUISoundSink(), new UnityFrameClock());
                    logicWorld = new LogicWorldAdapter(Catalog, session, Galaxy, blueprints, audio, UI.Notifications, Ecs.World, narrativeConfig);
                    var adapter = new NarrativeLogicAdapter(); var economy = new EconomyService(Catalog, economyConfig, () => 42, () => 123.25);
                    Narrative = NarrativeBootstrap.Build(Catalog, definitions, store => new LogicContext(store, economy, adapter, logicWorld, () => 42));
                    Services.Register<ILogicContext>(Narrative.Context);
                    dialog = NarrativeBootstrap.BuildDialog(Catalog, Narrative.Context); adapter.BindDialog(dialog);
                    dialogPresenter = new DialogPresenter(dialog, Catalog, new DialogViewModel(), UI.Navigator, new NullUISoundSink());
                    Story = NarrativeBootstrap.BuildStory(Catalog, Narrative, adapter, dialog, 0);
                    Galaxy.NewGame(42); Galaxy.Reveal(StarSystemId.FromAuthored(B.Id.Hash));
                    if (!hideRumorTarget) Galaxy.Reveal(StarSystemId.FromAuthored(C.Id.Hash));
                    Narrative.OnSessionRestored(); Narrative.SetWorldReady(true);
                    state.CurrentStarSystem = Galaxy.Galaxy.StartStarSystem; state.HasPlayerLocation = true; state.PlayerLocation = new PlayerLocation { StarSystem = state.CurrentStarSystem };
                    Loader.OnSceneReady(new SceneReadyContext(GameScene.World, SceneEntryReason.NewGame, default));
                    Ecs.UpdateSystem<OriginRebaseSystem>(); Ecs.CompleteAll(); Loader.Tick(new GalaxyFrameContext(Em, 123.25, 0, 0, 0)); Assert.IsTrue(Loader.IsReady);
                    Em.SetComponentData(Player, LocalTransform.FromPosition(new float3(12345.5f, 0, -9876.25f)));
                    GalaxyPresenter = new GalaxyMapPresenter(GalaxyModel, Galaxy, new GalaxyMapModel(Galaxy, Travel, territory), Travel);
                    GalaxyView = new GalaxyMapView(GalaxyModel, GalaxyPresenter); UI.Register(GalaxyView); GalaxyPresenter.Bind(GalaxyView, Story.ShipLog);
                    SystemPresenter = new StarSystemMapPresenter(SystemModel, Galaxy, Loader, Ecs.World);
                    SystemView = new StarSystemMapView(SystemModel, SystemPresenter); UI.Register(SystemView); SystemPresenter.Bind(SystemView, Story.ShipLog);
                    Frame();
                }
                catch { Dispose(); throw; }
            }
            T Create<T>(string id, Action<SerializedObject> edit = null) where T : ContentDefinition
            {
                var value = ScriptableObject.CreateInstance<T>(); assets.Add(value); definitions.Add(value);
                using var so = new SerializedObject(value); so.FindProperty("id").stringValue = id; edit?.Invoke(so); so.ApplyModifiedPropertiesWithoutUndo(); return value;
            }
            internal int Index(AuthoredStarSystemDefinition system)
            { var id = StarSystemId.FromAuthored(system.Id.Hash); for (int i = 0; i < GalaxyModel.Data.Points.Count; i++) if (GalaxyModel.Data.Points[i].Pos.Equals(Galaxy.Galaxy.Get(id).PositionLy)) return i; throw new InvalidOperationException("Map target absent"); }
            internal void OpenGate() { Narrative.Facts.SetBySlot(Narrative.Facts.Catalog.SlotOf("gate_open"), 1, FactWriteSource.Debugger); Frame(); }
            internal void Frame() { EventHub.DrainAll(); Narrative.Tick(); GalaxyPresenter?.Tick(); SystemPresenter?.Tick(); }
            internal double2 PlayerPosition
            { get { using var origins = Em.CreateEntityQuery(typeof(FrameOrigin)); return (origins.GetSingleton<FrameOrigin>().OriginInStarSystem + (double3)Em.GetComponentData<LocalTransform>(Player).Position).xz; } }
            public void Dispose()
            {
                Ecs.CompleteAll(); GalaxyPresenter?.Dispose(); SystemPresenter?.Dispose(); dialog?.Interrupt(); Story?.Dispose(); dialogPresenter?.Dispose(); UI?.Dispose();
                Services.Reset(); logicWorld?.Dispose(); Travel?.Dispose(); Loader?.Dispose(); territory?.Dispose(); session?.Dispose();
                registry?.Dispose(); if (registry != null) Object.DestroyImmediate(registry); blueprints?.Dispose(); factions?.Dispose(); Galaxy?.Dispose(); state.Dispose();
                foreach (var entity in factionEntities) if (Em.Exists(entity) && Em.HasComponent<FactionRelations>(entity)) Em.GetComponentData<FactionRelations>(entity).Dispose();
                Ecs.Dispose(); if (originEvents.IsCreated) originEvents.Dispose(); inputBridge?.Dispose(); input?.Dispose(); audio?.Dispose(); pause.Dispose(); Panel?.Dispose();
                foreach (var asset in assets) if (asset != null) Object.DestroyImmediate(asset);
            }
        }
        static void Activate(UIScreen view, string name) => DialogTestContext.Activate(view.Root.Q<Button>(name));
        [UnityTest] public IEnumerator GalaxyOpen_CentredOnCurrent()
        {
            using var f = new Fixture(); f.UI.Navigator.Push(f.GalaxyView); yield return null; yield return null;
            var canvas = f.GalaxyView.Canvas; var current = f.Galaxy.Galaxy.Get(f.Galaxy.State.CurrentStarSystem).PositionLy;
            Assert.Less(math.distance(current, canvas.View.Center), 1e-9); Assert.AreEqual(InputContext.Menu, f.UI.Navigator.CurrentContext);
            Assert.IsTrue(f.UI.Navigator.IsPausing); Assert.Greater(canvas.TessellationCount, 0);
            double scale = canvas.View.UnitsPerPixel; Activate(f.GalaxyView, "galaxy-map-zoom-in"); Assert.Less(canvas.View.UnitsPerPixel, scale);
            using (var key = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = KeyCode.RightArrow })) canvas.SendEvent(key);
            Assert.Greater(canvas.View.Center.x, current.x); Activate(f.GalaxyView, "galaxy-map-focus-current"); Assert.Less(math.distance(current, canvas.View.Center), 1e-9);
            Activate(f.GalaxyView, "galaxy-map-close"); Assert.IsFalse(f.GalaxyView.IsOpen); Assert.IsFalse(f.UI.Navigator.IsPausing);
        }
        [UnityTest] public IEnumerator SystemOpen_CentredOnPlayer()
        {
            using var f = new Fixture(); int quest = f.Catalog.IndexOf(f.Quest); Assert.IsTrue(f.Story.Quests.Start(quest)); f.Story.Quests.Track(quest); f.Frame();
            f.UI.Navigator.Push(f.SystemView); yield return null; yield return null;
            Assert.Less(math.distance(f.PlayerPosition, f.SystemView.Canvas.View.Center), 1e-7);
            Assert.IsTrue(f.SystemModel.Data.Markers.Any(m => m.Kind == MapMarkerKind.Player && math.distance(m.Pos, f.PlayerPosition) < 1e-7));
            BodySpec body = default;
            foreach (var candidate in f.Galaxy.GetContent(f.Loader.Loaded).Bodies)
                if (candidate.Id.Role == BodyRole.Planet) { body = candidate; break; }
            Assert.AreEqual(BodyRole.Planet, body.Id.Role);
            var point = f.SystemModel.Data.Points.First(p => p.Name == f.Galaxy.GetBodyName(body.Id));
            Assert.Less(math.distance(OrbitMath.Position(body, 123.25).xz, point.Pos), 1e-7); Assert.IsNotEmpty(f.SystemModel.Data.Rings);
            Assert.IsTrue(f.SystemModel.Data.Markers.Any(m => m.Kind == MapMarkerKind.Waypoint && math.distance(m.Pos, point.Pos) < 1e-7));
            Assert.IsFalse(f.SystemModel.CanSetWaypoint); Assert.AreEqual(DisplayStyle.None, f.SystemView.Root.Q<Button>("system-map-set-waypoint").resolvedStyle.display);
        }
        [UnityTest] public IEnumerator GatedTarget_DestinationDisabledWithReason()
        {
            using var f = new Fixture(); f.UI.Navigator.Push(f.GalaxyView); yield return null;
            f.GalaxyPresenter.Select(f.Index(f.B));
            Assert.IsFalse(f.Travel.CanSetDestination(StarSystemId.FromAuthored(f.B.Id.Hash), out string reason)); Assert.IsNotEmpty(reason);
            Assert.IsFalse(f.GalaxyModel.CanSetDestination); Assert.AreEqual(reason, f.GalaxyModel.DestinationBlockedReason);
            Assert.AreEqual(reason, f.GalaxyView.Root.Q<Label>("galaxy-map-destination-blocked").text);
            Assert.IsFalse(f.GalaxyView.Root.Q<Button>("galaxy-map-set-destination").enabledSelf);
            f.GalaxyPresenter.SetDestination(); Assert.IsFalse(f.Travel.Destination.IsValid);
        }
        [UnityTest] public IEnumerator Destination_RouteDrawnAndCleared()
        {
            using var f = new Fixture(); f.OpenGate(); f.UI.Navigator.Push(f.GalaxyView); yield return null;
            int tessellations = f.GalaxyView.Canvas.TessellationCount;
            f.GalaxyPresenter.Select(f.Index(f.C)); Activate(f.GalaxyView, "galaxy-map-set-destination");
            yield return null; yield return null; Assert.Greater(f.GalaxyView.Canvas.TessellationCount, tessellations);
            Assert.AreEqual(StarSystemId.FromAuthored(f.C.Id.Hash), f.Travel.Destination); Assert.AreEqual(2, f.Travel.Route.Count);
            Assert.AreEqual(2, f.GalaxyModel.Data.Lines.Count(line => line.Route));
            int a = f.Index(f.A), c = f.Index(f.C);
            Assert.IsFalse(f.GalaxyModel.Data.Lines.Single(line => line.A == a && line.B == c || line.A == c && line.B == a).Route, "The unrelated destination-adjacent direct lane stays unhighlighted.");
            Assert.AreEqual(1, f.GalaxyModel.Data.Points.Count(point => (point.Flags & MapPointFlags.Destination) != 0));
            Activate(f.GalaxyView, "galaxy-map-clear-destination");
            yield return null;
            Assert.IsFalse(f.Travel.Destination.IsValid); Assert.IsFalse(f.GalaxyModel.Data.Lines.Any(line => line.Route));
            Assert.IsFalse(f.GalaxyModel.Data.Points.Any(point => (point.Flags & MapPointFlags.Destination) != 0));
            Assert.IsFalse(f.GalaxyModel.Data.Markers.Any(marker => marker.Kind == MapMarkerKind.Destination));
        }
        [UnityTest] public IEnumerator OwnerColour_FromToken()
        {
            using var f = new Fixture(); f.UI.Navigator.Push(f.GalaxyView); yield return null; yield return null;
            Assert.AreEqual("--color-faction-vestari", f.GalaxyModel.Data.Points[f.Index(f.B)].ColorToken);
            Color colour = default; bool found = false;
            for (var element = (VisualElement)f.GalaxyView.Canvas; element != null && !found; element = element.parent)
                found = element.customStyle.TryGetValue(new CustomStyleProperty<Color>("--color-faction-vestari"), out colour);
            Assert.IsTrue(found); Assert.Greater(colour.a, 0);
            Assert.AreEqual(colour, f.GalaxyView.Canvas.PointColor(f.GalaxyModel.Data.Points[f.Index(f.B)]), "Drawing resolves the approved ancestor token");
            Assert.Greater(f.GalaxyView.Canvas.TessellationCount, 0);
        }
        [UnityTest] public IEnumerator RumorMarker_Shown()
        {
            using var f = new Fixture(hideRumorTarget: true); f.UI.Navigator.Push(f.GalaxyView); yield return null;
            Assert.IsFalse(f.GalaxyModel.Data.Markers.Any(m => m.Kind == MapMarkerKind.Rumor));
            Assert.IsTrue(f.Story.Clues.Reveal(f.Catalog.IndexOf<ClueDefinition>(f.Rumor))); f.Frame();
            var position = f.Galaxy.Galaxy.Get(StarSystemId.FromAuthored(f.C.Id.Hash)).PositionLy;
            Assert.IsFalse(f.Galaxy.State.IsKnown(StarSystemId.FromAuthored(f.C.Id.Hash)), "A rumor marker does not reveal its target system.");
            Assert.AreEqual("Uncharted", f.GalaxyModel.Data.Points[f.Index(f.C)].Name);
            Assert.IsTrue(f.GalaxyModel.Data.Markers.Any(m => m.Kind == MapMarkerKind.Rumor && math.distance(position, m.Pos) < 1e-9 && m.Label == f.Rumor.Title));
            f.UI.Navigator.Pop(f.GalaxyView); f.UI.Navigator.Push(f.GalaxyView, StarSystemId.FromAuthored(f.C.Id.Hash)); yield return null;
            Assert.AreEqual(f.Index(f.C), f.GalaxyModel.SelectedIndex); Assert.Less(math.distance(position, f.GalaxyView.Canvas.View.Center), 1e-9);
            f.GalaxyPresenter.Reset(); f.Frame(); Assert.AreEqual(1, f.GalaxyModel.Data.Markers.Count(m => m.Kind == MapMarkerKind.Rumor), "Reset must preserve one canonical query binding.");
        }
        [UnityTest] public IEnumerator Layout_AllLogicalSizes_NoIssues()
        {
            var issues = new List<LayoutIssue>();
            foreach (var size in UITestPanel.LogicalSizes)
            {
                using var f = new Fixture(size.x, size.y);
                foreach (var view in new UIScreen[] { f.GalaxyView, f.SystemView })
                {
                    f.UI.Navigator.Push(view); yield return null; yield return null;
                    UILayoutAudit.Run(view.Root, f.Panel.Viewport, issues);
                    Assert.IsEmpty(issues, size + " " + view.Key + ": " + string.Join(", ", issues.Select(issue => issue.Kind + " " + issue.ElementPath)));
                    f.UI.Navigator.Pop(view);
                }
            }
        }
    }
}
