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
        {
            registrations.Add(new FunctionalComponentRegistration { Module = module, Type = ComponentType.ReadWrite<PowerRequest>() });
            registrations.Add(new FunctionalComponentRegistration { Module = module, Type = ComponentType.ReadWrite<PowerLink>() });
            registrations.Add(new FunctionalComponentRegistration { Module = module, Type = ComponentType.ReadWrite<Powered>() });
        }

        /// <summary>Finds the element for module (appends if missing); sets Draw and Priority, and Requesting = 0.</summary>
        public static void SetConsumer(DynamicBuffer<PowerRequest> requests, BlockModuleId module, float draw, byte priority)
        {
            for (int i = 0; i < requests.Length; i++)
            {
                var request = requests[i];
                if (request.Module != module) continue;
                request.Draw = draw; request.Priority = priority; request.Requesting = 0;
                requests[i] = request; return;
            }
            requests.Add(new PowerRequest { Module = module, Draw = draw, Priority = priority, ConsumerIndex = -1 });
        }

        /// <summary>Sets Requesting for module (no-op if missing); linear scan over at most four elements.</summary>
        public static void SetRequesting(DynamicBuffer<PowerRequest> requests, BlockModuleId module, bool requesting)
        {
            for (int i = 0; i < requests.Length; i++)
            {
                var request = requests[i];
                if (request.Module != module) continue;
                request.Requesting = (byte)(requesting ? 1 : 0); requests[i] = request; return;
            }
        }
    }
}
