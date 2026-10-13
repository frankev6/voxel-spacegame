namespace Game.Simulation.Power
{
    /// <summary>Implements SPEC-22 §4.1.</summary>
    [System.Flags] public enum PowerConsumerFlags : byte { None = 0, Granted = 1, Requesting = 2, Alive = 4, BrownedOut = 8 }
}
