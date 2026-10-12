namespace Game.Simulation.Spatial
{
    /// <summary>Implements SPEC-23 §4.1: ProxyFlags.</summary>
    [System.Flags] public enum ProxyFlags : byte { None = 0, PlayerShip = 1, Dormant = 2, Shielded = 4, HasAi = 8 }
}
