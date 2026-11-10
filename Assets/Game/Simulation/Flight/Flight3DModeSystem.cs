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
    /// <summary>ADR-0123: sole writer of the <see cref="Flight3D"/> enabled bit, on every flying ship. It creates the world switch
    /// <see cref="Flight3DMode"/> on (owner, 2026-11-08: on by default). Switched on (solo only), every ship enters 3D at once. Switched
    /// off, each ship flies on in 3D while the heading controller levels it (PropulsionJob), and is handed back to the plane once pitch,
    /// roll and their rates are below the planar tilt tolerance; the plane pull then takes it to Y = 0 and GridMassUpdateSystem restores
    /// the planar inertia.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplayPrePhysicsGroup))]
    [UpdateBefore(typeof(PropulsionStatsSystem))]
    public partial struct Flight3DModeSystem : ISystem
    {
        /// <summary>Sine of the largest pitch or roll handed back (0.03°): well inside PlanarImpulseJob's tilt warning.</summary>
        internal const float LevelSine = .0005f;
        internal const float LevelRate = .002f;   // rad/s of pitch or roll rate

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameplayActive>();
            state.EntityManager.CreateSingleton(new Flight3DMode { On = 1 });
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            bool on = Flight3DMode.Wanted(SystemAPI.TryGetSingleton(out Flight3DMode mode), mode,
                SystemAPI.TryGetSingleton(out Game.Core.Net.SimRole role) ? role.Value : Game.Core.Net.SimRoleKind.Solo);
            state.Dependency = new ModeJob { On = on }.Schedule(state.Dependency);
        }

        internal static bool IsLevel(quaternion rotation, float3 worldAngular)
        {
            float3 nose = math.rotate(rotation, new float3(0, 0, 1)), right = math.rotate(rotation, new float3(1, 0, 0));
            float3 local = math.rotate(math.inverse(rotation), worldAngular);
            return math.abs(nose.y) < LevelSine && math.abs(right.y) < LevelSine && math.abs(local.x) < LevelRate && math.abs(local.z) < LevelRate;
        }

        [BurstCompile, WithPresent(typeof(Flight3D))]
        partial struct ModeJob : IJobEntity
        {
            public bool On;

            void Execute(EnabledRefRW<Flight3D> flight3D, in LocalTransform pose, in PhysicsVelocity velocity, in PhysicsMass mass)
            {
                if (On) { if (!flight3D.ValueRO) flight3D.ValueRW = true; return; }
                if (!flight3D.ValueRO) return;
                float3 world = math.rotate(math.mul(pose.Rotation, mass.InertiaOrientation), velocity.Angular);
                if (IsLevel(pose.Rotation, world)) flight3D.ValueRW = false;
            }
        }
    }
}
