using System;
using System.Collections.Generic;
using Game.App.Flow;
using Game.App.Presenters;
using Game.App.Session;
using Game.App.Spawning;
using Game.Galaxy.Services;
using Game.Core.Flow;
using Game.App.Services;
using Game.App.Settings;
using Game.Core;
using Game.Core.Audio;
using Game.Presentation.Audio;
using Game.Presentation.Vfx;
using Game.Presentation.Services;
using Game.Presentation.Input;
using Game.Presentation.Systems;
using Game.Core.Settings;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Input;
using Game.UI.Services;
using Game.Persistence.Container;
using Game.Persistence.Data;
using Game.Persistence.Services;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Unity.Entities;
using UnityEngine;

namespace Game.App.Boot
{
    /// <summary>Builds every service in the order of SPEC-01 §5.2 and disposes them in reverse. Implements SPEC-01 §4.5, §5.2. A step whose
    /// type does not exist yet is a comment naming the task that adds it; each task adds exactly its own line.</summary>
    internal sealed class GameComposition : IDisposable
    {
        internal DefeatWatcher Defeat { get; private set; }
        internal readonly List<IDisposable> DisposeStack = new List<IDisposable>();

        /// <summary>Step names in construction order (FND-005 test hook).</summary>
        internal readonly List<string> StepLog = new List<string>();
        /// <summary>Type names in disposal order of the last Dispose (FND-005 test hook).</summary>
        [StaticStateOk("test hook, rewritten by every Dispose")] internal static readonly List<string> LastDisposeLog = new List<string>();
        /// <summary>Test hook: Build throws after the step with this name. Reset by tests.</summary>
        [StaticStateOk("test hook, reset by CompositionTests")] internal static string FailAfterStep;
        /// <summary>Test hook: a failed Build stops Play Mode in the Editor unless a test turns this off.</summary>
        [StaticStateOk("test hook, reset by CompositionTests")] internal static bool StopPlayModeOnFailure = true;

        internal SettingsService Settings { get; private set; }
        internal ContentService Content { get; private set; }
        internal BlueprintLibrary Blueprints { get; private set; }
        internal VoxelStructureRegistry Registry { get; private set; }
        internal InputService Input { get; private set; }
        internal AudioService Audio { get; private set; }
        internal CameraRigService Camera { get; private set; }
        internal FxRig Fx { get; private set; }
        internal UIRoot UI { get; private set; }
        internal UiComposition Screens { get; private set; }
        internal PauseService Pause { get; private set; }
        internal SaveService Save { get; private set; }
        internal GameConfigDefinition GameConfig { get; private set; }
        internal SceneFlow Flow { get; private set; }
        internal GameSession Session { get; private set; }
        internal ShipSpawnService Spawner { get; private set; }
        internal CoopSession Coop { get; private set; }

        GameComposition() { }

        internal static GameComposition Build(Transform root, GameRootConfig config, SavePaths savePaths, SettingsPaths settingsPaths)
        {
            var c = new GameComposition();
            try
            {
                c.BuildSteps(root, config, savePaths, settingsPaths);
                return c;
            }
            catch (Exception e)
            {
                c.Dispose();
                GameLog.Exception(LogCategories.Boot, e, "GameComposition.Build");
#if UNITY_EDITOR
                if (StopPlayModeOnFailure) UnityEditor.EditorApplication.isPlaying = false;
#endif
                throw;
            }
        }

