using Unity.Entities;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.5. Singleton, created by SPEC-01 from VoxelConfigDefinition.</summary>
    public struct VoxelConfig : IComponentData
    { public int SpawnBricksPerFrame; public byte RequireRenderer; public float DefaultCellSize; }
}
