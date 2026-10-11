using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>Copies sorted brick colliders into an owned grid compound (SPEC-12 §5.4).</summary>
    public static class GridColliderAssembler
    {
        /// <summary>Requires non-empty children sorted by (z, y, x). The caller retains ownership of every child blob.</summary>
        public static BlobAssetReference<Collider> Assemble(NativeArray<BrickChild> children, float cellSize)
        {
            var instances = new NativeArray<CompoundCollider.ColliderBlobInstance>(children.Length, Allocator.Temp);
            try
            {
                for (int i = 0; i < children.Length; i++)
                    instances[i] = new CompoundCollider.ColliderBlobInstance
                    {
                        Collider = children[i].Blob,
                        CompoundFromChild = new RigidTransform(quaternion.identity, (float3)(children[i].Coord * 16) * cellSize),
                    };
                return CompoundCollider.Create(instances);
            }
            finally { instances.Dispose(); }
        }

        /// <summary>Lexicographic (z, y, x), without subtraction overflow.</summary>
        public static int CompareCoords(int3 a, int3 b)
        {
            if (a.z != b.z) return a.z < b.z ? -1 : 1;
            if (a.y != b.y) return a.y < b.y ? -1 : 1;
            if (a.x != b.x) return a.x < b.x ? -1 : 1;
            return 0;
        }
    }
}
