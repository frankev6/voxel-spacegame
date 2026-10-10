namespace Game.Data.Blocks
{
    /// <summary>Immutable managed copy of the orientation tables (same layout as OrientationTablesBlob). Implements SPEC-04 §4.7.</summary>
    public sealed class OrientationData
    {
        public System.ReadOnlySpan<Unity.Mathematics.int3x3> Matrices => throw new System.NotImplementedException("DATA-003");      // 24
        public System.ReadOnlySpan<Unity.Mathematics.quaternion> Rotations => throw new System.NotImplementedException("DATA-003"); // 24
        public System.ReadOnlySpan<byte> ComposeTable => throw new System.NotImplementedException("DATA-003");   // 576
        public System.ReadOnlySpan<byte> InverseTable => throw new System.NotImplementedException("DATA-003");   // 24
        public System.ReadOnlySpan<byte> MirrorTable => throw new System.NotImplementedException("DATA-003");    // 72
        public System.ReadOnlySpan<byte> FaceTable => throw new System.NotImplementedException("DATA-003");      // 144
        public System.ReadOnlySpan<byte> AxisTurnTable => throw new System.NotImplementedException("DATA-003");  // 12
        public System.ReadOnlySpan<byte> ForwardUpTable => throw new System.NotImplementedException("DATA-003"); // 36
    }
}
