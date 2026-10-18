namespace Game.Simulation.Mechanisms
{
    /// <summary>How a moving structure moves (ADR-0119 decision 1). Append only.</summary>
    public enum MoverPath : byte
    {
        Rotate = 0,     // spins about its origin at Rate rad/s
        Rail = 1,       // follows its MoverPoints at Speed m/s; loops, or ping-pongs when Reverse is set
        Drift = 2,      // a slow seeded wander about its origin (debris, ice)
        Toggle = 3,     // moves between its two MoverPoints in 1/Rate seconds when played (doors)
    }
}
