using Unity.Mathematics;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.3. Mass accumulators; double so 1M-cell sums do not drift.</summary>
    public struct StructureMassData
    {
        public double Mass;            // Σ m (kg)
        public double3 FirstMoment;    // Σ m·p (p = cell centre, grid-local metres)
        public double3 SecondDiag;     // Σ m·(px², py², pz²)
        public double3 SecondOff;      // Σ m·(px·py, py·pz, pz·px)
        public double CellInertia;     // Σ m·s²/6 (each cell's own cube inertia about its centre, s = CellSize)
        public uint CellCount;
    }
}
