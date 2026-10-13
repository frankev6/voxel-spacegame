using System;
using Game.Data.Blocks;
using Game.Voxel.Model;
using Unity.Entities;

namespace Game.Simulation.Power
{
    /// <summary>Implements SPEC-22 §4.1. Registration helper for power-consuming modules,
    /// called from a system created after GridSpawnSystem.</summary>
    public static class PowerRegistration
    {
        /// <summary>Appends {module, PowerRequest}, {module, PowerLink} and {module, Powered} to the registration buffer.</summary>
        public static void RegisterConsumer(DynamicBuffer<FunctionalComponentRegistration> registrations, BlockModuleId module)
            => throw new NotImplementedException("PWR-004");

        /// <summary>Finds the element for module (appends if missing); sets Draw and Priority, and Requesting = 0.</summary>
        public static void SetConsumer(DynamicBuffer<PowerRequest> requests, BlockModuleId module, float draw, byte priority)
            => throw new NotImplementedException("PWR-004");

        /// <summary>Sets Requesting for module (no-op if missing); linear scan over at most four elements.</summary>
        public static void SetRequesting(DynamicBuffer<PowerRequest> requests, BlockModuleId module, bool requesting)
            => throw new NotImplementedException("PWR-004");
    }
}
