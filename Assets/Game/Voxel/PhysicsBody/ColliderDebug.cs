using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>SPEC-12 §5.14: grid-local box bounds for development overlays; never changes collider state.</summary>
    public static class ColliderDebug
    {
        /// Main thread, debug only (unsafe walk of the installed blob). Appends one entry per box leaf; returns the count; 0 if the root has no collider.
        public static unsafe int CollectBoxes(EntityManager em, Entity root, NativeList<ColliderDebugBox> output)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!em.Exists(root) || !em.HasComponent<PhysicsCollider>(root)) return 0;
            var blob = em.GetComponentData<PhysicsCollider>(root).Value; if (!blob.IsCreated) return 0;
            byte provisional = em.HasComponent<GridColliderState>(root) && em.GetComponentData<GridColliderState>(root).Phase == GridColliderPhase.Ready ? (byte)0 : (byte)1;
            int start = output.Length; Walk((Collider*)blob.GetUnsafePtr(), RigidTransform.identity, provisional, output); return output.Length - start;
#else
            return 0;
#endif
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static unsafe void Walk(Collider* collider, RigidTransform transform, byte provisional, NativeList<ColliderDebugBox> output)
        {
            if (collider->Type == ColliderType.Box)
            {
                var box = (BoxCollider*)collider;
                var pose = math.mul(transform, new RigidTransform(box->Orientation, box->Center));
                var rotation = new float3x3(pose.rot); var half = box->Size * .5f;
                float3 extent = math.abs(rotation.c0) * half.x + math.abs(rotation.c1) * half.y + math.abs(rotation.c2) * half.z;
                output.Add(new ColliderDebugBox { Min = pose.pos - extent, Max = pose.pos + extent, Provisional = provisional });
            }
            else if (collider->Type == ColliderType.Compound)
            {
                var compound = (CompoundCollider*)collider;
                for (int i = 0; i < compound->NumChildren; i++)
                {
                    // Children contain relative pointers; copying a Child would move the pointer's base address.
                    ref var child = ref compound->Children[i];
                    Walk(child.Collider, math.mul(transform, child.CompoundFromChild), provisional, output);
                }
            }
        }
#endif
    }
}
