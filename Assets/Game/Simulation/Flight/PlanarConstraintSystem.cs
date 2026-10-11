using Game.Core.Components;
using Game.Core.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Profiling;

namespace Game.Simulation.Flight
{
    /// <summary>Applies vertical impulses after propulsion, leaving integration to physics. Implements SPEC-20 §5.5.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplayPrePhysicsGroup))]
    [UpdateAfter(typeof(PropulsionSystem))]
    public partial struct PlanarConstraintSystem : ISystem
    {
        static readonly ProfilerMarker ScheduleMarker = new ProfilerMarker(ProfilerCategory.Scripts, "PlanarConstraintSystem.Schedule");
        NativeReference<int> tiltReported;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameplayActive>();
            state.RequireForUpdate<FlightConfig>();
            tiltReported = new NativeReference<int>(Allocator.Persistent);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            using var marker = ScheduleMarker.Auto();
            state.Dependency = new PlanarImpulseJob
            {
                Correction = SystemAPI.GetSingleton<FlightConfig>().PlaneCorrection, TiltReported = tiltReported
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            state.Dependency.Complete();
            if (tiltReported.IsCreated) tiltReported.Dispose();
        }
    }
}
