using System;

namespace Game.Persistence.Data
{
    /// <summary>Implements SPEC-05 §4.5. A slot inside a profile.</summary>
    public readonly struct SlotRef : IEquatable<SlotRef>
    {
        public readonly ProfileId Profile; public readonly SlotId Slot;
        public SlotRef(ProfileId profile, SlotId slot) { Profile = profile; Slot = slot; }
        public bool Equals(SlotRef other) => Profile.Equals(other.Profile) && Slot.Equals(other.Slot);
        public override bool Equals(object obj) => obj is SlotRef o && Equals(o);
        public override int GetHashCode() => Profile.GetHashCode() * 31 + Slot.GetHashCode();   // in-memory only
        public override string ToString() => $"{Profile}/{Slot}";
    }
}
