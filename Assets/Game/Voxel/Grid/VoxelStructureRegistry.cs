using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Flow;
using Unity.Entities;
using Game.Voxel.Model;
using UnityEngine;

namespace Game.Voxel.Grid
{
    /// <summary>Implements SPEC-10 §4.4. Runtime registry borrows structures; releasing a handle never disposes its owner's storage.</summary>
    public sealed class VoxelStructureRegistry : ScriptableObject, ISceneExiting, IDisposable
    {
        readonly List<VoxelStructure> structures = new List<VoxelStructure>();
        readonly List<IGridSpawnHook> hooks = new List<IGridSpawnHook>();
        BlueprintLibrary blueprints;
        IReadOnlyList<IGridSpawnHook> hookView;
        sealed class SpawnRead
        {
            public VoxelStructure Source;
            public Entity Bound;
            public int Handle;
            public ContentHash Blueprint;
            public bool Rebuild;
        }
        readonly Dictionary<int, SpawnRead> spawnReads = new Dictionary<int, SpawnRead>();
        readonly List<int> cancelled = new List<int>();
        int nextRead = 1;
        bool disposed;


        public static VoxelStructureRegistry CreateRuntime(BlueprintLibrary blueprints)
        {
            if (blueprints == null) throw new ArgumentNullException(nameof(blueprints));
            var registry = CreateInstance<VoxelStructureRegistry>();
            registry.hideFlags = HideFlags.DontSave; registry.name = "VoxelStructureRegistry";
            registry.blueprints = blueprints; registry.hookView = registry.hooks.AsReadOnly();
            return registry;
        }
        public int Register(VoxelStructure structure)
        {
            if (structure == null || !structure.Data.IsCreated) throw new ArgumentException("Structure must be alive", nameof(structure));
            int handle = structures.Count; structures.Add(structure); return handle;
        }
        public void Release(int handle) { if ((uint)handle < (uint)structures.Count) structures[handle] = null; }
        public VoxelStructure Get(int handle) => (uint)handle < (uint)structures.Count ? structures[handle] : null;
        public bool TryGetBlueprint(ContentHash blueprint, out VoxelStructure structure) => blueprints.TryGet(blueprint, out structure);
        public void AddSpawnHook(IGridSpawnHook hook)
        {
            if (hook == null) throw new ArgumentNullException(nameof(hook));
            foreach (var existing in hooks) if (ReferenceEquals(existing, hook)) return;
            hooks.Add(hook);
        }
        public void RemoveSpawnHook(IGridSpawnHook hook)
        {
            for (int i = 0; i < hooks.Count; i++) if (ReferenceEquals(hooks[i], hook)) { hooks.RemoveAt(i); return; }
        }
        public IReadOnlyList<IGridSpawnHook> SpawnHooks => hookView;
        internal int PinSpawnRead(VoxelStructure source, int handle, ContentHash blueprint)
        {
            if (disposed) throw new ObjectDisposedException(nameof(VoxelStructureRegistry));
            int token = nextRead; nextRead = checked(nextRead + 1);
            source.Pin();
            try { spawnReads.Add(token, new SpawnRead { Source = source, Handle = handle, Blueprint = blueprint }); }
            catch { source.Unpin(); throw; }
            return token;
        }
        internal VoxelStructure ReadSource(int token) => spawnReads.TryGetValue(token, out var read) ? read.Source : null;
        internal bool MatchesRead(int token, VoxelStructure source, int handle, ContentHash blueprint)
            => spawnReads.TryGetValue(token, out var read) && ReferenceEquals(read.Source, source) && read.Handle == handle && read.Blueprint == blueprint;
        internal void BindRead(int token, Entity root, bool rebuild = false)
        { spawnReads[token].Bound = root; spawnReads[token].Rebuild = rebuild; }
        internal void EndRead(int token)
        {
            if (!spawnReads.TryGetValue(token, out var read)) return;
            spawnReads.Remove(token); read.Source.Unpin();
            if (!read.Blueprint.IsNone) blueprints.ReleaseUnpinnedLarge();
        }
        /// <summary>Managed teardown: release only reads whose bound request no longer exists (ADR-0076).</summary>
        public void ReconcileAbandonedReads(EntityManager em)
        {
            em.CompleteAllTrackedJobs();
            ReconcileReads(em);
        }
        internal void ReconcileReads(EntityManager em)
        {
            cancelled.Clear();
            foreach (var pair in spawnReads)
            {
                Entity entity = pair.Value.Bound;
                if (entity == Entity.Null) continue;
                bool matches = em.Exists(entity) && (pair.Value.Rebuild
                    ? em.HasComponent<RebuildReadToken>(entity) && em.GetComponentData<RebuildReadToken>(entity).Value == pair.Key
                    : em.HasComponent<SpawnReadToken>(entity) && em.GetComponentData<SpawnReadToken>(entity).Value == pair.Key);
                if (!matches) cancelled.Add(pair.Key);
            }
            foreach (int token in cancelled) EndRead(token);
            cancelled.Clear();
        }
        public void CancelSpawnReads()
        {
            foreach (var read in spawnReads.Values) read.Source.Unpin();
            spawnReads.Clear(); cancelled.Clear();
        }
        public int SceneExitOrder => -10000;
        public void OnSceneExiting(in SceneExitContext context)
        {
            CancelSpawnReads(); blueprints.ReleaseUnpinnedLarge();
        }
        public void Dispose()
        {
            if (disposed) return;
            CancelSpawnReads(); structures.Clear(); hooks.Clear(); disposed = true;
        }
        void OnDestroy() { Dispose(); blueprints = null; }
    }
}
