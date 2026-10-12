using System;
using System.Collections.Generic;
using Game.App.Flow;
using Game.App.Session;
using Game.Core.Flow;
using Game.App.Services;
using Game.App.Settings;
using Game.Core;
using Game.Core.Audio;
using Game.Presentation.Audio;
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
        internal UIRoot UI { get; private set; }
        internal PauseService Pause { get; private set; }
        internal SaveService Save { get; private set; }
        internal GameConfigDefinition GameConfig { get; private set; }
        internal SceneFlow Flow { get; private set; }
        internal GameSession Session { get; private set; }

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
            Session.BindFleet(Blueprints, Registry, Content.Catalog); Save.Register(Session.FleetParticipant);
            Register(Session.Fleet);

            // 8.1–13: galaxy, travel, spawner, factions, map, economy, narrative, UI, camera, FX and dialog are added by their tasks, in the
            //    rows of SPEC-01 §5.2 (GAL-008, TRV-006, FND-012, ENC-007, GAL-011, ECO-008, UI-003, …).

            // 9. Persistent UI document and root (UI-003). Stack/context ownership follows in UI-004.
            var inputBridge = Push(new InputServiceBridge(Input)); Register(inputBridge);
            UI = Push(new UIRoot(config.UiDocument, config.UiConfig, Pause, inputBridge,
                new AppUiSoundSink(Audio, config.UiConfig), new UnityFrameClock()));
            Register(UI); Register(UI.Navigator); Register(config.UiDocument);
            // TODO(UI-004): unbind the fallback before inputBridge.BindBack(UI.HandleBackPressed).
            Step("9 ui");

            // 9.1 Flight input. UI-005 rebinds the gate when pointer picking is implemented; FND-017 supplies the pre-UI scene context bridge.
            world.GetExistingSystemManaged<PlayerInputSampleSystem>().Bind(Input, null);
            world.GetExistingSystemManaged<SceneInputContextBridgeSystem>().Bind(Flow, Pause, Input);

            // 11. Camera (CAM-004/005), with context-gated input.
            Camera = Push(new CameraRigService(config.CameraConfig, root.Find("CameraRoot") ?? root, new InputCameraInput(Input)));
            Register(Camera);
            world.GetExistingSystemManaged<CameraTargetSyncSystem>().Bind(Camera, null); // CAM-008 supplies the FX reader.
            Audio.SetListenerTarget(Camera.ListenerPivot);
            Session.SetThumbnailCamera(Camera.Camera);
            Flow.RegisterReady(new CameraSceneHook(Camera));
            Step("11 camera");

            // 14. Settings applier (FND-004): follows SettingsChanged, then pushes every section once (SPEC-01 open question 7).
            Push(new SettingsApplier(Settings));
            SettingsApplier.ApplyAll(Settings.Current);
            Step("14 settings applier");

            // 15. Boot entity snapshot (FND-006, decision 5).
            Flow.SnapshotBootEntities();
            Step("15 snapshot");

            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            Register(Push(Game.App.DevRuntime.DevRuntimeHost.Create(root)));
            Step("16 dev runtime");
#endif
        }

        void Step(string name)
        {
            StepLog.Add(name);
            if (FailAfterStep != null && FailAfterStep == name) throw new InvalidOperationException("GameComposition test failure after step " + name);
        }

        static void Register<T>(T service) where T : class => Game.Core.Services.Register(service);

        T Push<T>(T service) where T : IDisposable
        {
            DisposeStack.Add(service);
            return service;
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
                world.GetExistingSystemManaged<SceneInputContextBridgeSystem>()?.Unbind();
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
            Settings = null; Content = null; Blueprints = null; Registry = null; Input = null; Audio = null; Camera = null; Pause = null; Save = null; GameConfig = null; Flow = null;
            Game.Core.Services.Reset();
        }
    }
}
