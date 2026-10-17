namespace Game.Simulation.Hazards
{
    /// <summary>Mine lifecycle (SPEC-91 §4.4). Append only.</summary>
    public enum MinePhase : byte { Arming = 0, Armed = 1, Triggered = 2, Spent = 3 }
}
