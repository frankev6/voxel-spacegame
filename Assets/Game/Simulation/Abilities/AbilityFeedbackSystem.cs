using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Definitions;
using Game.Simulation.Commands;
using Game.Voxel.PresentationStreams;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Simulation.Abilities
{
    /// <summary>SPEC-24 §5.1 activation feedback (ADR-0117 applier, every machine): the activation cue and effect of every slot flagged
    /// ActivatedThisTick, whether AbilitySystem activated it here or a co-op guest applied the host's activation.</summary>
    [BurstCompile, UpdateInGroup(typeof(AbilityActivationGroup)), UpdateAfter(typeof(AbilitySystem)), UpdateAfter(typeof(AbilitySlotApplySystem))]
    public partial struct AbilityFeedbackSystem : ISystem
    {
        FeedbackJob job;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            job.Players = state.GetComponentLookup<PlayerShip>(true); job.Velocities = state.GetComponentLookup<PhysicsVelocity>(true);
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<AbilityDefsRef>();
            state.RequireForUpdate<FxEventQueue>(); state.RequireForUpdate<SfxEventQueue>();
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            job.Players.Update(ref state); job.Velocities.Update(ref state);
            job.Definitions = SystemAPI.GetSingleton<AbilityDefsRef>().Value;
            var fx = SystemAPI.GetSingleton<FxEventQueue>(); var sfx = SystemAPI.GetSingleton<SfxEventQueue>();
            job.Fx = fx.Queue.AsParallelWriter(); job.Sfx = sfx.Queue.AsParallelWriter();
            state.Dependency = job.ScheduleParallel(sfx.Access.BeginRead(fx.Access.BeginRead(state.Dependency)));
            fx.Access.EndRead(state.Dependency); sfx.Access.EndRead(state.Dependency);
        }

        [BurstCompile]
        partial struct FeedbackJob : IJobEntity
        {
            [ReadOnly] public BlobAssetReference<AbilityDefsBlob> Definitions;
            [ReadOnly] public ComponentLookup<PlayerShip> Players;
            [ReadOnly] public ComponentLookup<PhysicsVelocity> Velocities;
            // Atomic presentation-only writers, with drain/clear lifetime guards owned by this system.
            [NativeDisableContainerSafetyRestriction] public NativeQueue<FxEvent>.ParallelWriter Fx;
            [NativeDisableContainerSafetyRestriction] public NativeQueue<SfxEvent>.ParallelWriter Sfx;
            void Execute(Entity root, in LocalTransform transform, in DynamicBuffer<AbilitySlot> slots)
            {
                bool player = Players.HasComponent(root);
                for (int i = 0; i < slots.Length; i++)
                {
                    var slot = slots[i];
                    if ((slot.Flags & AbilitySlotFlags.ActivatedThisTick) == 0 || slot.AbilityIndex >= Definitions.Value.Abilities.Length) continue;
                    ref var def = ref Definitions.Value.Abilities[slot.AbilityIndex];
                    if (def.ActivationCue.Value != 0) Sfx.Enqueue(new SfxEvent { Cue = def.ActivationCue, Pos = transform.Position, Volume = 1, Flags = player ? SfxEventFlags.PlayerSource : 0 });
                    if (def.ActivationFx.Value != 0) Fx.Enqueue(new FxEvent { Kind = FxKind.Muzzle, Fx = def.ActivationFx, Pos = transform.Position,
                        Dir = math.rotate(transform.Rotation, new float3(0, 0, 1)), Vel = Velocities.HasComponent(root) ? Velocities[root].Linear : default,
                        Scale = 2, Flags = player ? FxEventFlags.PlayerSource : 0 });
                }
            }
        }
    }
}
