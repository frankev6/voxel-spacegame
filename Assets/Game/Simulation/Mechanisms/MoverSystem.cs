using Game.Core;
using Game.Core.Components;
using Game.Core.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Simulation.Mechanisms
{
    /// <summary>HAZ-013 (ADR-0119 decision 1): sets each kinematic mover's PhysicsVelocity so the physics step lands it on its path pose
    /// for the next tick (MoverMath.Pose). Unity Physics then moves it and pushes dynamic ships aside; contacts raise ram damage
    /// through ImpactEventSystem as usual. Authority decider: guests receive mover poses in snapshots. Origin rebases (GameInitGroup,
    /// earlier this tick) shift the root; this system shifts the mover's origin and points by the same delta.</summary>
    [BurstCompile, UpdateInGroup(typeof(GameplayPrePhysicsGroup))]
    public partial struct MoverSystem : ISystem
    {
        NativeReference<uint> appliedShift;     // OriginShiftRecord.Sequence already applied; uint.MaxValue = not seen yet
        NativeReference<float3> shift;           // this tick's delta, zero when none
        ComponentLookup<OriginShiftRecord> shifts;
        uint appliedActions;                     // MechanismActions.Tick already applied

        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            appliedShift = new NativeReference<uint>(uint.MaxValue, Allocator.Persistent); appliedActions = uint.MaxValue;
            shift = new NativeReference<float3>(Allocator.Persistent);
            shifts = state.GetComponentLookup<OriginShiftRecord>(true);
            state.RequireForUpdate<GameplayActive>();
            state.RequireForUpdate<SimClock>();
            state.RequireForUpdate<Mover>();
        }

        [BurstCompile] public void OnDestroy(ref SystemState state)
        {
            if (appliedShift.IsCreated) appliedShift.Dispose();
            if (shift.IsCreated) shift.Dispose();
        }

        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            shifts.Update(ref state);
            uint tick = SystemAPI.GetSingleton<SimClock>().Tick;
            // HAZ-014: last tick's mechanism actions for movers (the decider writes them; this system owns Mover).
            if (SystemAPI.TryGetSingleton<MechanismActions>(out var actions) && actions.Current.IsCreated && actions.Current.Length > 0 && actions.Tick != appliedActions)
            {
                appliedActions = actions.Tick;
                state.CompleteDependency();
                foreach (var mover in SystemAPI.Query<RefRW<Mover>>())
                    foreach (var a in actions.Current)
                        if (MechanismActionMath.IsMover(a.Kind) && a.Group == mover.ValueRO.Group) MechanismActionMath.Apply(ref mover.ValueRW, a, tick);
            }
            SystemAPI.TryGetSingletonEntity<OriginShiftRecord>(out var shiftEntity);
            state.Dependency = new ShiftJob { Shifts = shifts, ShiftEntity = shiftEntity, Applied = appliedShift, Delta = shift }.Schedule(state.Dependency);
            // every mover's path follows a rebase, including one whose grid is still spawning (no physics yet: MoveJob skips it,
            // while OriginRebaseSystem already shifted its pending pose; a big rig spawning across a rebase was dragged off its hub)
            state.Dependency = new ShiftPathJob { Delta = shift }.ScheduleParallel(state.Dependency);
            state.Dependency = new MoveJob { Tick = tick }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile] struct ShiftJob : IJob
        {
            [ReadOnly] public ComponentLookup<OriginShiftRecord> Shifts;
            public Entity ShiftEntity;
            public NativeReference<uint> Applied;
            public NativeReference<float3> Delta;
            public void Execute()
            {
                Delta.Value = float3.zero;
                var record = Shifts.HasComponent(ShiftEntity) ? Shifts[ShiftEntity] : default;
                if (Applied.Value == uint.MaxValue) { Applied.Value = record.Sequence; return; }   // movers were authored in the frame we found
                if (record.Sequence == Applied.Value) return;
                Applied.Value = record.Sequence;
                if (record.StarSystemChanged == 0) Delta.Value = record.Delta;
            }
        }

        [BurstCompile] partial struct ShiftPathJob : IJobEntity
        {
            [ReadOnly] public NativeReference<float3> Delta;
            void Execute(ref Mover mover, DynamicBuffer<MoverPoint> points)
            {
                var d = Delta.Value;
                if (!math.any(d != 0)) return;
                mover.Origin += d;
                if (mover.Path != MoverPath.Drift) for (int i = 0; i < points.Length; i++) { var p = points[i]; p.Pose += d; points[i] = p; }
            }
        }

        [BurstCompile] partial struct MoveJob : IJobEntity
        {
            public uint Tick;
            void Execute(in Mover mover, ref PhysicsVelocity velocity, DynamicBuffer<MoverPoint> points, in LocalTransform transform, in PhysicsMass mass)
            {
                if (mass.InverseMass != 0) return;   // dynamic bodies never move on a path (ADR-0119)
                MoverMath.Pose(mover, points.AsNativeArray(), Tick + 1, out float3 position, out float yaw);
                velocity = PhysicsVelocity.CalculateVelocityToTarget(mass, transform.Position, transform.Rotation,
                    new RigidTransform(Planar.YawRotation(yaw), position), 1f / SimTime.FixedTimestep);
            }
        }
    }
}
