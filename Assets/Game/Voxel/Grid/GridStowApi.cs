using Game.Voxel.Damage;
using Unity.Physics;
using Unity.Physics.GraphicsIntegration;
using Unity.Transforms;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Voxel.Grid
{

    /// <summary>Implements SPEC-10 §4.6. Tick-exact entry and exit of pre-spawned grids (ADR-0015, §5.13). Fixed-step callers record into the GameplayEnd ECB.</summary>
    public static class GridStowApi
    {
        /// <summary>Disables the root and its LinkedEntityGroup (ecb.SetEnabled(root, false)). Precondition: GridSpawning and GridBodyInit are disabled.</summary>
        public static void Stow(EntityCommandBuffer.ParallelWriter ecb, int sortKey, Entity root) { ecb.SetEnabled(sortKey, root, false); }
        /// <summary>Re-enables the group and sets LocalTransform {pose, scale 1}, PhysicsVelocity {linear, angular} and ensures and resets
        /// PhysicsGraphicalInterpolationBuffer {PreviousTransform = pose, PreviousVelocity = velocity} (no smoothing streak).
        /// graceOther != Entity.Null also adds SPEC-13 SplitGrace { Other = graceOther, UntilTick = graceUntilTick } (launch from a
        /// carrier without contacts until the craft is clear; ADR-0017).</summary>
        public static void Unstow(EntityCommandBuffer.ParallelWriter ecb, int sortKey, Entity root, in RigidTransform pose, float3 linear, float3 angular,
                                  Entity graceOther = default, uint graceUntilTick = 0) {
            var velocity = new PhysicsVelocity { Linear = linear, Angular = angular };
            ecb.SetComponent(sortKey, root, LocalTransform.FromPositionRotationScale(pose.pos, pose.rot, 1));
            ecb.SetComponent(sortKey, root, velocity);
            // ECB cannot inspect component presence. AddComponent(value) also replaces an existing value at playback.
            ecb.AddComponent(sortKey, root, new PhysicsGraphicalInterpolationBuffer { PreviousTransform = pose, PreviousVelocity = velocity });
            if (graceOther != Entity.Null) ecb.AddComponent(sortKey, root, new SplitGrace { Other = graceOther, UntilTick = graceUntilTick });
            ecb.SetEnabled(sortKey, root, true);
        }
    }
}
