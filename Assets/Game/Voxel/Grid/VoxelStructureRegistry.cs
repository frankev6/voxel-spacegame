using System;
using System.Collections.Generic;
using Game.Core;
using Game.Voxel.Model;
using UnityEngine;

namespace Game.Voxel.Grid
{
    /// <summary>Implements SPEC-10 §4.4. Runtime registry borrows structures; releasing a handle never disposes its owner's storage.</summary>
    public sealed class VoxelStructureRegistry : ScriptableObject
    {
        readonly List<VoxelStructure> structures = new List<VoxelStructure>();
        readonly List<IGridSpawnHook> hooks = new List<IGridSpawnHook>();
        BlueprintLibrary blueprints;
        IReadOnlyList<IGridSpawnHook> hookView;

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
        void OnDestroy() { structures.Clear(); hooks.Clear(); blueprints = null; }
    }
}
