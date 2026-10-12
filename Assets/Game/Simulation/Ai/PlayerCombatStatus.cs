using Unity.Entities;

namespace Game.Simulation.Ai
{
    /// <summary>Implements SPEC-23 §4.3: PlayerCombatStatus.</summary>
    public struct PlayerCombatStatus : IComponentData
    {
        public ushort HostilesEngaging;  // AI ships in Engage with ShipTarget == player ship, within CombatBubbleRadius
        public ushort Reserved;
        public uint LastDamagedTick;     // GridDamageStats.LastDamageTick of the player ship (0 = never)
    }
}
