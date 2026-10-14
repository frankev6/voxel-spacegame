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

            // GAL-008 installs and registers its real GalaxyService at row 8.1 before this point.
            // 8.3 FND-012: install both canonical surfaces only after that production dependency exists.
            if (Game.Core.Services.TryGet<GalaxyService>(out var galaxy))
            {
                Spawner = Push(new ShipSpawnService(world, Registry, Content.Catalog, Session, galaxy));
                Register(Spawner); Register<IShipSpawner>(Spawner);
            }
            // SPEC-30 permits absent lane/ownership services until their implementations are composed.
            // TODO(TRV-006/ENC-007): pass their actual interfaces here when those services are installed above.
            Register(new GalaxyMapModel(campaignGalaxy, travel, null));
            // Remaining travel/faction/economy/narrative bindings follow SPEC-01 §5.2.

            // 9. Persistent UI document and root (UI-003). Navigation owns pause/context (UI-004).
            var inputBridge = Push(new InputServiceBridge(Input)); Register(inputBridge);
            UI = Push(new UIRoot(config.UiDocument, config.UiConfig, Pause, inputBridge,
                new AppUiSoundSink(Audio, config.UiConfig), new UnityFrameClock()));
            Register(UI); Register(UI.Navigator); Register(config.UiDocument);
            Game.Core.Services.Register<IWorldPointerGate>(UI);
            world.GetExistingSystemManaged<SceneNavigationBridgeSystem>().Bind(Flow, Pause, UI);
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
            var cargoCapacity = Push(new Game.App.Economy.CargoCapacitySync(economy.Inventory, Session.Fleet, Content.Catalog, economyConfig[0]));
            Register(cargoCapacity); Session.RegisterNewGameInitializer(cargoCapacity);
            Register(new Game.App.Economy.MerchantDirectory(Content.Catalog, campaignGalaxy));
            Session.RegisterModel(economy); Session.RegisterNewGameInitializer(economy);
            Save.Register(new Game.Economy.InventorySaveParticipant(economy, Content.Catalog));
            var narrativeOwner = new Game.Narrative.ShipLog.NarrativeLogicAdapter();
            // TODO(QST-006): narrativeOwner routes remain task-owned shells until quest/beat services exist.
            var narrative = NarrativeBootstrap.Build(Content.Catalog, config.ContentDatabase.Definitions,
                facts => new LogicContext(facts, economy, narrativeOwner, logicWorld, () => Session.CampaignSeed));
            Register(narrative); Register(narrative.Facts); Register<Game.Data.Logic.ILogicContext>(narrative.Context);
            Save.Register(new Game.Narrative.FactSaveParticipant(narrative.Facts));
            Session.BindNarrative(narrative);
            Push(new NarrativeEconomyDependencies(economy, narrative));
            // TODO(QST-002/003/004/005): register implemented INarrativeSystem owners here before sessions begin.
            // TODO(QST-008): construct NarrativeWorldEventBridge.CreateForWorld after real beat/travel/encounter owners; do not instantiate their staged shells.

            // 10. App-lifetime UI models and implemented presenter bindings (HUD-002).
            Screens = Push(new UiComposition(UI));
            Register(Screens);
            // DLG-007 installs the actual runner/service before this point; DLG-009 owns the authored view.
            if (Game.Core.Services.TryGet<Game.Narrative.Dialog.DialogService>(out var dialog))
            {
                Screens.BindDialog(dialog, Content.Catalog, new AppUiSoundSink(Audio, config.UiConfig));
                Register(Screens.DialogPresenter);
            }
            // TODO(DLG-007): construct/register the production DialogService; do not use a substitute runner.
            // TODO(HUD-002): install real destination/editor bindings and replace the M1 adapter before SetRootScreens.
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
            Step("11 camera");

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
                // TODO(CAM-009): BlueprintLab enters editor orbit with its structure bounds.
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
            }
            LastDisposeLog.Clear();
            for (int i = DisposeStack.Count - 1; i >= 0; i--)
            {
                LastDisposeLog.Add(DisposeStack[i].GetType().Name);
                try { DisposeStack[i].Dispose(); }
                catch (Exception e) { GameLog.Exception(LogCategories.Boot, e, "GameComposition.Dispose " + DisposeStack[i].GetType().Name); }
            }
            DisposeStack.Clear();
            Screens = null; UI = null; Session = null;
            Settings = null; Content = null; Blueprints = null; Registry = null; Input = null; Audio = null; Camera = null; Fx = null; Pause = null; Save = null; GameConfig = null; Flow = null;
            Game.Core.Services.Reset();
        }
    }
}
