using Game.Core.Components;
using Game.Core.Systems;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Simulation.Hazards
{
    /// <summary>RACE-016: a field-wall span held up by two voxel emitter posts. A and B are the posts' centres relative to the zone's
    /// live pose (so origin rebases carry them). Seen: bit 0/1 once post A/B has been found (grids spawn a few ticks late, and reach a
    /// guest by replication). MissA/B: consecutive checks a seen post has been missing (a collider rebuild or an origin rebase can hide a
    /// post for one check). Lost: which posts were gone when the span went down.</summary>
    public struct WallSpan : IComponentData { public float2 A, B; public ushort Index; public byte Seen, Lost, MissA, MissB; }
    /// <summary>A zone switched off: ZoneSystem skips it (a wall span whose emitter was shot away).</summary>
    public struct ZoneOff : IComponentData { }

    /// <summary>RACE-016 (owner: "fences should be destructible"): every 10 ticks, on every machine, each live span checks that a static
    /// voxel grid still fills both emitter heads (TrackStructureBuilder.EmitterHeight). Once a head it has seen is gone (shot away, the
    /// post cut through below it), the span goes down for the rest of the race. The test is spatial, so host and guests agree without
    /// shared ids: the posts are the same replicated grids everywhere.</summary>
    [UpdateInGroup(typeof(GameplayPostPhysicsGroup)), UpdateBefore(typeof(ZoneSystem))]
    public partial struct WallEmitterSystem : ISystem
    {
        internal const float HeadHeight = 9, Probe = .45f;
        internal const int Misses = 3;   // checks in a row (10 ticks apart) a seen post must be missing before its spans go down
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WallSpan>(); state.RequireForUpdate<PhysicsWorldSingleton>(); state.RequireForUpdate<SimClock>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.GetSingleton<SimClock>().Tick % 10 != 0) return;
            state.CompleteDependency();
            var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
            var grids = SystemAPI.GetComponentLookup<VoxelGrid>(true);
            var hits = new NativeList<DistanceHit>(8, Allocator.Temp);
            var off = new NativeList<Entity>(Allocator.Temp);
            foreach (var (span, pose, e) in SystemAPI.Query<RefRW<WallSpan>, RefRO<LocalTransform>>().WithNone<ZoneOff>().WithEntityAccess())
            {
                float2 at = pose.ValueRO.Position.xz;
                bool a = Present(world, grids, at + span.ValueRO.A, hits), b = Present(world, grids, at + span.ValueRO.B, hits);
                byte seen = (byte)(span.ValueRO.Seen | (a ? 1 : 0) | (b ? 2 : 0));
                byte missA = (byte)((seen & 1) != 0 && !a ? math.min(255, span.ValueRO.MissA + 1) : 0);
                byte missB = (byte)((seen & 2) != 0 && !b ? math.min(255, span.ValueRO.MissB + 1) : 0);
                byte lost = (byte)((missA >= Misses ? 1 : 0) | (missB >= Misses ? 2 : 0));
                span.ValueRW.Seen = seen; span.ValueRW.MissA = missA; span.ValueRW.MissB = missB; span.ValueRW.Lost = lost;
                if (lost != 0) off.Add(e);
            }
            foreach (var e in off) state.EntityManager.AddComponent<ZoneOff>(e);
            hits.Dispose(); off.Dispose();
        }

        /// A static voxel grid's collider within Probe of the emitter head.
        static bool Present(in CollisionWorld world, in ComponentLookup<VoxelGrid> grids, float2 p, NativeList<DistanceHit> hits)
        {
            hits.Clear();
            var input = new PointDistanceInput { Position = new float3(p.x, HeadHeight, p.y), MaxDistance = Probe, Filter = CollisionFilter.Default };
            if (!world.CalculateDistance(input, ref hits)) return false;
            foreach (var h in hits)
                if (grids.HasComponent(h.Entity))
                {
                    var g = grids[h.Entity];
                    if (g.Kind == GridKind.Station) return true;
                }
            return false;
        }
    }
}
