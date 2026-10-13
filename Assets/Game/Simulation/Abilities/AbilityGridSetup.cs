using Game.Voxel.Model;
using Unity.Entities;

namespace Game.Simulation.Abilities
{
    /// <summary>Implements SPEC-24 §4.3/6: ability components on every combat profile (ADR-0015).</summary>
    public static class AbilityGridSetup
    {
        public static ComponentTypeSet RootTypes => new ComponentTypeSet(ComponentType.ReadWrite<AbilitySlot>(), ComponentType.ReadWrite<AbilityState>());

        /// <summary>Called once by AbilitySystem.OnCreate after GridSpawnSystem; the spawner adds values zeroed.</summary>
        public static void Register(DynamicBuffer<RootComponentRegistration> registrations)
        {
            var types = RootTypes;
            for (GridProfile profile = GridProfile.PlayerShip; profile <= GridProfile.Dummy; profile++)
                for (int i = 0; i < types.Length; i++)
                    registrations.Add(new RootComponentRegistration { Profile = profile, Type = types.GetComponentType(i) });
        }
    }
}
