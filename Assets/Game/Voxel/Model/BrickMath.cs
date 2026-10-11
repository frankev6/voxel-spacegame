using Game.Data.Blocks;
using Unity.Mathematics;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.2. Brick maths; Burst-compatible, no allocation.</summary>
    public static class BrickMath
    {
        public const int Size = 16, Shift = 4, Mask = 15, CellsPerBrick = 4096, MaskWords = 64;
        public static int3 BrickOf(int3 cell) => cell >> Shift;
        public static int3 LocalOf(int3 cell) => cell & Mask;
        public static int LocalIndex(int3 local) => local.x | (local.y << 4) | (local.z << 8);
        public static int LocalIndex(int x, int y, int z) => x | (y << 4) | (z << 8);
        public static int3 LocalFromIndex(int i) => new int3(i & 15, (i >> 4) & 15, i >> 8);
        public static int3 CellOf(int3 brick, int localIndex) => (brick << Shift) + LocalFromIndex(localIndex);
        public static int SlotIndex(int3 brick, int3 brickMin, int3 brickDims)
        {
            int3 d = brick - brickMin;
            return math.any(d < 0) || math.any(d >= brickDims) ? -1 : d.x + brickDims.x * (d.y + brickDims.y * d.z);
        }
        public static float3 CellMinCorner(int3 cell, float cellSize) => (float3)cell * cellSize;
        public static float3 CellCenter(int3 cell, float cellSize) => ((float3)cell + 0.5f) * cellSize;
        public static int3 CellAt(float3 gridLocalPoint, float cellSize) => (int3)math.floor(gridLocalPoint / cellSize);
        public static BlockFaceMask BorderFaces(int localIndex)
        {
            int3 local = LocalFromIndex(localIndex);
            var faces = BlockFaceMask.None;
            if (local.x == 0) faces |= BlockFaceMask.NegX;
            if (local.x == Mask) faces |= BlockFaceMask.PosX;
            if (local.y == 0) faces |= BlockFaceMask.NegY;
            if (local.y == Mask) faces |= BlockFaceMask.PosY;
            if (local.z == 0) faces |= BlockFaceMask.NegZ;
            if (local.z == Mask) faces |= BlockFaceMask.PosZ;
            return faces;
        }
        public static int3 FaceDirection(BlockFace f) => f switch
        {
            BlockFace.PosX => new int3(1, 0, 0), BlockFace.NegX => new int3(-1, 0, 0),
            BlockFace.PosY => new int3(0, 1, 0), BlockFace.NegY => new int3(0, -1, 0),
            BlockFace.PosZ => new int3(0, 0, 1), BlockFace.NegZ => new int3(0, 0, -1),
            _ => throw new System.ArgumentOutOfRangeException(nameof(f)),
        };
    }
}
