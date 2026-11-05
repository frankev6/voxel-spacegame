using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Simulation.Flight
{
    /// <summary>ADR-0123: sole writer of the <see cref="Flight3D"/> enabled bit. A request turns the mode on at once. Turned off, the ship
    /// flies on in 3D while the heading controller levels it (PropulsionJob), and hands it back to the plane once pitch, roll and their
    /// rates are below the planar tilt tolerance; the plane pull then takes it to Y = 0 and GridMassUpdateSystem restores the planar
    /// inertia.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplayPrePhysicsGroup))]
    [UpdateBefore(typeof(PropulsionStatsSystem))]
    public partial struct Flight3DModeSystem : ISystem
    {
        /// <summary>Sine of the largest pitch or roll handed back (0.03°): well inside PlanarImpulseJob's tilt warning.</summary>
        internal const float LevelSine = .0005f;
        internal const float LevelRate = .002f;   // rad/s of pitch or roll rate

        [BurstCompile]
        public void OnCreate(ref SystemState state) { state.RequireForUpdate<GameplayActive>(); }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) => state.Dependency = new ModeJob().Schedule(state.Dependency);

        internal static bool IsLevel(quaternion rotation, float3 worldAngular)
        {
            float3 nose = math.rotate(rotation, new float3(0, 0, 1)), right = math.rotate(rotation, new float3(1, 0, 0));
            float3 local = math.rotate(math.inverse(rotation), worldAngular);
            return math.abs(nose.y) < LevelSine && math.abs(right.y) < LevelSine && math.abs(local.x) < LevelRate && math.abs(local.z) < LevelRate;
        }

        [BurstCompile, WithPresent(typeof(Flight3D))]
        partial struct ModeJob : IJobEntity
        {
            void Execute(in Flight3DRequest request, EnabledRefRW<Flight3D> flight3D, in LocalTransform pose, in PhysicsVelocity velocity, in PhysicsMass mass)
            {
                if (request.On != 0) { if (!flight3D.ValueRO) flight3D.ValueRW = true; return; }
                if (!flight3D.ValueRO) return;
                float3 world = math.rotate(math.mul(pose.Rotation, mass.InertiaOrientation), velocity.Angular);
                if (IsLevel(pose.Rotation, world)) flight3D.ValueRW = false;
            }
        }
    }
}
