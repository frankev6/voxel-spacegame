using Unity.Mathematics;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §5.4. Static, Burst: per-cell mass and inertia accumulation in double.</summary>
    public static class MassAccumulator
    {
        public static void AddCell(ref StructureMassData d, int3 cell, float cellSize, float mass) { throw new System.NotImplementedException("VOX-007"); }
        public static void RemoveCell(ref StructureMassData d, int3 cell, float cellSize, float mass) { throw new System.NotImplementedException("VOX-007"); }
        public static void Add(ref StructureMassData a, in StructureMassData b) { throw new System.NotImplementedException("VOX-007"); }
    }
}
