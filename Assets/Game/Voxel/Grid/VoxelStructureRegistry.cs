using System.Collections.Generic;
using Game.Core;
using Game.Voxel.Model;
using UnityEngine;

namespace Game.Voxel.Grid
{

    /// <summary>Implements SPEC-10 §4.4. Runtime instance only (CreateInstance, HideFlags.DontSave).</summary>
    public sealed class VoxelStructureRegistry : ScriptableObject
    {
        public static VoxelStructureRegistry CreateRuntime(BlueprintLibrary blueprints) { throw new System.NotImplementedException("VOX-011"); }
        public int Register(VoxelStructure structure) { throw new System.NotImplementedException("VOX-011"); }           // handle ≥ 0; a handle is never reused in a session
        public void Release(int handle) { throw new System.NotImplementedException("VOX-011"); }                          // forgets the structure; does NOT dispose it (owner disposes)
        public VoxelStructure Get(int handle) { throw new System.NotImplementedException("VOX-011"); }                    // null if released or out of range
        public bool TryGetBlueprint(ContentHash blueprint, out VoxelStructure structure) { throw new System.NotImplementedException("VOX-011"); }   // via BlueprintLibrary
        public void AddSpawnHook(IGridSpawnHook hook) { throw new System.NotImplementedException("VOX-011"); }
        public void RemoveSpawnHook(IGridSpawnHook hook) { throw new System.NotImplementedException("VOX-011"); }
        public IReadOnlyList<IGridSpawnHook> SpawnHooks => throw new System.NotImplementedException("VOX-011");
    }
}
