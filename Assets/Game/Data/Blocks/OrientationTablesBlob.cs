namespace Game.Data.Blocks
{
    /// <summary>The 24-orientation group. Methods must be called through `ref` to the blob value. Implements SPEC-04 §4.7.</summary>
    public struct OrientationTablesBlob
    {
        public Unity.Entities.BlobArray<Unity.Mathematics.int3x3> Matrices;   // 24; column c = image of local basis vector c
        public Unity.Entities.BlobArray<Unity.Mathematics.quaternion> Rotations; // 24; same rotation as Matrices
        public Unity.Entities.BlobArray<byte> ComposeTable;   // 576; [a*24+b] = Compose(a, b)
        public Unity.Entities.BlobArray<byte> InverseTable;   // 24
        public Unity.Entities.BlobArray<byte> MirrorTable;    // 72;  [o*3+axis], axis 0=X 1=Y 2=Z
        public Unity.Entities.BlobArray<byte> FaceTable;      // 144; [o*6+face] = rotated face
        public Unity.Entities.BlobArray<byte> AxisTurnTable;  // 12;  [axis*4+q] = rotation by q·90° about the grid axis (right-hand rule)
        public Unity.Entities.BlobArray<byte> ForwardUpTable; // 36;  [fwdFace*6+upFace] = orientation, or 255 if not perpendicular

        public Unity.Mathematics.int3 Rotate(Unity.Mathematics.int3 v, byte o) { throw new System.NotImplementedException("DATA-003"); }
        public Unity.Mathematics.float3 Rotate(Unity.Mathematics.float3 v, byte o) { throw new System.NotImplementedException("DATA-003"); }
        public byte Compose(byte a, byte b) { throw new System.NotImplementedException("DATA-003"); }          // R[a]·R[b]: apply b first, then a
        public byte Inverse(byte o) { throw new System.NotImplementedException("DATA-003"); }
        public byte Mirror(byte o, int axis) { throw new System.NotImplementedException("DATA-003"); }         // index of M_axis · R[o] · Sx
        public BlockFace RotateFace(BlockFace face, byte o) { throw new System.NotImplementedException("DATA-003"); }
        public byte AxisTurn(int axis, int quarterTurns) { throw new System.NotImplementedException("DATA-003"); }   // quarterTurns taken modulo 4 (negative allowed)
        public byte FromForwardUp(BlockFace forward, BlockFace up) { throw new System.NotImplementedException("DATA-003"); }
    }
}
