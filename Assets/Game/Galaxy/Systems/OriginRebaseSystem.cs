using Game.Core;
using Game.Core.Components;
using Game.Core.Events;
using Game.Core.Systems;
using Game.Data.Components;
using Game.Galaxy.Components;
using Game.Galaxy.Data;
using Game.Simulation.Commands;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.GraphicsIntegration;
using Unity.Profiling;
using Unity.Transforms;

namespace Game.Galaxy.Systems
{
    /// <summary>SPEC-30 §5.10: sole floating-origin writer, with asynchronous gather/decide/shift/place/sync.</summary>
    [BurstCompile, UpdateInGroup(typeof(GameInitGroup), OrderFirst = true)]
    public partial struct OriginRebaseSystem : ISystem
    {
        static readonly ProfilerMarker DecideMarker = new ProfilerMarker("OriginRebaseSystem.Decide");
        static readonly ProfilerMarker ShiftMarker = new ProfilerMarker("OriginRebaseSystem.Shift");
        static readonly ProfilerMarker SyncMarker = new ProfilerMarker("OriginRebaseSystem.Sync");
        Entity origin;
        EntityQuery playerQuery;
        ComponentLookup<FrameOrigin> origins;
        ComponentLookup<OriginRebaseRequest> requests;
        ComponentLookup<OriginShiftRecord> records;
        ComponentLookup<UniversePosition> positions;
        ComponentLookup<UniversePosition> readPositions;
        ComponentLookup<PhysicsGraphicalInterpolationBuffer> interpolation;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            origin = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponent<FrameOrigin>(origin);
            state.EntityManager.AddComponent<OriginRebaseRequest>(origin);
            state.EntityManager.AddComponent<OriginShiftRecord>(origin);
            state.EntityManager.SetComponentEnabled<OriginRebaseRequest>(origin, false);
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<WorldSceneActive>(); state.RequireForUpdate<FrameOrigin>();
            state.RequireForUpdate<ManagedEventQueue<OriginShifted>>();
            // Register an ECS write dependency for the queue owner so the managed drain waits for this producer.
            _ = state.GetComponentTypeHandle<ManagedEventQueue<OriginShifted>>();
            playerQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<PlayerShip, LocalTransform>().WithNone<Parent>().Build(ref state);
            origins = state.GetComponentLookup<FrameOrigin>(); requests = state.GetComponentLookup<OriginRebaseRequest>();
            records = state.GetComponentLookup<OriginShiftRecord>(); positions = state.GetComponentLookup<UniversePosition>();
            readPositions = state.GetComponentLookup<UniversePosition>(true);
            interpolation = state.GetComponentLookup<PhysicsGraphicalInterpolationBuffer>();
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            origins.Update(ref state); requests.Update(ref state); records.Update(ref state);
            positions.Update(ref state); readPositions.Update(ref state); interpolation.Update(ref state);
            // Capture the preceding physics step before a same-system rebase can overwrite its pose.
            state.Dependency = new CapturePositionJob { Origin = origin, Origins = origins, Requests = requests }.ScheduleParallel(state.Dependency);
            var decision = new NativeReference<RebaseDecision>(state.WorldUpdateAllocator);
            var players = playerQuery.ToComponentDataListAsync<LocalTransform>(state.WorldUpdateAllocator, state.Dependency, out var gather);
            var queue = SystemAPI.GetSingleton<ManagedEventQueue<OriginShifted>>().Queue.AsParallelWriter();
            uint tick = SystemAPI.TryGetSingleton<SimClock>(out var clock) ? clock.Tick : 0;
            using (DecideMarker.Auto()) state.Dependency = new DecideJob { Origin = origin, Origins = origins, Requests = requests,
                Records = records, Players = players, Decision = decision, Events = queue, Tick = tick }.Schedule(JobHandle.CombineDependencies(state.Dependency, gather));
            using (ShiftMarker.Auto())
            {
                state.Dependency = new ShiftJob { Decision = decision, Positions = readPositions, Interpolation = interpolation }.ScheduleParallel(state.Dependency);
                state.Dependency = new PlaceJob { Decision = decision, Positions = positions, Interpolation = interpolation }.Schedule(state.Dependency);
            }
            using (SyncMarker.Auto()) state.Dependency = new SyncJob { Decision = decision }.ScheduleParallel(state.Dependency);
        }
        struct RebaseDecision
        {
            public double3 NewOrigin;
            public float3 Delta;
            public byte Rebase, Changed, PlacePlayer;
            public PlayerPlacement Placement;
        }
        [BurstCompile, WithNone(typeof(Parent), typeof(UniversePositionIsAnalytic))] partial struct CapturePositionJob : IJobEntity
        {
            public Entity Origin;
            [ReadOnly] public ComponentLookup<FrameOrigin> Origins;
            [ReadOnly] public ComponentLookup<OriginRebaseRequest> Requests;
            void Execute(ref UniversePosition position, in LocalTransform transform)
            {
                var frame = Origins[Origin]; var request = Requests[Origin];
                // On star-system entry, newly authored universe positions already belong to the destination.
                if (Requests.IsComponentEnabled(Origin) && request.Serial > frame.AppliedRequestSerial &&
                    !request.StarSystem.Equals(frame.StarSystem)) return;
                position.Value = frame.OriginInStarSystem + (double3)transform.Position;
            }
        }
        [BurstCompile] struct DecideJob : IJob
        {
            public Entity Origin;
            public ComponentLookup<FrameOrigin> Origins;
            public ComponentLookup<OriginRebaseRequest> Requests;
            public ComponentLookup<OriginShiftRecord> Records;
            [ReadOnly] public NativeList<LocalTransform> Players;
            public NativeReference<RebaseDecision> Decision;
            public NativeQueue<ManagedEventEnvelope<OriginShifted>>.ParallelWriter Events;
            public uint Tick;
            public void Execute()
            {
                var frame = Origins[Origin]; var result = new RebaseDecision { NewOrigin = frame.OriginInStarSystem };
                var request = Requests[Origin];
                if (Requests.IsComponentEnabled(Origin) && request.Serial > frame.AppliedRequestSerial)
                {
                    result.Rebase = 1; result.Changed = (byte)(request.StarSystem.Equals(frame.StarSystem) ? 0 : 1);
                    result.NewOrigin = request.NewOrigin; result.Delta = result.Changed == 1 ? float3.zero : (float3)(frame.OriginInStarSystem - request.NewOrigin);
                    result.PlacePlayer = request.PlacePlayer; result.Placement = request.Placement;
                    frame.StarSystem = request.StarSystem; frame.AppliedRequestSerial = request.Serial;
                    Requests.SetComponentEnabled(Origin, false);
                }
                else if (Players.Length > 0 && math.lengthsq(Players[0].Position) > GalaxyConstants.OriginRebaseThresholdM * GalaxyConstants.OriginRebaseThresholdM)
                {
                    result.Rebase = 1; result.NewOrigin += (double3)Players[0].Position;
                    result.Delta = (float3)(frame.OriginInStarSystem - result.NewOrigin);
                }
                if (result.Rebase == 1)
                {
                    frame.OriginInStarSystem = result.NewOrigin; frame.ShiftSequence++;
                    frame.LastShiftDelta = result.Delta; frame.LastShiftChangedStarSystem = result.Changed;
                    Origins[Origin] = frame;
                    Records[Origin] = new OriginShiftRecord { Sequence = frame.ShiftSequence, Delta = result.Delta, StarSystemChanged = result.Changed };
                    Events.Enqueue(new ManagedEventEnvelope<OriginShifted> { Tick = Tick, SortKey = frame.ShiftSequence,
                        Payload = new OriginShifted(result.Delta, result.Changed, frame.StarSystem.Value, frame.ShiftSequence) });
                }
                Decision.Value = result;
            }
        }
        [BurstCompile, WithNone(typeof(Parent))] partial struct ShiftJob : IJobEntity
        {
            [ReadOnly] public NativeReference<RebaseDecision> Decision;
            [ReadOnly] public ComponentLookup<UniversePosition> Positions;
            // Query entities are unique roots; each iteration writes only its own history, never a shared entity.
            [NativeDisableParallelForRestriction] public ComponentLookup<PhysicsGraphicalInterpolationBuffer> Interpolation;
            void Execute(Entity entity, ref LocalTransform transform)
            {
                var d = Decision.Value; if (d.Rebase == 0) return;
                if (Positions.HasComponent(entity)) transform.Position = (float3)(Positions[entity].Value - d.NewOrigin);
                else if (d.Changed == 0) transform.Position += d.Delta;
                else return;
                if (!Interpolation.HasComponent(entity)) return;
                var history = Interpolation[entity];
                if (d.Changed == 1) history.PreviousTransform = new RigidTransform(transform.Rotation, transform.Position);
                else history.PreviousTransform.pos += d.Delta;
                Interpolation[entity] = history;
            }
        }
        [BurstCompile, WithAll(typeof(PlayerShip)), WithNone(typeof(Parent))] partial struct PlaceJob : IJobEntity
        {
            [ReadOnly] public NativeReference<RebaseDecision> Decision;
            public ComponentLookup<UniversePosition> Positions;
            public ComponentLookup<PhysicsGraphicalInterpolationBuffer> Interpolation;
            void Execute(Entity entity, ref LocalTransform transform, ref PhysicsVelocity velocity)
            {
                var d = Decision.Value; if (d.Rebase == 0 || d.PlacePlayer == 0) return;
                float yaw = Planar.YawFromForward(math.mul(transform.Rotation, new float3(0, 0, 1)));
                transform.Position = (float3)(d.Placement.PositionM - d.NewOrigin); transform.Rotation = Planar.YawRotation(d.Placement.YawRad);
                velocity.Linear = math.mul(quaternion.RotateY(d.Placement.YawRad - yaw), velocity.Linear);
                if (Positions.HasComponent(entity)) Positions[entity] = new UniversePosition { Value = d.Placement.PositionM };
                if (Interpolation.HasComponent(entity))
                { var history = Interpolation[entity]; history.PreviousTransform = new RigidTransform(transform.Rotation, transform.Position); Interpolation[entity] = history; }
            }
        }
        [BurstCompile, WithNone(typeof(Parent), typeof(UniversePositionIsAnalytic))] partial struct SyncJob : IJobEntity
        {
            [ReadOnly] public NativeReference<RebaseDecision> Decision;
            void Execute(ref UniversePosition position, in LocalTransform transform)
                => position.Value = Decision.Value.NewOrigin + (double3)transform.Position;
        }
    }
}