        void BuildSteps(Transform root, GameRootConfig config, SavePaths savePaths, SettingsPaths settingsPaths)
        {
            var world = World.DefaultGameObjectInjectionWorld ?? throw new InvalidOperationException("GameComposition: no Default World (Bootstrap creates it first)");
            var em = world.EntityManager;

            // 1. Settings (FND-004).
            Settings = Push(new SettingsService(new NewtonsoftSaveJsonCodec(), settingsPaths));
            Settings.Load();
            Register(Settings); Register<ISettingsService>(Settings);
            Step("1 settings");

            // 2. Content (FND-005, DATA-007): validates and throws ContentValidationException, so boot fails loudly.
            if (config?.ContentDatabase == null) throw new InvalidOperationException("GameRootConfig.ContentDatabase is not set on the GameRoot prefab");
            Content = Push(new ContentService(config.ContentDatabase));
            Content.CreateSingletons(em);
            Push(BootSingletons.CreateAll(em, Content.Catalog, Content.PropMeshes));
            Register(Game.Simulation.Factions.FactionIndexMap.Build(Content.Catalog));
            Register(Content);
            Content.Catalog.TryGet(new ContentId("config_game"), out GameConfigDefinition gameConfig);
            GameConfig = gameConfig;                                       // FND-E003 guarantees exactly one in a validated database
            Blueprints = Push(new BlueprintLibrary(Content.Catalog, new VoxelContent { Blocks = Content.BlockDefs, Orientations = Content.Orientations }));
            Register(Blueprints);
            Registry = VoxelStructureRegistry.CreateRuntime(Blueprints);
            Push(new RegistryLifetime(Registry)); Register(Registry);
            var registryEntity = em.CreateSingleton(new VoxelStructureRegistryRef { Value = Registry });
            em.AddComponent<BootSingleton>(registryEntity);
            Step("2 content");

            // 3. Input (INP-002).
            Input = Push(new InputService(Settings.Current.Controls));
            Register(Input);
            Step("3 input");

            // 4. Audio core exists in M0; cue/config content lands in AUD-007 (SPEC-64 Q14).
            Content.Catalog.TryGet(new ContentId("config_audio"), out AudioConfigDefinition audioConfig);
            Audio = Push(new AudioService(audioConfig, config.ContentDatabase, SettingsMapping.ToVolumes(Settings.Current.Audio), root));
            Audio.BindTo(world); Register(Audio); Register<IAudioService>(Audio);
            Step("4 audio");

            // 5. Pause (FND-003), shared by audio (AUD-003).
            Pause = Push(new PauseService());
            Audio.BindPause(Pause);
            Register(Pause); Register<IPauseService>(Pause);
            Step("5 pause");

            // 6. Save (PERS-002 profiles and slots; PERS-005 adds the pipeline).
            Save = Push(new SaveService(savePaths, Content, Pause, new SaveServiceConfig()));
            Register(Save);
            Step("6 save");

            // 7. Scene flow (FND-006).
            Flow = Push(new SceneFlow(world, Pause, Save, GameConfig));
            Register(Flow);
            Flow.RegisterExiting(Registry);
            Step("7 flow");

            Session = Push(new GameSession(Flow, Save, world, Content, GameConfig));
            Save.AttachContext(Session); Save.Register(new SessionSaveParticipant(Session));
            Register(Session); Register<IGameFlow>(Session); Register<IPersistentIdAllocator>(Session);
            Step("8 session");
            var factions = Push(new FactionService(world, Content.Catalog));
            Register(factions); Session.RegisterNewGameInitializer(factions);
            Session.RegisterModel(new FactionSessionModel(factions));
            Save.Register(new FactionSaveParticipant(factions, Content.Catalog));
            var galaxyTicker = new GalaxyFrameTicker(); Register(galaxyTicker);
            var campaignGalaxy = Push(new GalaxyService(Content.Catalog, Session.Galaxy, world, galaxyTicker));
            Register(campaignGalaxy); Session.BindGalaxy(campaignGalaxy);
            // ENC-014 owns entry/reset subscriptions; dispose it before its galaxy/faction dependencies.
            var territory = Push(new TerritoryService(campaignGalaxy, factions, Content.Catalog));
            Register(territory); Register<IStarSystemOwnership>(territory); galaxyTicker.Register(territory);
            Save.Register(new GalaxySaveParticipant(Session.Galaxy, Content.Catalog));
            var docking = new DockingService(); Register(docking);
            var celestialVisuals = world.GetOrCreateSystemManaged<Game.Presentation.World.CelestialVisualSystem>();
            var systemLoader = Push(new StarSystemLoader(campaignGalaxy, world, Registry, Blueprints, Session, Content.Catalog, docking, celestialVisuals));
            Register(systemLoader); galaxyTicker.Register(systemLoader);
            Flow.RegisterReady(systemLoader); Flow.RegisterExiting(systemLoader);
            Session.RegisterWriteback(new GalaxyFlushWriteback(systemLoader));
            Save.Register(new PlayerLocationSaveParticipant(Session.Galaxy, docking));
            var travel = Push(new TravelService(campaignGalaxy, systemLoader, docking, world, Content.Catalog, Pause, Audio));
            Register(travel); Register<ILaneAccess>(travel); galaxyTicker.Register(travel);
            Session.RegisterSaveBlocker(travel); Flow.RegisterExiting(travel);

            Session.BindFleet(Blueprints, Registry, Content.Catalog); Save.Register(Session.FleetParticipant);
            Register(Session.Fleet);
            var rangeConfig = Content.Catalog.Get<FiringRangeConfigDefinition>(new ContentId("config_firing_range"));
            var range = Push(new Game.App.Session.FiringRangeService(world, Content.Catalog, new VoxelContent { Blocks = Content.BlockDefs, Orientations = Content.Orientations }, Registry, Session, rangeConfig));
            Register(range); Flow.RegisterReady(range); Flow.RegisterExiting(range);
            Flow.RegisterReady(new Game.App.Session.MainMenuShowcase(Registry, Content, config.ShipEditorConfig ? config.ShipEditorConfig.BrickMaterial : null));

            // GAL-008 installs and registers its real GalaxyService at row 8.1 before this point.
            // 8.3 FND-012: install both canonical surfaces only after that production dependency exists.
            if (Game.Core.Services.TryGet<GalaxyService>(out var galaxy))
            {
                Spawner = Push(new ShipSpawnService(world, Registry, Content.Catalog, Session, galaxy));
                Register(Spawner); Register<IShipSpawner>(Spawner);
            }
            // Co-op spike (ADR-0116): the role singleton is a boot entity, so it survives scene changes; absent or Solo = single-player.
            var roleEntity = em.CreateSingleton(new Game.Core.Net.SimRole { Value = Game.Core.Net.SimRoleKind.Solo });
            em.AddComponent<BootSingleton>(roleEntity);
            Coop = Push(new CoopSession(world, Flow, Session, Registry, Pause, GameConfig, () => UI));
            Register(Coop); Register<Game.Core.Net.ICoopSession>(Coop);
            var encounters = Push(new EncounterDirectorService(campaignGalaxy, systemLoader, travel, docking, Spawner, factions, territory, world, Content.Catalog));
            Register(encounters); galaxyTicker.Register(encounters); Flow.RegisterExiting(encounters);
            Save.Register(new EncounterSaveParticipant(encounters, Content.Catalog));
            Register(new GalaxyMapModel(campaignGalaxy, travel, territory));
            // Remaining travel/faction/economy/narrative bindings follow SPEC-01 §5.2.

            // 9. Persistent UI document and root (UI-003). Navigation owns pause/context (UI-004).
            var inputBridge = Push(new InputServiceBridge(Input)); Register(inputBridge);
            UI = Push(new UIRoot(config.UiDocument, config.UiConfig, Pause, inputBridge,
                new AppUiSoundSink(Audio, config.UiConfig), new UnityFrameClock()));
            Register(UI); Register(UI.Navigator); Register(config.UiDocument);
            Game.Core.Services.Register<IWorldPointerGate>(UI);
            inputBridge.BindBack(UI.HandleBackPressed);
            Step("9 ui");

            // Narrative owners are bound after the real UI exists, before any session can start.
            var narrativeConfig = Content.Catalog.All<NarrativeConfigDefinition>();
            var economyConfig = Content.Catalog.All<EconomyConfigDefinition>();
            if (narrativeConfig.Count != 1 || economyConfig.Count != 1)
                throw new InvalidOperationException("Narrative boot requires exactly one narrative and economy config.");
            var logicWorld = Push(new LogicWorldAdapter(Content.Catalog, Session, campaignGalaxy, Blueprints,
                Audio, UI.Notifications, world, narrativeConfig[0]));
            var economy = new Game.Economy.EconomyService(Content.Catalog, economyConfig[0],
                () => Session.CampaignSeed, () => logicWorld.UniversalTime);
            Register(economy); Register<Game.Data.Logic.ILogicEconomy>(economy);
            Register(economy.Inventory); Register<Game.Data.Economy.ICargo>(economy.Inventory);
            Register(economy.Wallet); Register(economy.Merchants);
            var salvage = Push(new Game.Galaxy.Salvage.SalvageService(world, Content.Catalog, economyConfig[0], Audio));
            Register(salvage);
            var cargoCapacity = Push(new Game.App.Economy.CargoCapacitySync(economy.Inventory, Session.Fleet, Content.Catalog, economyConfig[0]));
            Register(cargoCapacity); Session.RegisterNewGameInitializer(cargoCapacity);
            var merchants = new Game.App.Economy.MerchantDirectory(Content.Catalog, campaignGalaxy); Register(merchants);
            Session.RegisterModel(economy); Session.RegisterNewGameInitializer(economy);
            Save.Register(new Game.Economy.InventorySaveParticipant(economy, Content.Catalog));
            var narrativeOwner = new Game.Narrative.ShipLog.NarrativeLogicAdapter();
            var narrative = NarrativeBootstrap.Build(Content.Catalog, config.ContentDatabase.Definitions,
                facts => new LogicContext(facts, economy, narrativeOwner, logicWorld, () => Session.CampaignSeed));
            Register(narrative); Register(narrative.Facts); Register<Game.Data.Logic.ILogicContext>(narrative.Context);
            Save.Register(new Game.Narrative.FactSaveParticipant(narrative.Facts));
            Session.BindNarrative(narrative);
            var dialogService = NarrativeBootstrap.BuildDialog(Content.Catalog, narrative.Context);
            narrativeOwner.BindDialog(dialogService); Register(dialogService);
            Save.Register(new Game.Narrative.Dialog.DialogSaveParticipant(dialogService.Memory));
            Session.RegisterSaveBlocker(Push(new DialogSessionBinding(dialogService)));
            Push(new NarrativeEconomyDependencies(economy, narrative));

            // 10. App-lifetime UI models and implemented presenter bindings (HUD-002).
            Screens = Push(new UiComposition(UI));
            Register(Screens);
            var merchantLauncher = new Game.App.Economy.MerchantLauncher(economy, merchants, factions, territory, encounters, systemLoader, UI.Navigator);
            Register(merchantLauncher);
            Screens.BindDialog(dialogService, Content.Catalog, new AppUiSoundSink(Audio, config.UiConfig));
            Register(Screens.DialogPresenter);
            var story = Push(NarrativeBootstrap.BuildStory(Content.Catalog, narrative, narrativeOwner, dialogService, Session.CampaignSeed));
            Register(narrativeOwner); Register<Game.Data.Logic.ILogicNarrative>(narrativeOwner);
            Register(story.Quests); Register(story.Clues); Register(story.Beats); Register(story.ShipLog);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            story.Beats.QuietCampaigns = Game.App.Testing.GameRootTestHooks.SkipCampaignOpening;
#endif
            Save.Register(story.Save);
            Screens.BindMapPresenters(world);
            Screens.BindShipLogQuery(story.ShipLog);
            Screens.BindShipLogMapNavigation(id => Screens.ShowGalaxyMapSelected(Game.Galaxy.Data.StarSystemId.FromAuthored(id.Hash)));
            // Travel subscribed first: a successful gate jump is already Spooling when this bridge sees it.
            Push(NarrativeWorldEventBridge.CreateForWorld(narrative, Content.Catalog, campaignGalaxy,
                world, travel, encounters, story.Beats, dialogService, UI.Navigator));
            Step("10 ui composition");

            // 9.1 Flight input uses the live UI pointer gate; ADR-0047 supplies scene roots through the navigator.
            world.GetExistingSystemManaged<PlayerInputSampleSystem>().Bind(Input, UI);

            // 11. Camera (CAM-004/005), with context-gated input.
            Camera = Push(new CameraRigService(config.CameraConfig, root.Find("CameraRoot") ?? root, new InputCameraInput(Input)));
            Register(Camera);
            world.GetExistingSystemManaged<CameraTargetSyncSystem>().Bind(Camera,
                new FxFrameReader(world.GetExistingSystemManaged<FxUploadSystem>()));
            Audio.SetListenerTarget(Camera.ListenerPivot);
            Session.SetThumbnailCamera(Camera.Camera);
            Flow.RegisterReady(new CameraSceneHook(Camera));
            // Persistent editor bindings and real scene roots (ADR-0098).
            var blueprintStore = Push(new Game.App.EditorHost.ActiveProfileBlueprintStore(Save, savePaths, Content, config.ShipEditorConfig));
            var editorServices = new Game.App.EditorHost.EditorHostServices(UI, Input, Camera, Content, config.ShipEditorConfig, blueprintStore);
            editorServices.ShowSettings = Screens.ShowSettings;
            Register(editorServices);
            var editorLauncher = Push(new Game.App.EditorHost.ShipEditorLauncher(Session, Flow, world, docking, economy, editorServices));
            Register(editorLauncher);
            Screens.BindEditorLauncher(editorLauncher);
            Screens.BindDockLaunchers(merchantLauncher, editorLauncher);
            Screens.BindMenuNavigation(Screens.ShowSaveSlots, editorLauncher.OpenLab, Screens.ShowSettings);
            var hudSync = world.GetOrCreateSystemManaged<HudSyncSystem>();
            var hudNames = new Game.Presentation.Presenters.HudNames(Content.Catalog,
                Game.Core.Services.Get<Game.Simulation.Factions.FactionIndexMap>(), campaignGalaxy, travel, encounters);
            hudSync.Bind(Screens.HudModel, hudNames, Input.GetBindingDisplay);
            world.GetOrCreateSystemManaged<WorldOverlaySystem>().Bind(UI.GetLayer(Game.UI.Data.UILayer.World), hudNames, Camera.Camera);
            world.GetOrCreateSystemManaged<HitMarkerSystem>().Bind(UI.GetLayer(Game.UI.Data.UILayer.World), Camera.Camera,
                new FxFrameReader(world.GetExistingSystemManaged<FxUploadSystem>()));
            Input.BindingsChanged += hudSync.RefreshBindings;
            Screens.BindSceneRoots(hudSync, Input.GetBindingDisplay, editorServices);
            Screens.BindHudPresenters(Input, Content.Catalog);
            Screens.BindHudObjectiveQuery(story.ShipLog);
            world.GetExistingSystemManaged<SceneNavigationBridgeSystem>().Bind(Flow, Pause, UI);
            Defeat = Push(new DefeatWatcher(Session, Save, UI, GameConfig));
            Defeat.Bind();
            Step("11 camera and scene roots");

            // 12. Authored VFX. Null graphics runs retain the unbound drain instead of allocating GPU resources.
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                var vfxConfig = Content.Catalog.Get<VfxConfigDefinition>(new ContentId("config_vfx"));
                var paletteDefinition = Content.Catalog.Get<VoxelPaletteDefinition>(new ContentId("config_voxel_palette"));
                var palette = new Color32[paletteDefinition.Colors.Count];
                for (int i = 0; i < palette.Length; i++) palette[i] = paletteDefinition.Colors[i];
                Fx = FxRig.Create(root, vfxConfig, Content.Catalog.All<FxDefinition>(), palette, (VfxQuality)Settings.Current.Video.VfxQuality);
                Push(new FxLifetime(Fx)); Fx.BindTo(world); Register(Fx);
            }
            Step("12 fx");

