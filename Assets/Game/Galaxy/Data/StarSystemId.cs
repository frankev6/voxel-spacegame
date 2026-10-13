using Game.Data.Definitions;
using Unity.Mathematics;

namespace Game.Galaxy.Data
{
    /// <summary>Star-system identity (architecture §6.5). Implements SPEC-30 §4.2.</summary>
    public readonly struct StarSystemId : System.IEquatable<StarSystemId>, System.IComparable<StarSystemId>
    {
        public readonly uint Value;                                   // 0 = None
        public const uint AuthoredBit = 0x80000000u;
        public StarSystemId(uint value) { Value = value; }
        public static StarSystemId None => default;
        public bool IsValid => Value != 0;
        public bool IsAuthored => (Value & AuthoredBit) != 0;
        public static StarSystemId FromProceduralIndex(int index) => new StarSystemId((uint)index + 1u);  // index 0 .. 0x7FFFFFFD
        public static StarSystemId FromAuthored(Game.Core.ContentHash hash)
        { uint low = (uint)(hash.Value & 0x7FFFFFFFul); return new StarSystemId(AuthoredBit | (low == 0u ? 1u : low)); }
        public bool Equals(StarSystemId o) => Value == o.Value;
        public override bool Equals(object o) => o is StarSystemId s && s.Value == Value;
        public override int GetHashCode() => (int)Value;              // dictionary use only; never persisted
        public int CompareTo(StarSystemId o) => Value.CompareTo(o.Value);
        public override string ToString() => "ss:" + Value.ToString("X8");
    }
}
