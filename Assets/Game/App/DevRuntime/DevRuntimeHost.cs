#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace Game.App.DevRuntime
{
    /// <summary>Thin host of the dev runtime: commands, input injection, UI query, logs, overlays, console, operations and scenario runners
    /// (SPEC-70 §4). Implements SPEC-70 §4 (shell; DEV-003 adds the owned parts and the behaviour).</summary>
    public sealed class DevRuntimeHost : MonoBehaviour
    {
        public static DevRuntimeHost Create(Transform parent) { throw new System.NotImplementedException("DEV-003"); }
    }
}
#endif
