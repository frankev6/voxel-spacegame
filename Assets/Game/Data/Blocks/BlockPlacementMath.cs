using System;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.Data.Blocks
{
    /// <summary>Footprint, clearance, mirrored placement and face maths (SPEC-04 §5.6, §6.3). Burst-safe: no managed types except
    /// ComputeMirrorOffset, a managed helper used by validation.</summary>
    public static class BlockPlacementMath
    {
        public static void GetCells(ref BlockDef def, ref OrientationTablesBlob ot, byte orient, int3 origin, NativeList<int3> output) { throw new System.NotImplementedException("DATA-006"); }
        public static void GetClearanceCells(ref BlockDef def, ref OrientationTablesBlob ot, byte orient, int3 origin, NativeList<int3> output) { throw new System.NotImplementedException("DATA-006"); }
        public static void MirrorPlace(ref BlockDef def, ref OrientationTablesBlob ot, byte orient, int3 origin, int axis, int planeTwice,
                                       out ushort mirroredBlock, out byte mirroredOrient, out int3 mirroredOrigin) { throw new System.NotImplementedException("DATA-006"); }
        public static BlockFaceMask WorldAttachFaces(BlockFaceMask local, byte orient, ref OrientationTablesBlob ot) { throw new System.NotImplementedException("DATA-006"); }
        public static int3 ComputeMirrorOffset(ReadOnlySpan<int3> footprint, ReadOnlySpan<int3> partnerFootprint, out bool valid) { throw new System.NotImplementedException("DATA-004"); } // managed helper used by validation
    }
}
