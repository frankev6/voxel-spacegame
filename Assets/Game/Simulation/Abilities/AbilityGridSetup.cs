using Unity.Entities;

namespace Game.Simulation.Abilities
{
    /// <summary>Implements SPEC-24 §4.3. Ability root composition (ADR-0015), registered once by AbilitySystem.OnCreate.</summary>
    public static class AbilityGridSetup
    {
        // TODO(ABL-002): AbilitySlot buffer (empty), AbilityState (zero).
        public static ComponentTypeSet RootTypes => throw new System.NotImplementedException("ABL-002");

        // TODO(ABL-002): append RootTypes for every combat profile.
        public static void Register(DynamicBuffer<Game.Voxel.Model.RootComponentRegistration> registrations)
            => throw new System.NotImplementedException("ABL-002");
    }
}
