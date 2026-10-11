using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Data.Blocks
{
    /// <summary>Generates the 24-orientation tables (SPEC-04 §4.7, §5.3). Decision 7: orientation 0 is identity; yaw-only orientations are 0, 4, 8, 12.</summary>
    public static class OrientationTableGenerator
    {
        [Game.Core.StaticStateOk("constant table, never written after the static constructor")] internal static readonly int3[] FaceVectors =
        { new int3(1, 0, 0), new int3(-1, 0, 0), new int3(0, 1, 0), new int3(0, -1, 0), new int3(0, 0, 1), new int3(0, 0, -1) };

        /// <summary>§5.3 steps 1–5.</summary>
        public static OrientationData Generate()
        {
            var F = new[] { BlockFace.PosZ, BlockFace.PosX, BlockFace.NegZ, BlockFace.NegX, BlockFace.PosY, BlockFace.NegY };
            var m = new int3x3[24];
            for (int f = 0; f < 6; f++)
            {
                int3 fwd = FaceVectors[(int)F[f]];
                int3 u = F[f] == BlockFace.PosY ? FaceVectors[(int)BlockFace.NegZ] : F[f] == BlockFace.NegY ? FaceVectors[(int)BlockFace.PosZ] : FaceVectors[(int)BlockFace.PosY];
                for (int r = 0; r < 4; r++)
                {
                    m[f * 4 + r] = new int3x3(Cross(u, fwd), u, fwd);   // columns: images of local +X, +Y, +Z
                    u = Cross(fwd, u);                                // roll +90° about the forward axis
                }
            }
            var compose = new byte[576];
            for (int a = 0; a < 24; a++) for (int b = 0; b < 24; b++) compose[a * 24 + b] = Index(m, math.mul(m[a], m[b]));
            var inverse = new byte[24];
            for (int o = 0; o < 24; o++) inverse[o] = Index(m, math.transpose(m[o]));
            var mirror = new byte[72];
            for (int o = 0; o < 24; o++) for (int axis = 0; axis < 3; axis++) mirror[o * 3 + axis] = Index(m, math.mul(math.mul(Refl(axis), m[o]), Refl(0)));
            var face = new byte[144];
            for (int o = 0; o < 24; o++) for (int f = 0; f < 6; f++) face[o * 6 + f] = (byte)FaceOf(math.mul(m[o], FaceVectors[f]));
            var axisTurn = new byte[12];
            for (int axis = 0; axis < 3; axis++)
            {
                var turn = int3x3.identity;
                for (int q = 0; q < 4; q++) { axisTurn[axis * 4 + q] = Index(m, turn); turn = math.mul(QuarterTurn(axis), turn); }
            }
            var forwardUp = new byte[36];
            for (int i = 0; i < 36; i++) forwardUp[i] = Orientation.Invalid;
            for (int o = 0; o < 24; o++) forwardUp[FaceOf(m[o].c2) * 6 + FaceOf(m[o].c1)] = (byte)o;
            var rotations = new quaternion[24];
            for (int o = 0; o < 24; o++) rotations[o] = new quaternion(new float3x3((float3)m[o].c0, (float3)m[o].c1, (float3)m[o].c2));
            return new OrientationData(m, rotations, compose, inverse, mirror, face, axisTurn, forwardUp);
        }

        /// <summary>Copies OrientationData into a blob (the caller disposes it).</summary>
        public static BlobAssetReference<OrientationTablesBlob> BuildBlob(OrientationData data, Allocator allocator)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<OrientationTablesBlob>();
            Copy(builder, ref root.Matrices, data.Matrices);
            Copy(builder, ref root.Rotations, data.Rotations);
            Copy(builder, ref root.ComposeTable, data.ComposeTable);
            Copy(builder, ref root.InverseTable, data.InverseTable);
            Copy(builder, ref root.MirrorTable, data.MirrorTable);
            Copy(builder, ref root.FaceTable, data.FaceTable);
            Copy(builder, ref root.AxisTurnTable, data.AxisTurnTable);
            Copy(builder, ref root.ForwardUpTable, data.ForwardUpTable);
            return builder.CreateBlobAssetReference<OrientationTablesBlob>(allocator);
        }

        static void Copy<T>(BlobBuilder builder, ref BlobArray<T> target, System.ReadOnlySpan<T> source) where T : unmanaged
        {
            var arr = builder.Allocate(ref target, source.Length);
            for (int i = 0; i < source.Length; i++) arr[i] = source[i];
        }

        static int3 Cross(int3 a, int3 b) => new int3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);

        static int3x3 Refl(int axis)
        {
            var d = new int3(1, 1, 1); d[axis] = -1;
            return new int3x3(new int3(d.x, 0, 0), new int3(0, d.y, 0), new int3(0, 0, d.z));
        }

        /// <summary>+90° about a grid axis, right-hand rule (columns are the images of X, Y, Z).</summary>
        static int3x3 QuarterTurn(int axis) => axis switch
        {
            0 => new int3x3(new int3(1, 0, 0), new int3(0, 0, 1), new int3(0, -1, 0)),   // Y → Z, Z → −Y
            1 => new int3x3(new int3(0, 0, -1), new int3(0, 1, 0), new int3(1, 0, 0)),   // Z → X, X → −Z
            _ => new int3x3(new int3(0, 1, 0), new int3(-1, 0, 0), new int3(0, 0, 1)),   // X → Y, Y → −X
        };

        internal static int FaceOf(int3 v)
        {
            for (int f = 0; f < 6; f++) if (v.Equals(FaceVectors[f])) return f;
            throw new System.InvalidOperationException("not a unit axis vector: " + v);
        }

        static byte Index(int3x3[] m, int3x3 x)
        {
            for (int i = 0; i < 24; i++) if (m[i].Equals(x)) return (byte)i;
            throw new System.InvalidOperationException("matrix is not a proper grid rotation");
        }
    }
}
