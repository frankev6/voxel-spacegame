using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Game.Voxel.Model;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>Implements SPEC-12 §4 (physics contract).</summary>
    public static class GridBodyFactory
    {
        public static ComponentTypeSet DynamicBodyTypes() => throw new System.NotImplementedException("PHY-008");    // PhysicsCollider, PhysicsVelocity, PhysicsMass, PhysicsDamping, PhysicsGraphicalSmoothing, PhysicsGraphicalInterpolationBuffer
        public static ComponentTypeSet KinematicBodyTypes() => throw new System.NotImplementedException("PHY-008");  // PhysicsCollider, PhysicsVelocity, PhysicsMass, PhysicsGraphicalSmoothing, PhysicsGraphicalInterpolationBuffer
        public static ComponentTypeSet StaticBodyTypes() => throw new System.NotImplementedException("PHY-008");     // PhysicsCollider
        public static PhysicsWorldIndex WorldIndex() => new PhysicsWorldIndex(0);   // add with AddSharedComponent
        /// One box over cells cellMin..cellMax (inclusive), grid-local metres, BevelRadius 0, ProvisionalMaterial (EditorPreviewMaterial when editorPreview),
        /// filter Grid (EditorPreview when editorPreview). The caller owns the blob until a grid record adopts it (§5.6 step 3).
        public static BlobAssetReference<Collider> CreateProvisionalCollider(int3 cellMin, int3 cellMax, float cellSize, bool editorPreview) => throw new System.NotImplementedException("PHY-008");
        public static PhysicsDamping DefaultDamping(in PhysicsVoxelConfig c) => new PhysicsDamping { Linear = c.GridLinearDamping, Angular = c.GridAngularDamping };
    }
}
