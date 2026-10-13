namespace Game.Simulation.Abilities
{
    /// <summary>Implements SPEC-24 §4.3. Per-tick activation, denial and slot resync flags.</summary>
    [System.Flags]
    public enum AbilitySlotFlags : byte
    {
        None = 0,
        ActivatedThisTick = 1,
        DeniedThisTick = 2,
        Resynced = 4 // slot list rebuilt or hulk this tick
    }
}
