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
    public struct ColliderBatchQueue : IComponentData
    {
        internal NativeReference<ColliderBatchState> State;
        internal ColliderBatchContainers B;
        public static ColliderBatchQueue Create() => new ColliderBatchQueue
        {
            State = new NativeReference<ColliderBatchState>(Allocator.Persistent),
            B = new ColliderBatchContainers
            {
                Bricks = new NativeList<BrickBuildItem>(512, Allocator.Persistent),
                Masks = new NativeList<BrickMaskBits>(512, Allocator.Persistent),
                Results = new NativeList<BrickBuildResult>(512, Allocator.Persistent),
                Assemblies = new NativeList<AssemblyItem>(64, Allocator.Persistent),
                AssemblyChildren = new NativeList<BrickChild>(512, Allocator.Persistent),
                AssemblyApply = new NativeList<int>(512, Allocator.Persistent),
                AssemblyResults = new NativeList<AssemblyResult>(64, Allocator.Persistent),
            },
        };
        public void Dispose()
        {
            if (State.IsCreated) State.Value.Handle.Complete();
            // Only results own blobs; AssemblyChildren are borrowed snapshots of the cache's children.
            if (B.Results.IsCreated)
            {
                for (int i = 0; i < B.Results.Length; i++)
                {
                    var blob = B.Results[i].Blob;
                    if (blob.IsCreated) blob.Dispose();
                }
                B.Results.Dispose();
            }
            if (B.AssemblyResults.IsCreated)
            {
                for (int i = 0; i < B.AssemblyResults.Length; i++)
                {
                    var blob = B.AssemblyResults[i].Compound;
                    if (blob.IsCreated) blob.Dispose();
                }
                B.AssemblyResults.Dispose();
            }
            if (B.Bricks.IsCreated) B.Bricks.Dispose();
            if (B.Masks.IsCreated) B.Masks.Dispose();
            if (B.Assemblies.IsCreated) B.Assemblies.Dispose();
            if (B.AssemblyChildren.IsCreated) B.AssemblyChildren.Dispose();
            if (B.AssemblyApply.IsCreated) B.AssemblyApply.Dispose();
            if (State.IsCreated) State.Dispose();
        }
    }
}
