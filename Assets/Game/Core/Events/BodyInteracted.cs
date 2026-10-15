namespace Game.Core.Events
{
    /// <summary>Implements SPEC-31 §4.1/4.4. Core-only BodyInteracted payload. PilotPersistentId (ADR-0117) is the ship that interacted:
    /// the local player's, or a co-op guest's on the host; 0 when unknown (the local player).</summary>
    public readonly struct BodyInteracted
    {
        public readonly uint StarSystemValue; public readonly ulong BodyKey; public readonly byte Kind; public readonly ulong GridPersistentId;
        public readonly ulong PilotPersistentId;
        public BodyInteracted(uint s, ulong key, byte kind, ulong grid, ulong pilot = 0)
        { StarSystemValue = s; BodyKey = key; Kind = kind; GridPersistentId = grid; PilotPersistentId = pilot; }
    }
}
