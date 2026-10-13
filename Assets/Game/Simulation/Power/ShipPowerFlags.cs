namespace Game.Simulation.Power
{
    /// <summary>Implements SPEC-22 §4.1.</summary>
    [System.Flags] public enum ShipPowerFlags : byte { None = 0, Initialized = 1, Brownout = 2, Dead = 4 }
}
