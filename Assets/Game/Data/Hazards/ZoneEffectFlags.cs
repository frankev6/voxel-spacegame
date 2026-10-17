namespace Game.Data.Hazards
{
    /// <summary>Which roots a zone effect applies to (SPEC-91 §4.3). Default: ships and devices.</summary>
    [System.Flags] public enum ZoneEffectFlags : byte { None = 0, AffectsShips = 1, AffectsDebris = 2, AffectsDevices = 4 }
}
