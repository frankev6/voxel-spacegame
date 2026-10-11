using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.GraphicsIntegration;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>SPEC-12 §5.6: initial body component sets and query-only provisional geometry.</summary>
    public static class GridBodyFactory
    {
        public static ComponentTypeSet DynamicBodyTypes()
        {
            var types = new FixedList128Bytes<ComponentType> { ComponentType.ReadWrite<PhysicsCollider>(), ComponentType.ReadWrite<PhysicsVelocity>(),
                ComponentType.ReadWrite<PhysicsMass>(), ComponentType.ReadWrite<PhysicsDamping>(), ComponentType.ReadWrite<PhysicsGraphicalSmoothing>(),
                ComponentType.ReadWrite<PhysicsGraphicalInterpolationBuffer>() };
            return new ComponentTypeSet(types);
        }
        public static ComponentTypeSet KinematicBodyTypes() => new ComponentTypeSet(ComponentType.ReadWrite<PhysicsCollider>(), ComponentType.ReadWrite<PhysicsVelocity>(),
            ComponentType.ReadWrite<PhysicsMass>(), ComponentType.ReadWrite<PhysicsGraphicalSmoothing>(), ComponentType.ReadWrite<PhysicsGraphicalInterpolationBuffer>());
        public static ComponentTypeSet StaticBodyTypes() => new ComponentTypeSet(ComponentType.ReadWrite<PhysicsCollider>());
        public static PhysicsWorldIndex WorldIndex() => new PhysicsWorldIndex(0);   // add with AddSharedComponent
        /// One box over cells cellMin..cellMax (inclusive), grid-local metres, BevelRadius 0, ProvisionalMaterial (EditorPreviewMaterial when editorPreview),
        /// filter Grid (EditorPreview when editorPreview). The caller owns the blob until a grid record adopts it (§5.6 step 3).
        public static BlobAssetReference<Collider> CreateProvisionalCollider(int3 cellMin, int3 cellMax, float cellSize, bool editorPreview)
            => BoxCollider.Create(new BoxGeometry
            {
                Center = ((float3)cellMin + (float3)cellMax + 1) * (.5f * cellSize),
                Size = ((float3)cellMax - (float3)cellMin + 1) * cellSize, Orientation = quaternion.identity, BevelRadius = 0,
            }, editorPreview ? PhysicsLayers.EditorPreview : PhysicsLayers.Grid,
                editorPreview ? PhysicsLayers.EditorPreviewMaterial : PhysicsLayers.ProvisionalMaterial);
        public static PhysicsDamping DefaultDamping(in PhysicsVoxelConfig c) => new PhysicsDamping { Linear = c.GridLinearDamping, Angular = c.GridAngularDamping };
    }
}
