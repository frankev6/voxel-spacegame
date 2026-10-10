namespace Game.Data.Blocks
{
    /// <summary>Managed, thread-safe facade over the orientation tables for non-Burst code (editor, MIG, tests); same results as
    /// OrientationTablesBlob. DATA-003 backs it with a static readonly OrientationData from OrientationTableGenerator.Generate().
    /// Implements SPEC-04 §4.7.</summary>
    public static class OrientationTables
    {
        public static Unity.Mathematics.int3x3 Matrix(byte o) { throw new System.NotImplementedException("DATA-003"); }
        public static bool TryFromMatrix(Unity.Mathematics.int3x3 m, out byte o) { throw new System.NotImplementedException("DATA-003"); }
        public static Unity.Mathematics.quaternion ToQuaternion(byte o) { throw new System.NotImplementedException("DATA-003"); }
        public static Unity.Mathematics.int3 Rotate(Unity.Mathematics.int3 v, byte o) { throw new System.NotImplementedException("DATA-003"); }
        public static Unity.Mathematics.float3 Rotate(Unity.Mathematics.float3 v, byte o) { throw new System.NotImplementedException("DATA-003"); }
        public static byte Compose(byte a, byte b) { throw new System.NotImplementedException("DATA-003"); }          // R[a]·R[b]: apply b first, then a
        public static byte Inverse(byte o) { throw new System.NotImplementedException("DATA-003"); }
        public static byte Mirror(byte o, int axis) { throw new System.NotImplementedException("DATA-003"); }         // index of M_axis · R[o] · Sx
        public static BlockFace RotateFace(BlockFace face, byte o) { throw new System.NotImplementedException("DATA-003"); }
        public static byte AxisTurn(int axis, int quarterTurns) { throw new System.NotImplementedException("DATA-003"); }   // quarterTurns taken modulo 4 (negative allowed)
        public static byte FromForwardUp(BlockFace forward, BlockFace up) { throw new System.NotImplementedException("DATA-003"); }
    }
}
