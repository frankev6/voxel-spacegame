namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §5.11. Deterministic benchmark structures built from one single-cell Structural Cube block, colour 0.</summary>
    public static class BenchmarkStructures
    {
        public static VoxelStructure HollowStation1M(ushort cubeBlock) { throw new System.NotImplementedException("VOX-017"); }      // reference: 1,008,128 cells, 872 bricks
        public static VoxelStructure ThinShellStation1M(ushort cubeBlock) { throw new System.NotImplementedException("VOX-017"); }   // stress: 1,043,464 cells, 3,784 bricks
        public static VoxelStructure SolidAsteroid1M(ushort cubeBlock) { throw new System.NotImplementedException("VOX-017"); }      // ≈ 998,000 cells, 245 bricks
        public static VoxelStructure Ship20k(ushort cubeBlock) { throw new System.NotImplementedException("VOX-017"); }              // 20,000 cells, 18 bricks
        public static VoxelStructure Lattice2500(ushort cubeBlock) { throw new System.NotImplementedException("VOX-017"); }          // 2,500 cells, 2,500 bricks
    }
}
