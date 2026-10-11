using Game.Voxel.Model;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>Builds an owned persistent brick collider from its solid mask (SPEC-12 §5.3).</summary>
    public static class BrickColliderBuilder
    {
        /// <summary>Empty returns Null; one box stays a box; compounds own copies of their temporary children.</summary>
        public static BlobAssetReference<Collider> Build(in BrickMaskBits mask, float cellSize, int maxBoxes, float bevelFraction,
            CollisionFilter filter, Material material, ref NativeList<BrickBox> scratch, out int boxCount)
        {
            BrickBoxMerger.Merge(mask, maxBoxes, ref scratch);
            boxCount = scratch.Length;
            if (boxCount == 0) return BlobAssetReference<Collider>.Null;
            if (boxCount == 1) return CreateBox(scratch[0], cellSize, bevelFraction, filter, material);

            var children = new NativeArray<CompoundCollider.ColliderBlobInstance>(boxCount, Allocator.Temp);
            try
            {
                for (int i = 0; i < boxCount; i++)
                    children[i] = new CompoundCollider.ColliderBlobInstance
                    {
                        Collider = CreateBox(scratch[i], cellSize, bevelFraction, filter, material),
                        CompoundFromChild = RigidTransform.identity,
                    };
                return CompoundCollider.Create(children);
            }
            finally
            {
                // These newly built blobs have never been published to physics. The returned compound copied them.
                for (int i = 0; i < children.Length; i++)
                    if (children[i].Collider.IsCreated) children[i].Collider.Dispose();
                children.Dispose();
            }
        }

        static BlobAssetReference<Collider> CreateBox(BrickBox box, float cellSize, float bevelFraction,
            CollisionFilter filter, Material material)
        {
            var min = new float3(box.MinX, box.MinY, box.MinZ);
            var max = new float3(box.MaxX, box.MaxY, box.MaxZ);
            return BoxCollider.Create(new BoxGeometry
            {
                Center = (min + max + 1f) * (0.5f * cellSize),
                Size = (max - min + 1f) * cellSize,
                Orientation = quaternion.identity,
                BevelRadius = bevelFraction * cellSize,
            }, filter, material);
        }
    }
}
