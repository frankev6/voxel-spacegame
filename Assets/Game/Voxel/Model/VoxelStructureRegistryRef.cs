using Game.Voxel.Grid;
using Unity.Entities;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.4. Singleton, created by SPEC-01.</summary>
    public struct VoxelStructureRegistryRef : IComponentData { public UnityObjectRef<VoxelStructureRegistry> Value; }
}
