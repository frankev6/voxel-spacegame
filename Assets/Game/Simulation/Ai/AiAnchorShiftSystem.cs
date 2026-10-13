using Game.Core;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Simulation.Commands;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.Simulation.Ai
{
    /// <summary>Registers AI composition and maintains local-frame anchors/goals through rebases (SPEC-23 §5.3, ADR-0015).</summary>
    [BurstCompile]
    [CreateAfter(typeof(GridSpawnSystem))]
    [UpdateInGroup(typeof(ShipCommandGroup))]
    // TODO(WPN-005): UpdateAfter(PlayerTargetingSystem) when that specified predecessor exists (SPEC-23 Q12).
    public partial struct AiAnchorShiftSystem : ISystem
    {
        ComponentLookup<OriginShiftRecord> shifts;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            AiSetup.Register(SystemAPI.GetSingletonBuffer<RootComponentRegistration>());
            state.RequireForUpdate<GameplayActive>();
            state.RequireForUpdate<OriginShiftRecord>();
            shifts = state.GetComponentLookup<OriginShiftRecord>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            shifts.Update(ref state);
            state.Dependency = new AnchorShiftJob
            {
                Shifts = shifts,
                ShiftEntity = SystemAPI.GetSingletonEntity<OriginShiftRecord>()
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        [WithNone(typeof(PlayerShip))]
        partial struct AnchorShiftJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<OriginShiftRecord> Shifts;
            public Entity ShiftEntity;

            void Execute(ref AiBrain brain, in LocalTransform pose)
            {
                var shift = Shifts[ShiftEntity];
                if (brain.Initialized == 0)
                {
                    brain.State = AiState.Idle;
                    brain.Anchor = brain.GoalPosition = pose.Position;
                    brain.GoalHeadingYaw = Planar.YawFromForward(math.rotate(pose.Rotation, new float3(0, 0, 1)));
                    brain.OrbitSign = 1;
                    // The current pose already belongs to this frame, including for a post-rebase spawn.
                    brain.ShiftSequence = shift.Sequence;
                    brain.Initialized = 1;
                    return;
                }
                if (brain.ShiftSequence == shift.Sequence) return;
                if (shift.StarSystemChanged == 1 || unchecked(shift.Sequence - brain.ShiftSequence) > 1)
                    brain.Anchor = brain.GoalPosition = pose.Position;
                else
                {
                    brain.Anchor += shift.Delta;
                    brain.GoalPosition += shift.Delta;
                }
                brain.ShiftSequence = shift.Sequence;
            }
        }
    }
}