            // 14. Settings applier (FND-004): follows SettingsChanged, then pushes every section once (SPEC-01 open question 7).
            Push(new SettingsApplier(Settings));
            SettingsApplier.ApplyAll(Settings.Current);
            Step("14 settings applier");

            // 15. Boot entity snapshot (FND-006, decision 5).
            Flow.SnapshotBootEntities();
            Step("15 snapshot");

            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            var devHost = Push(Game.App.DevRuntime.DevRuntimeHost.Create(root));
            devHost.BindConsole(Input, UI); Register(devHost);
            Step("16 dev runtime");
#endif
        }

        void Step(string name)
        {
            StepLog.Add(name);
            if (FailAfterStep != null && FailAfterStep == name) throw new InvalidOperationException("GameComposition test failure after step " + name);
        }

        sealed class FactionSessionModel : ISessionModel
        {
            readonly FactionService factions;
            internal FactionSessionModel(FactionService factions) { this.factions = factions; }
            public void ResetToEmpty() => factions.InitializeNewGame(default);
        }
        static void Register<T>(T service) where T : class => Game.Core.Services.Register(service);

        T Push<T>(T service) where T : IDisposable
        {
            DisposeStack.Add(service);
            return service;
        }

        sealed class FxLifetime : IDisposable
        {
            readonly FxRig rig;
            public FxLifetime(FxRig rig) { this.rig = rig; }
            public void Dispose() { if (rig == null) return; rig.Dispose(); UnityEngine.Object.Destroy(rig.gameObject); }
        }

