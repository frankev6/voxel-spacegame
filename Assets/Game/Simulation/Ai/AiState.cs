namespace Game.Simulation.Ai
{
    /// <summary>Implements SPEC-23 §4.3: AiState.</summary>
    public enum AiState : byte { Idle = 0, Patrol = 1, Engage = 2, Flee = 3, Return = 4, Hold = 5 }
}
