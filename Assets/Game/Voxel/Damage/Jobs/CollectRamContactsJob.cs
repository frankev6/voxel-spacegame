using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace Game.Voxel.Damage.Jobs
{
    internal struct RamContact : System.IComparable<RamContact>
    {
        public Entity A, B;
        public float3 Point, Normal;
        public float Impulse;
        public int Order;
        public int CompareTo(RamContact b)
        {
            int c = A.Index.CompareTo(b.A.Index); if (c == 0) c = B.Index.CompareTo(b.B.Index);
            return c == 0 ? Order.CompareTo(b.Order) : c;
        }
    }
    /// <summary>Unity normals point B-to-A; normalize the stored pair and normal to the SPEC-13 A-to-B convention.</summary>
    [BurstCompile] internal struct CollectRamContactsJob : ICollisionEventsJob
    {
        [ReadOnly] public ComponentLookup<VoxelGrid> Grids;
        [ReadOnly] public PhysicsWorld World;
        public NativeList<RamContact> Contacts;
        public void Execute(CollisionEvent collision)
        {
            Entity a = collision.EntityA, b = collision.EntityB;
            if (!Grids.HasComponent(a) && !Grids.HasComponent(b)) return;
            var details = collision.CalculateDetails(ref World);
            float3 normal = -collision.Normal;
            if (a.Index > b.Index) { var swap = a; a = b; b = swap; normal = -normal; }
            Contacts.Add(new RamContact { A = a, B = b, Point = details.AverageContactPointPosition, Normal = normal,
                Impulse = details.EstimatedImpulse, Order = Contacts.Length });
        }
    }
}
