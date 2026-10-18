namespace Game.Simulation.Mechanisms
{
    /// <summary>What a mechanism rule does; each kind is applied by the system that owns its state (ADR-0119 decision 2). Append only.</summary>
    public enum MechanismActionKind : byte
    {
        MoverPlay = 0, MoverReverse = 1, MoverSpeed = 2,            // MoverSystem
        ZoneEnable = 3, ZoneDisable = 4, ZonePeriod = 5,            // ZoneSystem
        MinesArm = 6, MinesDisarm = 7,                              // MineSystem
        TargetPolicy = 8,                                           // AI targeting
        PortalJam = 9,                                              // the portal applier
        SpawnHazardGroup = 10,                                      // HazardSpawn
        NarrativeEffect = 11,                                       // the narrative effect runner
    }

    /// <summary>One action record, written by MechanismSystem (authority) and read by the action's owner.</summary>
    public struct MechanismAction
    {
        public MechanismActionKind Kind;
        public ushort Group;
        public float Value;
    }
}
