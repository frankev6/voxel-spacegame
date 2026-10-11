using Unity.Mathematics;

namespace Game.Voxel.Damage
{
    /// <summary>Amanatides–Woo 3D DDA over integer cells. Implements SPEC-13 §5.3.3.</summary>
    public struct GridDda
    {
        public bool Init(float3 startCells, float3 dir, int3 aabbMin, int3 aabbMaxExclusive) { throw new System.NotImplementedException("DMG-004"); } // false if the ray misses the AABB
        public bool Next(out int3 cell) { throw new System.NotImplementedException("DMG-004"); }  // cells in traversal order, starting at the entry cell; false after leaving the AABB
    }
}
