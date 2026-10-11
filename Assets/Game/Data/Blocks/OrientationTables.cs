using Unity.Mathematics;

namespace Game.Data.Blocks
{
    /// <summary>Managed, thread-safe facade over the orientation tables for non-Burst code (editor, MIG, tests); same results as
    /// OrientationTablesBlob. Backed by a static readonly OrientationData from OrientationTableGenerator.Generate(). Implements SPEC-04 §4.7.</summary>
    public static class OrientationTables
    {
        [Game.Core.StaticStateOk("immutable tables generated once; identical every play")]
        static readonly OrientationData Data = OrientationTableGenerator.Generate();

        internal static OrientationData Tables => Data;
        public static int3x3 Matrix(byte o) => Data.Matrices[o];

        public static bool TryFromMatrix(int3x3 m, out byte o)
        {
            for (int i = 0; i < 24; i++) if (Data.Matrices[i].Equals(m)) { o = (byte)i; return true; }
            o = Orientation.Invalid;
            return false;
        }

        public static quaternion ToQuaternion(byte o) => Data.Rotations[o];
        public static int3 Rotate(int3 v, byte o) => math.mul(Data.Matrices[o], v);
        public static float3 Rotate(float3 v, byte o) { var m = Data.Matrices[o]; return v.x * (float3)m.c0 + v.y * (float3)m.c1 + v.z * (float3)m.c2; }
        public static byte Compose(byte a, byte b) => Data.ComposeTable[a * 24 + b];          // R[a]·R[b]: apply b first, then a
        public static byte Inverse(byte o) => Data.InverseTable[o];
        public static byte Mirror(byte o, int axis) => Data.MirrorTable[o * 3 + axis];         // index of M_axis · R[o] · Sx
        public static BlockFace RotateFace(BlockFace face, byte o) => (BlockFace)Data.FaceTable[o * 6 + (int)face];
        public static byte AxisTurn(int axis, int quarterTurns) => Data.AxisTurnTable[axis * 4 + (((quarterTurns % 4) + 4) % 4)];   // modulo 4, negative allowed
        public static byte FromForwardUp(BlockFace forward, BlockFace up) => Data.ForwardUpTable[(int)forward * 6 + (int)up];
    }
}
