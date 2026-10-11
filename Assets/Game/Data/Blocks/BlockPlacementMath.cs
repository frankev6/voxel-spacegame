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
        /// <summary>Managed helper used by validation (SPEC-04 §5.6 step 1): d = min(partner) − min(Sx(footprint)) per component;
        /// valid iff {Sx(c) + d | c ∈ footprint} equals the partner footprint as a set. Sx(c) = (−c.x, c.y, c.z).</summary>
        public static int3 ComputeMirrorOffset(ReadOnlySpan<int3> footprint, ReadOnlySpan<int3> partnerFootprint, out bool valid)
        {
            valid = false;
            if (footprint.Length == 0 || partnerFootprint.Length == 0) return int3.zero;
            int3 minMirrored = new int3(int.MaxValue), minPartner = new int3(int.MaxValue);
            foreach (var c in footprint) minMirrored = math.min(minMirrored, new int3(-c.x, c.y, c.z));
            foreach (var c in partnerFootprint) minPartner = math.min(minPartner, c);
            int3 d = minPartner - minMirrored;
            var mirrored = new System.Collections.Generic.HashSet<int3>();
            foreach (var c in footprint) mirrored.Add(new int3(-c.x, c.y, c.z) + d);
            var partner = new System.Collections.Generic.HashSet<int3>();
            foreach (var c in partnerFootprint) partner.Add(c);
            valid = mirrored.SetEquals(partner);
            return d;
        }
    }
}
