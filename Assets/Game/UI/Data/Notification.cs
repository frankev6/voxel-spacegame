namespace Game.UI.Data
{
    /// <summary>Implements SPEC-60 §4.1. Shared UI contract.</summary>
    public struct Notification
    {
        public NotificationKind Kind;
        public string Label;          // shown text, e.g. "Hull" or "Game saved"
        public int Amount;            // summed when coalesced
        public string CoalesceKey;    // null or "" = never coalesce; e.g. "item:block_hull"
    }
}
