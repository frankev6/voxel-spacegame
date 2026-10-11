using System;
using Unity.Mathematics;

namespace Game.Data.Blocks
{
    /// <summary>Immutable managed copy of the orientation tables (same layout as OrientationTablesBlob). Implements SPEC-04 §4.7.</summary>
    public sealed class OrientationData
    {
        readonly int3x3[] matrices; readonly quaternion[] rotations;
        readonly byte[] compose, inverse, mirror, face, axisTurn, forwardUp;

        internal OrientationData(int3x3[] matrices, quaternion[] rotations, byte[] compose, byte[] inverse, byte[] mirror, byte[] face, byte[] axisTurn, byte[] forwardUp)
        {
            this.matrices = matrices; this.rotations = rotations; this.compose = compose; this.inverse = inverse;
            this.mirror = mirror; this.face = face; this.axisTurn = axisTurn; this.forwardUp = forwardUp;
        }

        public ReadOnlySpan<int3x3> Matrices => matrices;        // 24
        public ReadOnlySpan<quaternion> Rotations => rotations;  // 24
        public ReadOnlySpan<byte> ComposeTable => compose;       // 576
        public ReadOnlySpan<byte> InverseTable => inverse;       // 24
        public ReadOnlySpan<byte> MirrorTable => mirror;         // 72
        public ReadOnlySpan<byte> FaceTable => face;             // 144
        public ReadOnlySpan<byte> AxisTurnTable => axisTurn;     // 12
        public ReadOnlySpan<byte> ForwardUpTable => forwardUp;   // 36
    }
}
