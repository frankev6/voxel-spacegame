using Unity.Mathematics;

namespace Game.Voxel.Model
{
    /// <summary>Implements SPEC-10 §5.4. Static, Burst: per-cell mass and inertia accumulation in double.</summary>
    public static class MassAccumulator
    {
        public static void AddCell(ref StructureMassData d, int3 cell, float cellSize, float mass)
        {
            Accumulate(ref d, cell, cellSize, mass); d.CellCount++;
        }
        public static void RemoveCell(ref StructureMassData d, int3 cell, float cellSize, float mass)
        {
            Accumulate(ref d, cell, cellSize, -(double)mass); d.CellCount--;
        }
        static void Accumulate(ref StructureMassData d, int3 cell, float cellSize, double mass)
        {
            double3 p = ((double3)cell + 0.5) * cellSize;
            d.Mass += mass; d.FirstMoment += mass * p;
            d.SecondDiag += mass * p * p;
            d.SecondOff += mass * new double3(p.x * p.y, p.y * p.z, p.z * p.x);
            d.CellInertia += mass * cellSize * cellSize / 6;
        }
        public static void Add(ref StructureMassData a, in StructureMassData b)
        {
            a.Mass += b.Mass; a.FirstMoment += b.FirstMoment; a.SecondDiag += b.SecondDiag;
            a.SecondOff += b.SecondOff; a.CellInertia += b.CellInertia; a.CellCount += b.CellCount;
        }
    }
}
