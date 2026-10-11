using Unity.Mathematics;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §5.5. Grid-space DDA (Amanatides–Woo). originCells and dir in grid cell units (grid-local metres / CellSize); dir need not be normalized.</summary>
    public static class VoxelRaycast
    {
        public static bool Cast<T>(ref T query, float3 originCells, float3 dir, float maxCells, out VoxelRayHit hit) where T : struct, ICellSolidQuery { throw new System.NotImplementedException("VOX-008"); }
    }
}
