using Unity.Entities;

namespace Game.Simulation.Ai
{
    /// <summary>ADR-0117: SPEC-23 §4.3 combat status of one pilot (Game.Simulation.Commands.Pilot). The authority's
    /// PlayerCombatStatusSystem computes it for every pilot; a co-op guest receives its own. PlayerCombatStatusSyncSystem copies the
    /// ship this machine flies into the PlayerCombatStatus singleton.</summary>
    public struct PilotCombatStatus : IComponentData
    {
        public ushort HostilesEngaging;
        public ushort Reserved;
        public uint LastDamagedTick;   // this machine's tick (0 = never)
    }
}
