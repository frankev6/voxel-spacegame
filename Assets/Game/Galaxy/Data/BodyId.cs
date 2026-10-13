using Game.Data.Definitions;
using Unity.Mathematics;

namespace Game.Galaxy.Data
{
    /// <summary>Structural body identity (architecture §6.5). LocalKey is unique inside one star system. Implements SPEC-30 §4.</summary>
    public readonly struct BodyId : System.IEquatable<BodyId>
    {
        public readonly StarSystemId System;
        public readonly Game.Data.Definitions.BodyRole Role;
        public readonly ushort Slot, SubSlot;
        public BodyId(StarSystemId system, Game.Data.Definitions.BodyRole role, ushort slot, ushort subSlot)
        { System = system; Role = role; Slot = slot; SubSlot = subSlot; }
        public bool IsValid => System.IsValid && Role != Game.Data.Definitions.BodyRole.None;
        /// <summary>(Role &lt;&lt; 32) | (Slot &lt;&lt; 16) | SubSlot. Saved in deltas. Sort order = generation order.</summary>
        public ulong LocalKey => ((ulong)Role << 32) | ((ulong)Slot << 16) | SubSlot;
        public static BodyId FromLocalKey(StarSystemId system, ulong key)
            => new BodyId(system, (Game.Data.Definitions.BodyRole)(byte)(key >> 32), (ushort)(key >> 16), (ushort)key);
        public ulong StableKey => Game.Core.StableHash.Combine(System.Value, LocalKey);  // seeds and hashing
        public bool Equals(BodyId o) => System.Value == o.System.Value && LocalKey == o.LocalKey;
        public override bool Equals(object o) => o is BodyId b && Equals(b);
        public override int GetHashCode() => (int)(System.Value * 31u) ^ (int)LocalKey;    // dictionary use only
        public override string ToString() => System + "/" + Role + ":" + Slot + "." + SubSlot;
    }
}
