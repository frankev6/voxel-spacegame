namespace Game.Simulation.Commands
{
    /// <summary>SPEC-32 §4.2 singleton: TRV-007 creates it, TravelService owns writes, PlayerCommandApplySystem reads it.</summary>
    public struct PlayerControlLock : Unity.Entities.IComponentData
    {
        public PlayerControlLockFlags Flags;
    }
}
