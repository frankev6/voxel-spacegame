using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Game.Voxel.Rendering
{
    /// <summary>SPEC-11 §4.2 queue, implemented by VOX-021. Main-thread callers complete access first.
    /// Jobs carry the queue containers only and construct a local view; never pass the Access guards into a job.</summary>
    public struct BrickMeshQueue : IComponentData
    {
        public NativeList<BrickMeshRequest> Damage, Neighbour, Spawn, Lod;
        public NativeArray<int> Heads;
        public NativeHashMap<Entity, byte> Pending;
        public Game.Data.Streams.GuardedHandles Access;
        public static BrickMeshQueue Create() => new BrickMeshQueue
        {
            Damage = new NativeList<BrickMeshRequest>(1024, Allocator.Persistent),
            Neighbour = new NativeList<BrickMeshRequest>(1024, Allocator.Persistent),
            Spawn = new NativeList<BrickMeshRequest>(1024, Allocator.Persistent),
            Lod = new NativeList<BrickMeshRequest>(1024, Allocator.Persistent),
            Heads = new NativeArray<int>(4, Allocator.Persistent),
            Pending = new NativeHashMap<Entity, byte>(4096, Allocator.Persistent),
            Access = Game.Data.Streams.GuardedHandles.Create(),
        };
        public void Dispose()
        {
            Access.Dispose();
            if (Damage.IsCreated) Damage.Dispose();
            if (Neighbour.IsCreated) Neighbour.Dispose();
            if (Spawn.IsCreated) Spawn.Dispose();
            if (Lod.IsCreated) Lod.Dispose();
            if (Heads.IsCreated) Heads.Dispose();
            if (Pending.IsCreated) Pending.Dispose();
        }
        public JobHandle Acquire(JobHandle dependsOn) => Access.Acquire(dependsOn);
        public void Release(JobHandle lastJob) => Access.Release(lastJob);
        public void CompleteAccess() => Access.CompleteAccess();
        public void Enqueue(Entity brick, Entity grid, MeshPriority p)
        {
            if (brick == Entity.Null) return;
            if (Pending.TryGetValue(brick, out byte priority))
            {
                if (priority <= (byte)p) return;
                // Retire the old entry permanently. A priority-only Pending check would resurrect it
                // if this brick is consumed, then queued again at that priority before the old list drains.
                var previous = List((MeshPriority)priority);
                for (int i = previous.Length - 1; i >= Heads[priority]; i--)
                    if (previous[i].Brick == brick) { previous[i] = default; break; }
            }
            List(p).Add(new BrickMeshRequest { Brick = brick, Grid = grid, Priority = p });
            Pending[brick] = (byte)p;
        }
        public bool TryDequeue(out BrickMeshRequest r)
        {
            for (int priority = 0; priority < 4; priority++)
            {
                var list = List((MeshPriority)priority);
                while (Heads[priority] < list.Length)
                {
                    r = list[Heads[priority]]; Heads[priority]++;
                    if (Heads[priority] == list.Length) { list.Clear(); Heads[priority] = 0; }
                    if (Pending.TryGetValue(r.Brick, out byte current) && current == priority)
                    { Pending.Remove(r.Brick); return true; }
                }
            }
            r = default; return false;
        }
        public int Count => Pending.Count;
        NativeList<BrickMeshRequest> List(MeshPriority p) => p switch
        {
            MeshPriority.Damage => Damage, MeshPriority.Neighbour => Neighbour,
            MeshPriority.Spawn => Spawn, MeshPriority.Lod => Lod,
            _ => throw new System.ArgumentOutOfRangeException(nameof(p))
        };
    }
}
