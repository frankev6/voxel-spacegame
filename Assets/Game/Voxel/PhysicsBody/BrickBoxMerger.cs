using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Game.Voxel.Model;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>Implements SPEC-12 §4 (physics contract).</summary>
    public static class BrickBoxMerger
    {
        /// Exact, disjoint cover of the solid cells. Output cleared first. Returns the box count.
        public static int MergeExact(in BrickMaskBits mask, ref NativeList<BrickBox> output) => throw new System.NotImplementedException("PHY-004");
        /// Conservative cover on super-cells of edge superCell ∈ {2, 4}; a super-cell is solid if any of its cells is.
        public static int MergeCoarse(in BrickMaskBits mask, int superCell, ref NativeList<BrickBox> output) => throw new System.NotImplementedException("PHY-004");
        /// Level 0 exact, 1 = super-cell 2, 2 = super-cell 4, 3 = one box over the solid bounds; the first level with ≤ maxBoxes boxes.
        /// Returns the level used. An empty mask returns 0 with no boxes.
        public static int Merge(in BrickMaskBits mask, int maxBoxes, ref NativeList<BrickBox> output) => throw new System.NotImplementedException("PHY-004");
    }
}
