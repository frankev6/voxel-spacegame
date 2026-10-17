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
    /// post for one check). PostA/B, CellsA/B: the post found at each head (the smallest static grid there: a post embedded in a wreck's hull is not
    /// the hull) and its solid cells then; later checks follow that grid. Lost: which posts were gone when the span went down.</summary>
    public struct WallSpan : IComponentData { public float2 A, B; public ushort Index; public byte Seen, Lost, MissA, MissB; public int CellsA, CellsB; public Entity PostA, PostB; }
    /// <summary>A zone switched off: ZoneSystem skips it (a wall span whose emitter was shot away).</summary>
    public struct ZoneOff : IComponentData { }

    /// <summary>RACE-016 (owner: "fences should be destructible"): every 10 ticks, on every machine, each live span checks that a static
    /// voxel grid still fills both emitter heads (TrackStructureBuilder.EmitterHeight) and that neither post has lost more than
    /// Integrity of the cells it had when first seen. Once a post it has seen fails (its head shot away, the post cut through, or
    /// rammed, or shot enough to lose a twentieth of itself), the span goes down for the rest of the race. The test is spatial, so host and guests agree without
    /// shared ids: the posts are the same replicated grids everywhere.</summary>
    [UpdateInGroup(typeof(GameplayPostPhysicsGroup)), UpdateBefore(typeof(ZoneSystem))]
    public partial struct WallEmitterSystem : ISystem
    {
        internal const float HeadHeight = 9, Probe = .45f;
        internal const int Misses = 3;
        internal const float Integrity = .95f;   // a post that has lost a twentieth of its cells (~8 bolts) no longer holds the field   // checks in a row (10 ticks apart) a seen post must be missing before its spans go down
        ulong postBlueprint, pylonBlueprint;
        public void OnCreate(ref SystemState state)
        {
            // only the track's own emitters count (TrackStructureBuilder ids): never a wreck hull or debris beside them
            postBlueprint = new Game.Core.ContentId("ship_track_post").Hash.Value; pylonBlueprint = new Game.Core.ContentId("ship_track_pylon").Hash.Value;
            state.RequireForUpdate<WallSpan>(); state.RequireForUpdate<PhysicsWorldSingleton>(); state.RequireForUpdate<SimClock>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.GetSingleton<SimClock>().Tick % 10 != 0) return;
            state.CompleteDependency();
            var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
            var masses = SystemAPI.GetComponentLookup<Game.Voxel.PhysicsBody.GridMass>(true);
            var sources = SystemAPI.GetComponentLookup<GridSource>(true);
            var hits = new NativeList<DistanceHit>(8, Allocator.Temp);
            var off = new NativeList<Entity>(Allocator.Temp);
            foreach (var (span, pose, e) in SystemAPI.Query<RefRW<WallSpan>, RefRO<LocalTransform>>().WithNone<ZoneOff>().WithEntityAccess())
            {
                float2 at = pose.ValueRO.Position.xz;
                bool a = Holds(world, sources, masses, at + span.ValueRO.A, hits, ref span.ValueRW.PostA, ref span.ValueRW.CellsA);
                bool b = Holds(world, sources, masses, at + span.ValueRO.B, hits, ref span.ValueRW.PostB, ref span.ValueRW.CellsB);
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

        /// The span's post at p still stands: found once (a track emitter post or gate pylon whose collider is within Probe of the head),
        /// then still in the physics world there with Integrity of the cells it was found with.
        bool Holds(in CollisionWorld world, in ComponentLookup<GridSource> sources, in ComponentLookup<Game.Voxel.PhysicsBody.GridMass> masses, float2 p,
            NativeList<DistanceHit> hits, ref Entity post, ref int firstCells)
        {
            hits.Clear();
            var input = new PointDistanceInput { Position = new float3(p.x, HeadHeight, p.y), MaxDistance = Probe, Filter = CollisionFilter.Default };
            world.CalculateDistance(input, ref hits);
            Entity found = Entity.Null;
            foreach (var h in hits)
            {
                if (post != Entity.Null) { if (h.Entity == post) { found = post; break; } continue; }
                if (!sources.HasComponent(h.Entity)) continue;
                ulong blueprint = sources[h.Entity].Blueprint.Value;
                if (blueprint == postBlueprint || blueprint == pylonBlueprint) { found = h.Entity; break; }
            }
            if (found == Entity.Null) return false;
            if (post == Entity.Null) { post = found; firstCells = masses.HasComponent(found) ? (int)masses[found].Data.CellCount : 0; }
            if (firstCells == 0 || !masses.HasComponent(post)) return true;
            return masses[post].Data.CellCount >= firstCells * Integrity;
        }
    }
}
