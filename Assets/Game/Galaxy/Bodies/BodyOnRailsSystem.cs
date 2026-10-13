using Game.Core.Components;
using Game.Core.Systems;
using Game.Galaxy.Components;
using Game.Galaxy.Data;
using Game.Simulation.Power;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.Galaxy.Bodies
{
    /// <summary>SPEC-31 §5.13 tick-exact analytic positions before physics; preserves body rotation and scale.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplayPrePhysicsGroup))]
    [UpdateBefore(typeof(PowerAllocationSystem))]
    public partial struct BodyOnRailsSystem : ISystem
    {
        ComponentLookup<FrameOrigin> origins;

        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameplayActive>();
            state.RequireForUpdate<WorldSceneActive>();
            state.RequireForUpdate<FrameOrigin>();
            state.RequireForUpdate<SimClock>();
            origins = state.GetComponentLookup<FrameOrigin>(true);
        }

        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            origins.Update(ref state);
            state.Dependency = new UpdateBodyOrbitJob
            {
                Origin = SystemAPI.GetSingletonEntity<FrameOrigin>(), Origins = origins,
                UniversalTime = SystemAPI.GetSingleton<SimClock>().UniversalTime
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile, WithAll(typeof(UniversePositionIsAnalytic))]
        partial struct UpdateBodyOrbitJob : IJobEntity
        {
            public Entity Origin;
            [ReadOnly] public ComponentLookup<FrameOrigin> Origins;
            public double UniversalTime;

            void Execute(in BodyOrbit orbit, ref UniversePosition position, ref LocalTransform transform)
            {
                var body = new BodySpec { Orbit = orbit.Orbit, ParentOrbit = orbit.ParentOrbit };
                position.Value = OrbitMath.Position(body, UniversalTime);
                transform.Position = (float3)(position.Value - Origins[Origin].OriginInStarSystem);
            }
        }
    }
}
