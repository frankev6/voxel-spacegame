using System;
using Unity.Mathematics;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.1. A placed block (palette cell or instance); Equals compares all four fields.</summary>
    public struct BlockPlacement : IEquatable<BlockPlacement>
    {
        public ushort Block; public int3 Origin; public byte Orient; public byte Color;
        public bool Equals(BlockPlacement o) => Block == o.Block && Origin.Equals(o.Origin) && Orient == o.Orient && Color == o.Color;
        public override bool Equals(object obj) => obj is BlockPlacement p && Equals(p);
        public override int GetHashCode() => (int)math.hash(new int4(Origin, Block | (Orient << 16) | (Color << 24)));   // in-memory only
    }
}
