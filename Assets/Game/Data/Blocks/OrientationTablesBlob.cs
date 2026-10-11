using Unity.Entities;
using Unity.Mathematics;

namespace Game.Data.Blocks
{
    /// <summary>The 24-orientation group. Methods must be called through `ref` to the blob value. Burst-compatible. Implements SPEC-04 §4.7.</summary>
    public struct OrientationTablesBlob
    {
        public BlobArray<int3x3> Matrices;   // 24; column c = image of local basis vector c
        public BlobArray<quaternion> Rotations; // 24; same rotation as Matrices
        public BlobArray<byte> ComposeTable;   // 576; [a*24+b] = Compose(a, b)
        public BlobArray<byte> InverseTable;   // 24
        public BlobArray<byte> MirrorTable;    // 72;  [o*3+axis], axis 0=X 1=Y 2=Z
        public BlobArray<byte> FaceTable;      // 144; [o*6+face] = rotated face
        public BlobArray<byte> AxisTurnTable;  // 12;  [axis*4+q] = rotation by q·90° about the grid axis (right-hand rule)
        public BlobArray<byte> ForwardUpTable; // 36;  [fwdFace*6+upFace] = orientation, or 255 if not perpendicular

        public int3 Rotate(int3 v, byte o) => math.mul(Matrices[o], v);
        public float3 Rotate(float3 v, byte o) { var m = Matrices[o]; return v.x * (float3)m.c0 + v.y * (float3)m.c1 + v.z * (float3)m.c2; }
        public byte Compose(byte a, byte b) => ComposeTable[a * 24 + b];          // R[a]·R[b]: apply b first, then a
        public byte Inverse(byte o) => InverseTable[o];
        public byte Mirror(byte o, int axis) => MirrorTable[o * 3 + axis];         // index of M_axis · R[o] · Sx
        public BlockFace RotateFace(BlockFace face, byte o) => (BlockFace)FaceTable[o * 6 + (int)face];
        public byte AxisTurn(int axis, int quarterTurns) => AxisTurnTable[axis * 4 + (((quarterTurns % 4) + 4) % 4)];   // modulo 4 (negative allowed)
        public byte FromForwardUp(BlockFace forward, BlockFace up) => ForwardUpTable[(int)forward * 6 + (int)up];
    }
}
