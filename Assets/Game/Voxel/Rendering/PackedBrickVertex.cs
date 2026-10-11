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
    }
}
