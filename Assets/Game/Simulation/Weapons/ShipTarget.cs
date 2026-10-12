using Unity.Entities;

namespace Game.Simulation.Weapons
{
    /// <summary>Implements SPEC-21 weapon contract: ShipTarget.</summary>
    public struct ShipTarget : Unity.Entities.IComponentData
    {
        public Unity.Entities.Entity Value;
        public uint SinceTick;           // SimClock.Tick when Value last changed
    }
}
