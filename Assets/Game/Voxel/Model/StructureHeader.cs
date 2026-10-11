using Unity.Mathematics;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.3.</summary>
    public struct StructureHeader
    {
        public float CellSize;         // metres; default 0.5
        public uint Revision;          // +1 on every mutating call
        public int3 CellMin, CellMax;  // inclusive solid bounds; valid when BoundsValid == 1
        public byte BoundsValid;       // 0 after any mutation; recomputed lazily
        public byte ReadOnly;          // 1 for blueprint structures: mutators refuse
        public int PinCount;           // > 0 while a spawn or rebuild reads it: mutators throw
        public uint MassRevision;      // Revision at which Mass was computed; 0 = never
        public StructureMassData Mass;
    }
}
