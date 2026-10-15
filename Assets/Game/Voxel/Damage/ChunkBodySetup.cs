using Game.Voxel.PhysicsBody;
using Game.Voxel.PresentationStreams;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.GraphicsIntegration;
using Unity.Transforms;

namespace Game.Voxel.Damage
{
    /// <summary>ADR-0117: the one setup of a chunk body's components, for SplitSystem's pooled chunks on the authority and for a co-op
    /// guest's replica of one. Only the mass differs: dynamic on the authority, kinematic (pose from snapshots) on a guest.</summary>
    public static class ChunkBodySetup
    {
        public static ComponentType[] Types() => new ComponentType[]
        {
            typeof(LocalTransform), typeof(LocalToWorld), typeof(PhysicsVelocity), typeof(PhysicsMass), typeof(PhysicsCollider), typeof(PhysicsDamping),
            typeof(PhysicsWorldIndex), typeof(PhysicsGraphicalSmoothing), typeof(PhysicsGraphicalInterpolationBuffer), typeof(ChunkBody),
            typeof(ChunkContactState), typeof(ChunkCell), typeof(ChunkColliderBlob), typeof(SplitGrace), typeof(SplitOrigin), typeof(ChunkDissolve),
            typeof(Disabled)
        };

        /// <summary>Writes every component of a live chunk body onto <paramref name="entity"/>, an entity of <see cref="Types"/>.</summary>
        public static void Write(ref EntityCommandBuffer ecb, Entity entity, in LocalTransform pose, in PhysicsMass mass, in PhysicsVelocity velocity,
            BlobAssetReference<Collider> collider, in ChunkBody body, NativeArray<ChunkCell> cells, Entity parent, uint graceUntilTick, byte smoothing)
        {
            ecb.RemoveComponent<Disabled>(entity);
            ecb.SetComponent(entity, pose); ecb.SetComponent(entity, mass); ecb.SetComponent(entity, velocity);
            ecb.SetComponent(entity, new PhysicsCollider { Value = collider }); ecb.SetComponent(entity, new ChunkColliderBlob { Value = collider });
            ecb.SetComponent(entity, new PhysicsDamping { Linear = .02f, Angular = .05f });
            ecb.SetComponent(entity, new PhysicsGraphicalSmoothing { ApplySmoothing = smoothing });   // replicas interpolate snapshot poses
            ecb.SetComponent(entity, new PhysicsGraphicalInterpolationBuffer { PreviousTransform = new RigidTransform(pose.Rotation, pose.Position), PreviousVelocity = velocity });
            ecb.SetComponent(entity, new ChunkContactState());
            ecb.SetComponent(entity, body); ecb.SetComponent(entity, new SplitGrace { Other = parent, UntilTick = graceUntilTick });
            ecb.SetComponent(entity, new SplitOrigin { Parent = parent });
            ecb.SetBuffer<ChunkCell>(entity).CopyFrom(cells);
        }
    }
}
