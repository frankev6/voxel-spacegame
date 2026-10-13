namespace Game.Simulation.Power
{
    /// <summary>SPEC-22 §4.3 shield lifecycle flags; PWR-001 remaining contracts are tracked separately.</summary>
    [System.Flags] public enum ShieldFlags : byte { None = 0, Initialized = 1, Collapsed = 2, FillOnNextUpdate = 4 }
}
