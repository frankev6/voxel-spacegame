namespace Game.Core.Events
{
    /// <summary>Implements SPEC-31 §4.1/4.4. Core-only BodyDestroyed payload.</summary>
    public readonly struct BodyDestroyed
    {
        public readonly uint StarSystemValue; public readonly ulong BodyKey;
        public BodyDestroyed(uint s, ulong key) { StarSystemValue = s; BodyKey = key; }
    }
}
