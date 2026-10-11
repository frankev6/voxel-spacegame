#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Game.Core;
using Unity.Entities;
using UnityEngine;

namespace Game.App.DevRuntime
{
    /// <summary>Live service/world access for commands, owned by the host (SPEC-70 §4).</summary>
    public sealed class DevContext
    {
        public DevRuntimeHost Host { get; }
        internal DevContext(DevRuntimeHost host) { Host = host; }
        public World World => World.DefaultGameObjectInjectionWorld;
        public EntityManager EntityManager => World != null && World.IsCreated ? World.EntityManager : default;
        public bool TryGetService<T>(out T service) where T : class => Game.Core.Services.TryGet(out service);
        public DevLogCapture Logs => Host.Logs;
        public Input.DevInputInjector Input => Host.Input;
        public Ui.DevUiQuery Ui => Host.Ui;
        public DevEventCounter Events => Host.Events;
        // CAM-004 and DEV-009 wire the camera service/player query when those contracts land (SPEC-70 Q13).
        public Camera GameplayCamera => null;
        public Entity PlayerShipRoot => Entity.Null;
    }
}
#endif
