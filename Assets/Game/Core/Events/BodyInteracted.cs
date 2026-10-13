namespace Game.Core.Events
{
    /// <summary>Implements SPEC-31 §4.1/4.4. Core-only BodyInteracted payload.</summary>
    public readonly struct BodyInteracted
    {
        public readonly uint StarSystemValue; public readonly ulong BodyKey; public readonly byte Kind; public readonly ulong GridPersistentId;
        public BodyInteracted(uint s, ulong key, byte kind, ulong grid) { StarSystemValue = s; BodyKey = key; Kind = kind; GridPersistentId = grid; }
    }
}
