using Game.App.Settings;
using Game.Core;
using Game.Persistence.Services;
using Game.Core.Events;
using Unity.Entities;
using UnityEngine;

namespace Game.App.Boot
{
    /// <summary>The persistent root object (Assets/Game/App/Resources/GameRoot.prefab): builds the composition in Awake and tears everything
    /// down in OnDestroy. Implements SPEC-01 §4.5, §5.1.</summary>
    public sealed class GameRoot : MonoBehaviour
    {
        [SerializeField] private GameRootConfig config;
        [StaticStateOk("reset by Bootstrap.ResetStatics in SubsystemRegistration")] internal static GameRoot Instance;
        internal GameComposition Composition { get; private set; }
        internal GameRootConfig Config => config;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                GameLog.Error(LogCategories.Boot, "a second GameRoot was created; destroying it");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // §5.1: test hooks redirect both paths in the Editor and development builds; release builds use the defaults.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var savePaths = Game.App.Testing.GameRootTestHooks.SaveRoot != null ? new SavePaths(Game.App.Testing.GameRootTestHooks.SaveRoot) : SavePaths.CreateDefault();
            var settingsPaths = Game.App.Testing.GameRootTestHooks.SettingsPath != null ? new SettingsPaths(Game.App.Testing.GameRootTestHooks.SettingsPath) : SettingsPaths.Default;
#else
            var savePaths = SavePaths.CreateDefault();
            var settingsPaths = SettingsPaths.Default;
#endif
            Composition = GameComposition.Build(transform, config, savePaths, settingsPaths);
        }

        async void Start()
        {
            if (Composition?.Flow == null) return;
            try { await Composition.Flow.StartupAsync(); }                 // §5.4.6; failures recover inside SceneFlow
            catch (System.Exception e) { GameLog.Exception(LogCategories.Flow, e, "SceneFlow.StartupAsync"); }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            Composition?.Session?.Tick(dt);
            Composition?.Save?.Autosave.Update(dt);
            Composition?.Screens?.Tick();
            Composition?.Defeat?.Tick(dt);
            Composition?.UI?.Tick(dt);
            Composition?.Settings?.Tick(dt);                      // debounced save
        }

        void OnApplicationQuit() { Composition?.Settings?.Flush(); }

        void OnDestroy()
        {
            if (Instance != this) return;
            try { Composition?.Dispose(); }
            finally
            {
                Composition = null;
                EventHub.Reset();
                var world = World.DefaultGameObjectInjectionWorld;
                if (world != null && world.IsCreated)
                {
                    ScriptBehaviourUpdateOrder.RemoveWorldFromCurrentPlayerLoop(world);
                    world.Dispose();
                }
                Instance = null;
            }
        }
    }
}
