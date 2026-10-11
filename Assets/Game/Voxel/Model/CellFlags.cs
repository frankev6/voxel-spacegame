using System;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.1. Per-cell flag bits.</summary>
    [Flags] public enum CellFlags : byte
    {
        None = 0,
        InstanceRoot = 1,      // this cell is footprint cell (0,0,0) of its BlockInstance
        Scorched = 2,          // cosmetic burn mark; written by SPEC-13; SPEC-11 darkens the face
        // bits 2..7 reserved; claim through SPEC-10 open questions
    }
}
