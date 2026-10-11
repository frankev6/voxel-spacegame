#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Unity.Entities;
using UnityEngine;

namespace Game.App.DevRuntime
{
    /// <summary>What commands may touch. Created by DevRuntimeHost. Implements SPEC-70 §4. The Input (DevInputInjector), Ui (DevUiQuery),
    /// Logs (DevLogCapture) and Events (DevEventCounter) properties are added by DEV-003/DEV-004 together with their types.</summary>
    public sealed class DevContext
    {
        public World World => throw new System.NotImplementedException("DEV-003");                      // World.DefaultGameObjectInjectionWorld
        public EntityManager EntityManager => throw new System.NotImplementedException("DEV-003");
        public bool TryGetService<T>(out T service) where T : class { throw new System.NotImplementedException("DEV-003"); }   // wraps Services.TryGet
        public Camera GameplayCamera => throw new System.NotImplementedException("DEV-003");            // from SPEC-62 (open question 10); null outside the World scene
        public Entity PlayerShipRoot => throw new System.NotImplementedException("DEV-003");            // Entity.Null when none
    }
}
#endif
