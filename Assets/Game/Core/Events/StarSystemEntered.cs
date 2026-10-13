namespace Game.Core.Events
{
    /// <summary>SPEC-31 §4.1; published by the star-system loader when entry completes.</summary>
    public readonly struct StarSystemEntered
    {
        public readonly uint StarSystemValue, VisitCount;
        public readonly StarSystemEntryReason Reason;
        public StarSystemEntered(uint s, StarSystemEntryReason r, uint visits) { StarSystemValue = s; Reason = r; VisitCount = visits; }
    }
}
