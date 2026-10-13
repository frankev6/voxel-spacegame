namespace Game.Data.Logic
{
    /// <summary>Public narrative contract (SPEC-40 §4.3); later task implementations are explicitly labelled.</summary>
    public readonly struct LogicBodyHandle
    {
        public readonly uint StarSystem; public readonly byte Role; public readonly ushort Slot; public readonly ushort SubSlot;
        public readonly Game.Core.ContentHash Template;                       // hash of the BodyTemplateDefinition ID
        public LogicBodyHandle(uint starSystem, byte role, ushort slot, ushort subSlot, Game.Core.ContentHash template)
        { StarSystem = starSystem; Role = role; Slot = slot; SubSlot = subSlot; Template = template; }
        public ulong Key => Game.Core.StableHash.Combine(Game.Core.StableHash.Combine(StarSystem, Role), ((ulong)Slot << 16) | SubSlot);
    }
}
