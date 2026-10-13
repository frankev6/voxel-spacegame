namespace Game.Core.Events
{
    /// <summary>Implements SPEC-31 §4.1/4.4. Core-only DockStateChanged payload.</summary>
    public readonly struct DockStateChanged
    {
        public readonly bool Docked; public readonly uint StarSystemValue; public readonly ulong BodyKey;
        public DockStateChanged(bool d, uint s, ulong k) { Docked = d; StarSystemValue = s; BodyKey = k; }
    }
}
