using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Voxel.Rendering
{
    /// <summary>SPEC-11 §4 / ADR-0027: positions are integer half-cell units (0..32); shader decoding returns cell units.</summary>
    public struct PackedBrickVertex
    {
        public uint Lo, Hi;
        public static PackedBrickVertex Pack(int x, int y, int z, int normal, int layer, int colour, int ao, int damage, bool scorched)
        {
            uint flags = (uint)(ao & 3) | ((uint)(damage & 3) << 2) | (scorched ? 16u : 0u);
            return new PackedBrickVertex { Lo = (uint)x | ((uint)y << 8) | ((uint)z << 16),
                Hi = (uint)normal | ((uint)layer << 8) | ((uint)colour << 16) | (flags << 24) };
        }
        /// <summary>ADR-0115 smooth (Xeno surface-nets) vertex, flag bit 5: positions in eighths of a cell offset by half a cell
        /// (brick cells -0.5..31.375), an octahedral normal in the normal byte and Lo's top byte, and a 0..15 depth (quarter cells)
        /// in the AO bits and flag bits 6-7 for the exposed-flesh glow.</summary>
        public const uint SmoothFlag = 32;
        public static PackedBrickVertex PackSmooth(float3 position, float3 normal, int layer, int colour, int depth, int damage)
        {
            int3 p = math.clamp((int3)math.round((position + .5f) * 8), 0, 255);
            float2 oct = OctEncode(normal);
            int ox = math.clamp((int)math.round(oct.x * 255), 0, 255), oy = math.clamp((int)math.round(oct.y * 255), 0, 255);
            depth = math.clamp(depth, 0, 15);
            uint flags = (uint)(depth & 3) | ((uint)(damage & 3) << 2) | SmoothFlag | ((uint)(depth >> 2) << 6);
            return new PackedBrickVertex { Lo = (uint)p.x | ((uint)p.y << 8) | ((uint)p.z << 16) | ((uint)oy << 24),
                Hi = (uint)ox | ((uint)layer << 8) | ((uint)colour << 16) | (flags << 24) };
        }
        public bool IsSmooth => ((Hi >> 24) & SmoothFlag) != 0;
        /// <summary>Brick-local position in cells for either encoding.</summary>
        public float3 Position => IsSmooth ? new float3(Lo & 255, (Lo >> 8) & 255, (Lo >> 16) & 255) / 8f - .5f
            : new float3(Lo & 255, (Lo >> 8) & 255, (Lo >> 16) & 255) * .5f;
        static float2 OctEncode(float3 n)
        {
            n /= math.max(1e-6f, math.abs(n.x) + math.abs(n.y) + math.abs(n.z));
            float2 e = n.y >= 0 ? n.xz : (1 - math.abs(n.zx)) * math.select(new float2(-1), new float2(1), n.xz >= 0);
            return e * .5f + .5f;
        }
    }
}