        sealed class NarrativeEconomyDependencies : IDisposable
        {
            readonly Game.Data.Logic.ILogicEconomy economy;
            readonly Game.Narrative.NarrativeService narrative;
            internal NarrativeEconomyDependencies(Game.Data.Logic.ILogicEconomy economy, Game.Narrative.NarrativeService narrative)
            {
                this.economy = economy; this.narrative = narrative;
                economy.ItemCountChanged += ItemChanged; economy.CreditsChanged += CreditsChanged;
            }
            void ItemChanged(int index) => narrative.NotifyDependency(new Game.Data.Logic.LogicDependency(Game.Data.Logic.LogicDependencyKind.Item, index));
            void CreditsChanged() => narrative.NotifyDependency(new Game.Data.Logic.LogicDependency(Game.Data.Logic.LogicDependencyKind.Credits, 0));
            public void Dispose() { economy.ItemCountChanged -= ItemChanged; economy.CreditsChanged -= CreditsChanged; }
        }

        sealed class CameraSceneHook : ISceneReady
        {
            readonly CameraRigService rig;
            public CameraSceneHook(CameraRigService rig) { this.rig = rig; }
            public int SceneReadyOrder => SceneHookOrder.Camera;
            public void OnSceneReady(in SceneReadyContext context)
            {
                rig.SetMode(context.Scene == GameScene.MainMenu ? Game.Presentation.Data.CameraMode.Menu
                    : context.Scene == GameScene.World ? Game.Presentation.Data.CameraMode.Flight
                    : Game.Presentation.Data.CameraMode.Hold, cut: true);
                // BlueprintLab: ShipEditorHost enters editor orbit with its structure bounds.
            }
            public bool IsSceneReadyComplete(in SceneReadyContext context) => true;
        }

