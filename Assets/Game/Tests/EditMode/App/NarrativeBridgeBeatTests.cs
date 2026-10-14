using System;
using System.Collections.Generic;
using Game.App.Services;
using Game.App.Session;
using Game.App.Spawning;
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
using Game.Editor.Narrative.Dialog;
using Game.Galaxy.Components;
using Game.Galaxy.Data;
using Game.Galaxy.Encounters;
using Game.Galaxy.Services;
using Game.Galaxy.Systems;
using Game.Galaxy.Travel;
using Game.Input;
using Game.Narrative;
using Game.Narrative.Dialog;
using Game.Narrative.ShipLog;
using Game.Presentation.Audio;
using Game.Presentation.Bridges;
using Game.Presentation.Systems;
using Game.Simulation;
using Game.Simulation.Ai;
using Game.Simulation.Commands;
using Game.Simulation.Factions;
using Game.Tests.Common;
using Game.Tests.EditMode.Data;
using Game.Tests.EditMode.Data.Logic;
using Game.Tests.EditMode.Galaxy;
using Game.UI.Data;
using Game.UI.Services;
using Game.UI.ViewModels;
using Game.Voxel.Damage;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Game.Tests.EditMode.App
{
    /// <summary>QST-008: real app composition owners, compiled dialog and ordered world events; no alternate beat/travel state.</summary>
    public sealed class NarrativeBridgeBeatTests
    {
        [SetUp] public void Setup() { Services.Reset(); EventHub.Reset(); GameLog.ResetOnceKeys(); }
        [TearDown] public void Cleanup() { EventHub.Reset(); Services.Reset(); TestContentFactory.DestroyAll(); GameLog.ResetOnceKeys(); }

        sealed class Fixture : IDisposable
        {
            internal readonly TestGalaxyFixtures.Fixture Content = new(2, 0, 42, false);
            internal readonly EcsTestWorld Ecs = EcsTestWorld.Create(typeof(OriginRebaseSystem));
            internal EntityManager Em => Ecs.EntityManager;
            internal readonly ContentCatalog Catalog;
            internal readonly GalaxyService Galaxy;
            internal readonly StarSystemLoader Loader;
            internal readonly TravelService Travel;
            internal readonly EncounterDirectorService Encounters;
            internal readonly ShipSpawnService Spawner;
            internal readonly NarrativeService Narrative;
            internal readonly NarrativeLogicAdapter Adapter = new();
            internal readonly NarrativeBootstrap.StoryRuntime Story;
            internal readonly DialogService Dialog;
            internal readonly DialogPresenter Presenter;
            internal readonly UIRoot UI;
            internal readonly BodySpec Gate;
            internal readonly EncounterDefinition Merchant;
            internal readonly Entity Player, Combat;
            readonly GalaxyState state = new();
            readonly PauseService pause = new();
            readonly List<Entity> factionEntities = new();
            readonly GameSession session;
            readonly FactionService factions;
            readonly TerritoryService territory;
            readonly BlueprintLibrary blueprints;
            readonly VoxelStructureRegistry registry;
            readonly AudioService audio;
            readonly LogicWorldAdapter logicWorld;
            readonly NarrativeWorldEventBridge bridge;
            readonly InputService input;
            readonly InputServiceBridge inputBridge;
            readonly GameObject documentRoot;
            readonly DialogProgramDefinition program;
            readonly NativeQueue<ManagedEventEnvelope<OriginShifted>> originEvents;

            internal Fixture()
            {
                try
                {
                    Content.Create<FactionDefinition>("faction_ark", so =>
                    { so.FindProperty("isPlayerFaction").boolValue = true; so.FindProperty("relationLocked").boolValue = true; });
                    var traders = Content.Create<FactionDefinition>("faction_traders");
                    var encounterConfig = Content.Create<EncounterConfigDefinition>("config_encounters", so =>
                    { so.FindProperty("minPlayerDistanceM").floatValue = 1; so.FindProperty("bodyClearanceM").floatValue = 0; });
                    var ship = Content.Create<ShipBlueprintDefinition>("ship_merchant_fixture", so =>
                    { so.FindProperty("boundsMin").vector3IntValue = Vector3Int.zero; so.FindProperty("boundsMax").vector3IntValue = Vector3Int.one; });
                    Merchant = Content.Create<EncounterDefinition>("encounter_merchant_shady", so =>
                    {
                        so.FindProperty("kind").intValue = (int)EncounterKind.Merchant;
                        so.FindProperty("faction").objectReferenceValue = traders;
                        so.FindProperty("hailable").boolValue = true;
                        so.FindProperty("spawnDistanceMinM").floatValue = so.FindProperty("spawnDistanceMaxM").floatValue = 1000;
                        var ships = so.FindProperty("ships"); ships.arraySize = 1; var row = ships.GetArrayElementAtIndex(0);
                        row.FindPropertyRelative("blueprint").objectReferenceValue = ship;
                        row.FindPropertyRelative("countMin").intValue = row.FindPropertyRelative("countMax").intValue = 1;
                    });
                    var economyConfig = Content.Create<EconomyConfigDefinition>("config_economy");
                    var fuel = Content.Create<ItemDefinition>("resource_fuel");
                    Content.Create<TravelConfigDefinition>("config_travel", so => so.FindProperty("fuelItem").objectReferenceValue = fuel);
                    var gameConfig = Content.Create<GameConfigDefinition>("config_story_game");
                    var narrativeConfig = Content.Create<NarrativeConfigDefinition>("narrative_story");
                    Content.Definitions.Add(FactCatalogTests.Facts("facts_bridge", new FactDeclaration { Name = "gate_open", Kind = FactKind.Bool }));
                    LogicTestEnvironment.Source(Content.Generation.AuthoredLanes[0].condition, "$gate_open");
                    Content.Generation.AuthoredLanes[0].lockedMessage = "Gate is asleep.";
                    foreach (var body in new[] { Content.Star, Content.Planet, Content.Moon, Content.Gate })
                    {
                        using var so = new SerializedObject(body);
                        so.FindProperty("hasCollider").boolValue = false;
                        if (body == Content.Gate) so.FindProperty("storyTag").stringValue = "ancient_gate";
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                    var dormant = Content.Create<StoryBeatDefinition>("beat_gate_dormant", so =>
                    {
                        so.FindProperty("trigger").intValue = (int)BeatTrigger.Interact;
                        so.FindProperty("yarnNode").stringValue = "gate_dormant";
                        var tags = so.FindProperty("where.bodyTags"); tags.arraySize = 1; tags.GetArrayElementAtIndex(0).stringValue = "ancient_gate";
                    });
                    LogicTestEnvironment.Source(dormant.Condition, "!$gate_open");
                    Content.Create<StoryBeatDefinition>("beat_merchant_shady", so =>
                    {
                        so.FindProperty("trigger").intValue = (int)BeatTrigger.Hail;
                        so.FindProperty("where.hailEncounter").objectReferenceValue = Merchant;
                        so.FindProperty("yarnNode").stringValue = "merchant_shady";
                    });
                    Content.Create<StoryBeatDefinition>("beat_waiting", so =>
                    { so.FindProperty("trigger").intValue = (int)BeatTrigger.Manual; so.FindProperty("yarnNode").stringValue = "waiting"; });
                    var report = new ValidationReport("QST-008 real owners");
                    var initial = ContentCatalog.Build(Content.Definitions, report);
                    program = DialogProgramCompiler.Compile(new Dictionary<string, string> { ["bridge.yarn"] =
                        "title: gate_dormant\n---\nGate is dormant. #line:gate_dormant\n===\n" +
                        "title: merchant_shady\n---\nA merchant answers. #line:merchant_shady\n===\n" +
                        "title: waiting\n---\nWaiting beat. #line:waiting\n===\n" }, initial, report);
                    Assert.IsFalse(report.HasErrors, report.ToText()); Assert.IsNotNull(program);
                    Content.Definitions.Add(program);
                    Content.Create<DialogCommandTableDefinition>("dialog_command_table", so => so.FindProperty("sourcesHash").ulongValue = program.SourcesHash);
                    Catalog = ContentCatalog.Build(Content.Definitions, report);
                    var refs = RefResolverRegistry.CreateDefault(Catalog); var factCatalog = FactCatalog.Build(Catalog, refs, report);
                    var compiler = new LogicCompiler(factCatalog, refs, new LogicFunctionRegistry());
                    foreach (var definition in Content.Definitions)
                        if (definition is ILogicSourceOwner owner) owner.CompileLogic(compiler, report.For(definition.IdString));
                    Assert.IsFalse(report.HasErrors, report.ToText());

                    Ecs.AddGameplayActive(); Ecs.AddWorldSceneActive(); Ecs.AddSimClock(); Ecs.AddSingleton(default(PlayerControlLock));
                    originEvents = new NativeQueue<ManagedEventEnvelope<OriginShifted>>(Allocator.Persistent);
                    Ecs.AddSingleton(new ManagedEventQueue<OriginShifted> { Queue = originEvents });
                    Player = Em.CreateEntity(typeof(PlayerShip), typeof(LocalTransform), typeof(UniversePosition));
                    Em.SetComponentData(Player, LocalTransform.Identity);
                    Combat = Em.CreateEntity(typeof(PlayerCombatStatus));
                    FactionContent.CreateSingletons(Em, Catalog, encounterConfig, factionEntities);
                    factions = new FactionService(Ecs.World, Catalog); Services.Register(factions);
                    Galaxy = new GalaxyService(Catalog, state, Ecs.World, new GalaxyFrameTicker());
                    blueprints = new BlueprintLibrary(Catalog, default); registry = VoxelStructureRegistry.CreateRuntime(blueprints);
                    session = new GameSession(Ecs.World, gameConfig);
                    var docking = new DockingService(); Loader = new StarSystemLoader(Galaxy, Ecs.World, registry, blueprints, session, Catalog, docking);
                    audio = new AudioService(null, null, AudioVolumes.Default, null);
                    // Travel subscribes before the narrative bridge, exactly as in GameComposition.
                    Travel = new TravelService(Galaxy, Loader, docking, Ecs.World, Catalog, pause, audio);
                    territory = new TerritoryService(Galaxy, factions, Catalog);
                    Spawner = new ShipSpawnService(Ecs.World, registry, Catalog, session, Galaxy);
                    Encounters = new EncounterDirectorService(Galaxy, Loader, Travel, docking, Spawner, factions, territory, Ecs.World, Catalog) { Enabled = false };
                    Services.Register(territory); Services.Register(Loader); Services.Register(Encounters); Services.Register(Travel);
                    input = new InputService(new ControlSettingsData()); inputBridge = new InputServiceBridge(input);
                    documentRoot = new GameObject("QST-008 UI"); var document = documentRoot.AddComponent<UIDocument>();
                    UI = new UIRoot(document, AssetDatabase.LoadAssetAtPath<UIConfig>("Assets/Game/UI/Data/UIConfig.asset"),
                        pause, inputBridge, new NullUISoundSink(), new UnityFrameClock());
                    Services.Register(UI.Navigator);
                    logicWorld = new LogicWorldAdapter(Catalog, session, Galaxy, blueprints, audio, UI.Notifications, Ecs.World, narrativeConfig);
                    var economy = new EconomyService(Catalog, economyConfig, () => 42, () => 0);
                    economy.Inventory.SetCapacity(100); economy.GiveItems(Catalog.IndexOf(fuel), 100);
                    Narrative = NarrativeBootstrap.Build(Catalog, Content.Definitions,
                        facts => new LogicContext(facts, economy, Adapter, logicWorld, () => 42));
                    Services.Register(Narrative); Services.Register<ILogicContext>(Narrative.Context);
                    Dialog = NarrativeBootstrap.BuildDialog(Catalog, Narrative.Context); Adapter.BindDialog(Dialog); Services.Register(Dialog);
                    Presenter = new DialogPresenter(Dialog, Catalog, new DialogViewModel(), UI.Navigator, new NullUISoundSink()); Services.Register(Presenter);
                    Story = NarrativeBootstrap.BuildStory(Catalog, Narrative, Adapter, Dialog, 0);
                    bridge = NarrativeWorldEventBridge.CreateForWorld(Narrative, Catalog, Galaxy, Ecs.World, Travel, Encounters, Story.Beats, Dialog, UI.Navigator);
                    Galaxy.NewGame(42);
                    Narrative.OnSessionRestored();
                    state.CurrentStarSystem = Galaxy.Galaxy.StartStarSystem; state.HasPlayerLocation = true;
                    state.PlayerLocation = new PlayerLocation { StarSystem = state.CurrentStarSystem };
                    Loader.OnSceneReady(new SceneReadyContext(GameScene.World, SceneEntryReason.NewGame, default));
                    Ecs.UpdateSystem<OriginRebaseSystem>(); Ecs.CompleteAll(); Loader.Tick(new GalaxyFrameContext(Em, 0, 0, 0, 0));
                    Assert.IsTrue(Loader.IsReady);
                    Em.SetComponentData(Player, LocalTransform.FromPosition(new float3(120000, 0, -70000)));
                    foreach (var body in Galaxy.GetContent(Loader.Loaded).Bodies) if (body.Id.Role == BodyRole.LaneGate) { Gate = body; break; }
                    Assert.AreEqual(BodyRole.LaneGate, Gate.Id.Role);
                    Frame(); Assert.IsTrue(Narrative.PlayerCanTalk);
                }
                catch { Dispose(); throw; }
            }
            internal int Beat(string id) => Catalog.IndexOf(Catalog.Get<StoryBeatDefinition>(new ContentId(id)));
            internal void OpenGate() => Narrative.Facts.SetBySlot(Narrative.Facts.Catalog.SlotOf("gate_open"), 1, FactWriteSource.Debugger);
            internal void Interact(InteractionKind kind = InteractionKind.LaneGate, ulong member = 0, ulong? key = null)
            {
                EventBus<BodyInteracted>.Publish(new BodyInteracted(Loader.Loaded.Value, key ?? Gate.Id.LocalKey, (byte)kind, member));
                EventHub.DrainAll();
            }
            internal void Frame()
            {
                EventHub.DrainAll(); Ecs.World.GetOrCreateSystemManaged<NarrativeTickBridgeSystem>().Update();
            }
            internal ulong SpawnMerchant()
            {
                Assert.AreEqual(1, Encounters.SpawnEncounter(Merchant, default, 1, EncounterSource.Narrative));
                using var q = Em.CreateEntityQuery(typeof(Game.Galaxy.Interaction.Interactable), typeof(GridSpawnRequest));
                using var roots = q.ToEntityArray(Allocator.Temp); Assert.AreEqual(1, roots.Length);
                ulong id = Em.GetComponentData<GridSpawnRequest>(roots[0]).PersistentId;
                Assert.IsTrue(Encounters.TryGetInstanceByMember(id, out var info)); Assert.AreSame(Merchant, info.Definition);
                Assert.IsTrue(Spawner.TryGetRoot(new PersistentId(id), out _)); return id;
            }
            public void Dispose()
            {
                Ecs.CompleteAll(); bridge?.Dispose(); Dialog?.Interrupt(); Story?.Dispose(); Presenter?.Dispose(); UI?.Dispose();
                Services.Reset(); logicWorld?.Dispose(); Encounters?.Dispose(); Travel?.Dispose(); Loader?.Dispose(); Spawner?.Dispose();
                territory?.Dispose(); session?.Dispose(); registry?.Dispose(); if (registry != null) Object.DestroyImmediate(registry); blueprints?.Dispose();
                factions?.Dispose(); Galaxy?.Dispose(); state.Dispose();
                foreach (var entity in factionEntities)
                    if (Em.Exists(entity) && Em.HasComponent<FactionRelations>(entity)) Em.GetComponentData<FactionRelations>(entity).Dispose();
                Ecs.Dispose(); if (originEvents.IsCreated) originEvents.Dispose();
                inputBridge?.Dispose(); input?.Dispose(); pause.Dispose(); audio?.Dispose();
                if (documentRoot != null) Object.DestroyImmediate(documentRoot); if (program != null) Object.DestroyImmediate(program); Content.Dispose();
            }
        }

        [Test] public void LockedGate_PlaysBeat()
        {
            using var f = new Fixture();
            f.UI.Confirm.Show(new ConfirmRequest { Title = "Hold", CancelIndex = 0,
                Options = new[] { new ConfirmOption { Label = "Close", Enabled = true } } }, _ => { });
            Assert.IsTrue(f.Narrative.PlayerCanTalk, "The previous tick predates this modal.");
            f.Interact(); Assert.IsFalse(f.Dialog.IsActive, "The bridge must consult current modal state at delivery.");
            Assert.IsTrue(f.UI.Navigator.Pop(f.UI.Navigator.Top)); f.Interact();
            Assert.AreEqual(TravelPhase.Idle, f.Travel.Phase);
            Assert.IsTrue(f.Dialog.IsActive); Assert.AreEqual(f.Beat("beat_gate_dormant"), f.Story.Beats.CurrentBeatIndex);
            Assert.AreSame(f.Presenter.View, f.UI.Navigator.Top); Assert.AreEqual("Gate is dormant.", f.Presenter.Model.LineText);
            Assert.AreEqual("QUST", f.Story.Save.Key); Assert.AreSame(f.Adapter, f.Narrative.Context.Narrative);
            Assert.Greater(f.Story.ShipLog.Revision, 0);
        }
        [Test] public void OpenGate_NoBeat()
        {
            using var f = new Fixture(); f.OpenGate(); f.Interact();
            Assert.AreEqual(TravelPhase.Spooling, f.Travel.Phase); Assert.IsFalse(f.Dialog.IsActive);
            Assert.AreEqual(0, f.Story.Beats.CurrentBeatIndex); Assert.AreEqual(0, f.Story.Beats.QueuedCount);
            int slot = f.Narrative.Facts.Catalog.GeneratedSlot(FactFamily.Interacted, f.Content.Gate.Id.Hash.Value);
            Assert.AreEqual(0, f.Narrative.Facts.GetBySlot(slot), "Successful gate travel must bypass the story interaction counter too.");
        }
        [Test] public void Hail_PlaysMerchant()
        {
            using var f = new Fixture(); ulong member = f.SpawnMerchant();
            f.Interact(InteractionKind.Hail, member + 1000, 0); Assert.IsFalse(f.Dialog.IsActive);
            f.Interact(InteractionKind.Hail, member, 0);
            Assert.AreEqual(f.Beat("beat_merchant_shady"), f.Story.Beats.CurrentBeatIndex);
            Assert.IsTrue(f.Dialog.IsActive); Assert.AreEqual("A merchant answers.", f.Presenter.Model.LineText);
        }
        [Test] public void Salvage_Ignored()
        {
            using var f = new Fixture(); ulong member = f.SpawnMerchant();
            f.Interact(InteractionKind.Salvage); f.Interact(InteractionKind.Salvage, member, 0);
            Assert.AreEqual(0, f.Story.Beats.CurrentBeatIndex); Assert.IsFalse(f.Dialog.IsActive);
            var hulk = f.Em.CreateEntity(typeof(PersistentId), typeof(Hulk)); f.Em.SetComponentData(hulk, new PersistentId(9000));
            f.Interact(InteractionKind.Inspect, 9000);
            Assert.AreEqual(0, f.Story.Beats.CurrentBeatIndex);
            int slot = f.Narrative.Facts.Catalog.GeneratedSlot(FactFamily.Interacted, f.Content.Gate.Id.Hash.Value);
            Assert.AreEqual(0, f.Narrative.Facts.GetBySlot(slot), "Salvage and Hulk targets never enter narrative interaction routing.");
            f.Interact(InteractionKind.Inspect);
            Assert.AreEqual(f.Beat("beat_gate_dormant"), f.Story.Beats.CurrentBeatIndex, "The same live template does route to the real beat owner.");
        }
        [Test] public void Free_FalseInTravelAndCombat()
        {
            using (var f = new Fixture())
            {
                int waiting = f.Beat("beat_waiting"); f.Adapter.QueueBeat(waiting);
                f.Em.SetComponentData(f.Combat, new PlayerCombatStatus { HostilesEngaging = 1 }); f.Frame();
                Assert.AreEqual(0, f.Story.Beats.CurrentBeatIndex); Assert.AreEqual(1, f.Story.Beats.QueuedCount);
                Assert.IsTrue(f.Narrative.PlayerCanTalk, "Combat blocks automatic beats but permits an explicit interaction.");
                f.Interact(); Assert.AreEqual(f.Beat("beat_gate_dormant"), f.Story.Beats.CurrentBeatIndex);
                f.Dialog.Interrupt(); f.Em.SetComponentData(f.Combat, default(PlayerCombatStatus)); f.Frame();
                Assert.AreEqual(waiting, f.Story.Beats.CurrentBeatIndex); Assert.IsTrue(f.Dialog.IsActive);
            }
            EventHub.Reset();
            using (var f = new Fixture())
            {
                f.Adapter.QueueBeat(f.Beat("beat_waiting")); f.OpenGate(); f.Interact(); f.Frame();
                Assert.AreEqual(TravelPhase.Spooling, f.Travel.Phase); Assert.IsFalse(f.Narrative.PlayerCanTalk);
                Assert.AreEqual(0, f.Story.Beats.CurrentBeatIndex); Assert.AreEqual(1, f.Story.Beats.QueuedCount);
                f.Travel.OnSceneExiting(new SceneExitContext(GameScene.World, GameScene.MainMenu, SceneEntryReason.QuitToMenu));
                f.Frame(); Assert.AreEqual(f.Beat("beat_waiting"), f.Story.Beats.CurrentBeatIndex);
            }
        }
    }
}
