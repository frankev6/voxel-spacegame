using Game.Core;
using Unity.Entities;
using UnityEngine;

namespace Game.App.Boot
{
    /// <summary>BeforeSceneLoad entry (SPEC-01 §5.1, decisions 1–2): creates the Default World explicitly (Entities' automatic runtime
    /// bootstrap is disabled by the UNITY_DISABLE_AUTOMATIC_SYSTEM_BOOTSTRAP_RUNTIME_WORLD define), then the one GameRoot.</summary>
    internal static class Bootstrap
    {
        internal const string WorldName = "Default World";

        /// <summary>Step 0: domain reload is off, so statics are reset here.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { GameRoot.Instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        internal static void Boot()
        {
            if (GameRoot.Instance != null) return;
            if (World.DefaultGameObjectInjectionWorld == null) DefaultWorldInitialization.Initialize(WorldName, false);
            var prefab = Resources.Load<GameRoot>("GameRoot");                 // the one allowed Resources.Load (conventions §3.2)
            if (prefab == null)
            {
                GameLog.Error(LogCategories.Boot, "GameRoot prefab missing (Assets/Game/App/Resources/GameRoot.prefab)");
                throw new System.InvalidOperationException("GameRoot prefab missing");
            }
            var root = Object.Instantiate(prefab);
            root.name = "GameRoot";
            Object.DontDestroyOnLoad(root.gameObject);
        }
    }
}
