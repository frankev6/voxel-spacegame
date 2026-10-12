using Unity.Entities;
using Unity.Collections;

namespace Game.Simulation.Weapons
{
    /// <summary>Implements SPEC-21 weapon contract: WeaponShots.</summary>
    public struct WeaponShots : IComponentData
    {
        public NativeList<WeaponShot> Shots;   // cleared and refilled every tick
        public Game.Data.Streams.GuardedHandles Access;
    }
}
