using Game.App.Settings;
using Game.Core;
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
        internal static GameRoot Instance;                        // reset in SubsystemRegistration (Bootstrap)
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
            // TODO(TST-003): read Game.App.Testing.GameRootTestHooks (SaveRoot, SettingsPath, SkipIntroScreens) in the Editor and
            // development builds. SavePaths arrives with the SaveService line (PERS-005).
            var settingsPaths = SettingsPaths.Default;
            Composition = GameComposition.Build(this, config, settingsPaths);
        }

        void Start()
        {
            // TODO(FND-006): SceneFlow.StartupAsync() (§5.4.6).
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            // TODO(PERS-009): save.Autosave.Update(dt). TODO(FND-009): session.Tick(dt). TODO(UI-003): uiRoot.Tick(dt).
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