        sealed class RegistryLifetime : IDisposable
        {
            readonly VoxelStructureRegistry registry;
            public RegistryLifetime(VoxelStructureRegistry registry) { this.registry = registry; }
            public void Dispose()
            {
                if (registry == null) return;
                registry.Dispose();
                if (Application.isPlaying) UnityEngine.Object.Destroy(registry);
                else UnityEngine.Object.DestroyImmediate(registry);
            }
        }

        /// <summary>Disposes DisposeStack in reverse (one failure cannot leak the rest), then Services.Reset.</summary>
        public void Dispose()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            try { if (world != null && world.IsCreated) world.EntityManager.CompleteAllTrackedJobs(); }
            catch (Exception e) { GameLog.Exception(LogCategories.Boot, e, "GameComposition.Dispose jobs"); }
            // Borrowed fleet structures must be unpinned before their owners dispose them.
            if (Registry != null) Registry.CancelSpawnReads();
            if (Camera != null) Audio?.ClearListenerTarget(Camera.ListenerPivot);
            if (world != null && world.IsCreated)
            {
                world.GetExistingSystemManaged<SceneNavigationBridgeSystem>()?.Unbind();
                world.GetExistingSystemManaged<PlayerInputSampleSystem>()?.Unbind();
                var hud = world.GetExistingSystemManaged<HudSyncSystem>();
                if (hud != null) { if (Input != null) Input.BindingsChanged -= hud.RefreshBindings; hud.Unbind(); }
                world.GetExistingSystemManaged<WorldOverlaySystem>()?.Unbind();
                world.GetExistingSystemManaged<HitMarkerSystem>()?.Unbind();
            }
            LastDisposeLog.Clear();
            for (int i = DisposeStack.Count - 1; i >= 0; i--)
            {
                LastDisposeLog.Add(DisposeStack[i].GetType().Name);
                try { DisposeStack[i].Dispose(); }
                catch (Exception e) { GameLog.Exception(LogCategories.Boot, e, "GameComposition.Dispose " + DisposeStack[i].GetType().Name); }
            }
            DisposeStack.Clear();
            Screens = null; UI = null; Session = null; Coop = null;
            Settings = null; Content = null; Blueprints = null; Registry = null; Input = null; Audio = null; Camera = null; Fx = null; Pause = null; Save = null; GameConfig = null; Flow = null;
            Game.Core.Services.Reset();
        }
    }
}
