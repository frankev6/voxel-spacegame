namespace Game.Simulation.Commands
{
    /// <summary>SPEC-32 §4.2 player movement/action restrictions.</summary>
    [System.Flags] public enum PlayerControlLockFlags : byte { None = 0, Movement = 1, Actions = 2 }
}
