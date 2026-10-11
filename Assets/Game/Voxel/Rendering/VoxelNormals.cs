using System;
using Unity.Mathematics;

namespace Game.Voxel.Rendering
{
    /// <summary>SPEC-11 §4.1: fixed axis/edge/long-slope ordering shared with BrickVertexDecode.hlsl.</summary>
    public static class VoxelNormals
    {
        public const int Count = 42;
        public static float3 Normal(int i)
        {
            float3 direction = Direction(i);
            return direction * math.rsqrt(math.lengthsq(direction));
        }
        public static float3 Tangent(int i)
        {
            if ((uint)i >= Count) throw new ArgumentOutOfRangeException(nameof(i));
            if (i < 6) return i switch
            {
                0 => new float3(0, 0, -1), 1 => new float3(0, 0, 1),
                5 => new float3(-1, 0, 0), _ => new float3(1, 0, 0)
            };
            int pair = i < 18 ? (i - 6) / 4 : (i - 18) / 8;
            return pair == 0 ? new float3(1, 0, 0) : pair == 1 ? new float3(0, 1, 0) : new float3(0, 0, 1);
        }
        public static int IndexOf(int3 scaledNormal)
        {
            for (int i = 0; i < Count; i++) if (math.all(Direction(i) == scaledNormal)) return i;
            return -1;
        }
        internal static int3 Direction(int i)
        {
            if ((uint)i >= Count) throw new ArgumentOutOfRangeException(nameof(i));
            if (i < 6) return i switch
            {
                0 => new int3(1, 0, 0), 1 => new int3(-1, 0, 0), 2 => new int3(0, 1, 0),
                3 => new int3(0, -1, 0), 4 => new int3(0, 0, 1), _ => new int3(0, 0, -1)
            };
            int offset = i < 18 ? i - 6 : i - 18;
            int pair = i < 18 ? offset / 4 : offset / 8;
            int a = 1, b = 1;
            if (i >= 18) { bool swap = (offset & 4) != 0; a = swap ? 2 : 1; b = swap ? 1 : 2; }
            if ((offset & 1) != 0) a = -a;
            if ((offset & 2) != 0) b = -b;
            return pair == 0 ? new int3(0, a, b) : pair == 1 ? new int3(a, 0, b) : new int3(a, b, 0);
        }
    }
}
