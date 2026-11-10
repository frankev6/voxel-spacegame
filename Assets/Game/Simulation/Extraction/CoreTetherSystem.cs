using Game.Core.Components;
using Game.Core.Systems;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.Simulation.Extraction
{
    /// <summary>SPEC-93 EXT-004, authority (ADR-0117 decider). Cuts tethers (requests, a destroyed or half-destroyed post), blacks out
    /// each cut tether's district, and frees the core when its last tether goes. A cut is loud (a NoiseEvent of kind Cut at the anchor).</summary>
    [UpdateInGroup(typeof(GameplayPostPhysicsGroup)), UpdateAfter(typeof(TowSystem))]
    public partial struct CoreTetherSystem : ISystem
    {
        public const float CutNoiseRadius = 260f;
        EntityQuery tethers, cores, districts;

        public void OnCreate(ref SystemState state)
        {
            tethers = SystemAPI.QueryBuilder().WithAllRW<CoreTether>().Build();
            cores = SystemAPI.QueryBuilder().WithAllRW<StationCore>().Build();
            districts = SystemAPI.QueryBuilder().WithAllRW<District>().Build();
            if (!SystemAPI.HasSingleton<TetherCutRequests>())
            {
                var e = state.EntityManager.CreateEntity(typeof(TetherCutRequests));
                state.EntityManager.SetName(e, "TetherCutRequests");
                state.EntityManager.SetComponentData(e, new TetherCutRequests { Pending = new NativeList<Entity>(4, Allocator.Persistent) });
            }
            state.RequireForUpdate<SimClock>(); state.RequireForUpdate(tethers);
        }

        public void OnDestroy(ref SystemState state)
        {
            if (SystemAPI.TryGetSingletonRW<TetherCutRequests>(out var r) && r.ValueRO.Pending.IsCreated) r.ValueRW.Pending.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();
            var em = state.EntityManager;
            uint tick = SystemAPI.GetSingleton<SimClock>().Tick;
            var requests = SystemAPI.GetSingleton<TetherCutRequests>().Pending;
            bool hasNoise = SystemAPI.TryGetSingleton<NoiseEvents>(out var noise) && noise.Current.IsCreated;
            using var all = tethers.ToEntityArray(Allocator.Temp);
            bool any = false;
            foreach (var e in all)
            {
                var t = em.GetComponentData<CoreTether>(e);
                if (t.Cut != 0) continue;
                bool cut = requests.Contains(e);
                if (!cut && t.Post != Entity.Null)
                {
                    if (!em.Exists(t.Post)) cut = true;
                    else
                    {
                        int cells = SolidCells(em, t.Post);
                        if (t.PostCells == 0) { t.PostCells = cells; em.SetComponentData(e, t); }
                        else if (cells * 2 < t.PostCells) cut = true;
                    }
                }
                if (!cut) continue;
                t.Cut = 1; t.CutTick = tick; em.SetComponentData(e, t); any = true;
                if (hasNoise)
                {
                    float3 at = t.Holder != Entity.Null && em.Exists(t.Holder) ? em.GetComponentData<LocalTransform>(t.Holder).TransformPoint(t.Anchor) : t.Anchor;
                    noise.Current.Add(new NoiseEvent { Position = at, Radius = CutNoiseRadius, Source = Entity.Null, Kind = (byte)NoiseKind.Cut });
                }
                Blackout(em, t.District, tick);
            }
            requests.Clear();
            if (!any) return;
            Recount(em, all, tick);
        }

        void Blackout(EntityManager em, ushort id, uint tick)
        {
            if (id == 0) return;
            using var ds = districts.ToEntityArray(Allocator.Temp);
            foreach (var d in ds)
            {
                var district = em.GetComponentData<District>(d);
                if (district.Id != id || district.Powered == 0) continue;
                district.Powered = 0; district.ChangedTick = tick; em.SetComponentData(d, district);
            }
        }

        void Recount(EntityManager em, NativeArray<Entity> all, uint tick)
        {
            using var cs = cores.ToEntityArray(Allocator.Temp);
            foreach (var c in cs)
            {
                var core = em.GetComponentData<StationCore>(c);
                int whole = 0;
                foreach (var e in all) { var t = em.GetComponentData<CoreTether>(e); if (t.Core == c && t.Cut == 0) whole++; }
                core.Tethers = (byte)whole;
                if (whole == 0 && core.Freed == 0) { core.Freed = 1; core.FreedTick = tick; }
                em.SetComponentData(c, core);
            }
        }

        public static int SolidCells(EntityManager em, Entity root)
        {
            if (!em.HasBuffer<BrickSlot>(root)) return 0;
            int count = 0;
            foreach (var slot in em.GetBuffer<BrickSlot>(root, true))
                if (slot.Brick != Entity.Null && em.Exists(slot.Brick) && em.HasComponent<Brick>(slot.Brick)) count += em.GetComponentData<Brick>(slot.Brick).SolidCount;
            return count;
        }
    }
}
