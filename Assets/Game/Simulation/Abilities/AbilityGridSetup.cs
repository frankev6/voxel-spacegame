using Game.Voxel.Model;
using Unity.Entities;

namespace Game.Simulation.Abilities
{
    /// <summary>Implements SPEC-24 §4.3/6: ability components on every combat profile (ADR-0015).</summary>
    public static class AbilityGridSetup
    {
        public static ComponentTypeSet RootTypes => new ComponentTypeSet(ComponentType.ReadWrite<AbilitySlot>(), ComponentType.ReadWrite<AbilityState>());

        /// <summary>ADR-0117: the authority's slot state for a co-op guest's AbilitySlotApplySystem, on the same profiles, so the network
        /// layer only writes data. Registered by AbilitySlotApplySystem.OnCreate.</summary>
        public static ComponentTypeSet HostTypes => new ComponentTypeSet(ComponentType.ReadWrite<AbilityHostSlot>(), ComponentType.ReadWrite<AbilityHostState>());

        public static void RegisterHost(DynamicBuffer<RootComponentRegistration> registrations)
        {
            var types = HostTypes;
            for (GridProfile profile = GridProfile.PlayerShip; profile <= GridProfile.Dummy; profile++)
                for (int i = 0; i < types.Length; i++)
                    registrations.Add(new RootComponentRegistration { Profile = profile, Type = types.GetComponentType(i) });
        }

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
