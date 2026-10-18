using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Components;
using Game.Data.Definitions;
using Game.Voxel.PresentationStreams;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Voxel.Damage
{
    /// <summary>SPEC-91 §5.1 step 5 (HAZ-005): a root shoved by at least <see cref="MinDv"/> in one tick emits an
    /// <see cref="FxKind.Impact"/> event with Scale = |Δv|, which the camera's shake rules turn into trauma near the player.
    /// Every machine: impulses already land on every machine (ADR-0117), and the event is presentation only.</summary>
    [BurstCompile, UpdateInGroup(typeof(GameplayPostPhysicsGroup)), UpdateAfter(typeof(ImpulseApplySystem))]
    public partial struct ExternalPushFeedbackSystem : ISystem
    {
        /// <summary>m/s in one tick. The measured §8 table puts a railgun hit on the starter fighter at 2.4 m/s and a siege shell at 1.7.</summary>
        public const float MinDv = 1.5f;
        [BurstCompile] public void OnCreate(ref SystemState state)
        { state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<SimClock>(); state.RequireForUpdate<FxEventQueue>(); }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            var fx = SystemAPI.GetSingleton<FxEventQueue>();
            state.Dependency = new EmitJob { Tick = SystemAPI.GetSingleton<SimClock>().Tick, Fx = fx.Queue.AsParallelWriter() }
                .ScheduleParallel(fx.Access.BeginRead(state.Dependency));
            fx.Access.EndRead(state.Dependency);
        }
        [BurstCompile] partial struct EmitJob : IJobEntity
        {
            public uint Tick;
            // Atomic enqueue only; FxEventQueue.Access guards shared writers against drain/disposal.
            [NativeDisableContainerSafetyRestriction] public NativeQueue<FxEvent>.ParallelWriter Fx;
            void Execute(in ExternalPush push, in LocalTransform transform, in PhysicsMass mass)
            {
                if (push.Tick != Tick) return;
                float dv = math.length(push.LinearDv);
                if (!(dv >= MinDv)) return;
                Fx.Enqueue(new FxEvent { Kind = FxKind.Impact, Pos = transform.TransformPoint(mass.CenterOfMass), Dir = push.LinearDv / dv, Scale = dv });
            }
        }
    }
}
