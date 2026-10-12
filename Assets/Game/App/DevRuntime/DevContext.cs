#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Game.App.Boot;
using Game.Core.Components;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.App.DevRuntime
{
    /// <summary>Live dependencies and per-scenario counter baselines; owns cached probe queries (SPEC-70 §4/§6.2).</summary>
    public sealed class DevContext : IDisposable
    {
        readonly Dictionary<TypeIndex, EntityQuery> queries = new Dictionary<TypeIndex, EntityQuery>();
        readonly Dictionary<string, TypeIndex> componentTypes = new Dictionary<string, TypeIndex>(StringComparer.Ordinal);
        readonly List<GameObject> sceneRoots = new List<GameObject>();
        readonly List<AudioListener> listeners = new List<AudioListener>();
        readonly Dictionary<string, long> eventBaseline;
        readonly long errors, exceptions, warnings, logSequence;
        World queryWorld;
        bool typesLoaded;
        public DevRuntimeHost Host { get; }
        internal DevContext(DevRuntimeHost host, bool snapshot = false)
        {
            Host = host;
            if (snapshot)
            {
                errors = Logs.Errors; exceptions = Logs.Exceptions; warnings = Logs.Warnings; logSequence = Logs.MessageCount;
                eventBaseline = Events.Snapshot();
            }
        }
        public DevContext CreateScenarioContext() => new DevContext(Host, true);
        public World World => World.DefaultGameObjectInjectionWorld;
        public EntityManager EntityManager => World != null && World.IsCreated ? World.EntityManager : default;
        public bool TryGetService<T>(out T service) where T : class => Game.Core.Services.TryGet(out service);
        public DevLogCapture Logs => Host.Logs;
        public Input.DevInputInjector Input => Host.Input;
        public Ui.DevUiQuery Ui => Host.Ui;
        public DevEventCounter Events => Host.Events;
        public long LogErrors => Logs.Errors - errors;
        public long LogExceptions => Logs.Exceptions - exceptions;
        public long LogWarnings => Logs.Warnings - warnings;
        public long LogStartSequence => logSequence;
        public long EventCount(string name) => Events.Count(name) - (eventBaseline != null && eventBaseline.TryGetValue(name, out long count) ? count : 0);
        // TODO(DEV-009): expose the existing camera service in the snapshot/aim integration.
        public Camera GameplayCamera => null;
        public Entity PlayerShipRoot
        {
            get
            {
                if (World == null || !World.IsCreated) return Entity.Null;
                var query = Query(TypeManager.GetTypeIndex<Game.Simulation.Commands.PlayerShip>());
                return query.CalculateEntityCount() == 1 ? query.GetSingletonEntity() : Entity.Null;
            }
        }
        internal bool HasGameplay => World != null && World.IsCreated && !Query(TypeManager.GetTypeIndex<GameplayActive>()).IsEmptyIgnoreFilter;
        internal bool ServicesReady => GameRoot.Instance != null && GameRoot.Instance.Composition != null;
        EntityQuery Query(TypeIndex type)
        {
            if (queryWorld != World)
            {
                ReleaseQueries(); queryWorld = World;
            }
            if (!queries.TryGetValue(type, out var query))
            {
                query = queryWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly(type)); queries.Add(type, query);
            }
            return query;
        }
        public double Tick
        {
            get
            {
                if (World == null || !World.IsCreated) return -1;
                var query = Query(TypeManager.GetTypeIndex<SimClock>());
                return query.TryGetSingleton<SimClock>(out var clock) ? clock.Tick : -1;
            }
        }
        public int CountEntities(string name)
        {
            if (!typesLoaded)
            {
                foreach (var info in TypeManager.GetAllTypes())
                {
                    var type = info.Type; if (type == null || info.TypeIndex == TypeIndex.Null) continue;
                    componentTypes[type.FullName] = info.TypeIndex;
                    if (componentTypes.ContainsKey(type.Name)) componentTypes[type.Name] = TypeIndex.Null; // require full name when ambiguous
                    else componentTypes.Add(type.Name, info.TypeIndex);
                }
                typesLoaded = true;
            }
            if (!componentTypes.TryGetValue(name, out var index)) throw new ArgumentException("unknown component '" + name + "'");
            if (index == TypeIndex.Null) throw new ArgumentException("ambiguous component '" + name + "'; use full name");
            return World == null || !World.IsCreated ? 0 : Query(index).CalculateEntityCount();
        }
        public int AudioListenerCount()
        {
            int count = 0;
            var gameRoot = GameRoot.Instance != null ? GameRoot.Instance.gameObject : null;
            if (gameRoot != null) count += CountListeners(gameRoot);
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i); if (!scene.isLoaded) continue;
                scene.GetRootGameObjects(sceneRoots);
                foreach (var root in sceneRoots) if (root != gameRoot) count += CountListeners(root);
            }
            return count;
        }
        int CountListeners(GameObject root)
        {
            root.GetComponentsInChildren(true, listeners); int count = 0;
            foreach (var listener in listeners) if (listener.isActiveAndEnabled) count++;
            return count;
        }
        void ReleaseQueries()
        {
            if (queryWorld != null && queryWorld.IsCreated) foreach (var query in queries.Values) query.Dispose();
            queries.Clear(); queryWorld = null;
        }
        public void Dispose() { ReleaseQueries(); sceneRoots.Clear(); listeners.Clear(); }
    }
}
#endif
