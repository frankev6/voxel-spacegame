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
        public static int SlotIndex(int3 brick, int3 brickMin, int3 brickDims) { throw new System.NotImplementedException("VOX-004"); }   // -1 if outside; else d.x + dims.x*(d.y + dims.y*d.z), d = brick - brickMin
        public static float3 CellMinCorner(int3 cell, float cellSize) => (float3)cell * cellSize;
        public static float3 CellCenter(int3 cell, float cellSize) => ((float3)cell + 0.5f) * cellSize;
        public static int3 CellAt(float3 gridLocalPoint, float cellSize) => (int3)math.floor(gridLocalPoint / cellSize);
        public static BlockFaceMask BorderFaces(int localIndex) { throw new System.NotImplementedException("VOX-004"); } // brick faces the cell touches: x==0 → NegX, x==15 → PosX, y==0 → NegY, ...
        public static int3 FaceDirection(BlockFace f) { throw new System.NotImplementedException("VOX-004"); }           // PosX→(1,0,0), NegX→(-1,0,0), PosY, NegY, PosZ, NegZ (SPEC-04 order)
    }
}
