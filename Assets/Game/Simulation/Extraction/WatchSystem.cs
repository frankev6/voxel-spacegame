using Game.Core;
using Game.Core.Components;
using Game.Core.Systems;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Simulation.Extraction
{
    /// <summary>SPEC-93 EXT-005, authority (ADR-0117 decider). Every watcher looks at every crew ship (a root with a Transponder): a ship
    /// that does not fit (dark, the wrong code for the zone it is in, towing a station core, or already wanted by the watcher's faction)
    /// and is in sight raises its Suspicion at the watcher's rate. A knock or snap the ship made, heard by a watcher within the noise's
    /// radius, adds a jump. Unseen for a while, suspicion fades. At 1 the ship is Wanted by the faction that saw it last (decision 7).
    /// Light and Sensor watchers in a blacked-out district see nothing; Ship and Crowd watchers carry their own eyes.
    /// Main thread: a place has tens of watchers and a crew has a few ships.</summary>
    [UpdateInGroup(typeof(GameplayPostPhysicsGroup)), UpdateAfter(typeof(CoreTetherSystem))]
    public partial struct WatchSystem : ISystem
    {
        /// <summary>Hits this close to a watcher are its own mount (a lamp sits in the hull it lights from).</summary>
        public const float MountClearance = 2.5f;
        EntityQuery crew, watchers, zones, districts;

        public void OnCreate(ref SystemState state)
        {
            crew = SystemAPI.QueryBuilder().WithAll<Transponder, Suspicion, LocalTransform>().Build();
            watchers = SystemAPI.QueryBuilder().WithAll<Watcher, LocalTransform>().Build();
            zones = SystemAPI.QueryBuilder().WithAll<AccessZone, LocalTransform>().Build();
            districts = SystemAPI.QueryBuilder().WithAll<District>().Build();
            state.RequireForUpdate<SimClock>(); state.RequireForUpdate(crew);
        }

        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();
            var em = state.EntityManager;
            uint tick = SystemAPI.GetSingleton<SimClock>().Tick;
            float dt = SimTime.FixedTimestep;
            bool physics = SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out var world);
            bool hasNoise = SystemAPI.TryGetSingleton<NoiseEvents>(out var noise) && noise.Current.IsCreated;
            using var ships = crew.ToEntityArray(Allocator.Temp);
            using var eyes = watchers.ToEntityArray(Allocator.Temp);
            using var boxes = zones.ToEntityArray(Allocator.Temp);
            using var power = districts.ToComponentDataArray<District>(Allocator.Temp);
            var hits = new NativeList<RaycastHit>(16, Allocator.Temp);
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            float time = tick * dt;

            foreach (var ship in ships)
            {
                var s = em.GetComponentData<Suspicion>(ship);
                var t = em.GetComponentData<Transponder>(ship);
                float3 at = em.GetComponentData<LocalTransform>(ship).Position;
                ulong wanted = em.HasComponent<Wanted>(ship) ? em.GetComponentData<Wanted>(ship).FactionMask : 0;
                var needs = AccessCode.None;
                foreach (var z in boxes)
                {
                    var box = em.GetComponentData<AccessZone>(z); var zt = em.GetComponentData<LocalTransform>(z);
                    float3 local = math.mul(math.inverse(zt.Rotation), at - zt.Position);
                    if (math.all(math.abs(local) <= box.HalfExtents) && box.Needs > needs) needs = box.Needs;
                }
                bool carriesCore = false;
                if (em.HasComponent<TowLink>(ship))
                {
                    var load = em.GetComponentData<TowLink>(ship).Load;
                    carriesCore = load != Entity.Null && em.Exists(load) && em.HasComponent<StationCore>(load);
                }
                bool misfit = t.On == 0 || !WatchRules.Fits(t.Code, needs) || carriesCore;
                Entity towed = em.HasComponent<TowLink>(ship) ? em.GetComponentData<TowLink>(ship).Load : Entity.Null;

                float raise = 0, best = 0; byte faction = s.Faction; s.Seen = 0; s.Heard = 0;
                foreach (var w in eyes)
                {
                    if (w == ship) continue;
                    var eye = em.GetComponentData<Watcher>(w);
                    bool alone = eye.Kind == WatcherKind.Ship || eye.Kind == WatcherKind.Crowd;
                    if (!alone && !Powered(power, eye.District)) continue;
                    bool hunting = eye.Faction < 64 && (wanted & (1ul << eye.Faction)) != 0;
                    if (!misfit && !hunting) continue;
                    var pose = em.GetComponentData<LocalTransform>(w);
                    float3 d = at - pose.Position; float dist = math.length(d);
                    if (dist > eye.Range || dist < 1e-3f) continue;
                    if (!alone)
                    {
                        float yaw = eye.SweepSeconds > 0 ? eye.SweepRadians * math.sin(2 * math.PI * (time / eye.SweepSeconds + eye.SweepPhase)) : 0;
                        float3 facing = math.mul(math.mul(pose.Rotation, quaternion.RotateY(yaw)), math.normalizesafe(eye.Facing, new float3(0, 0, 1)));
                        if (math.dot(facing, d / dist) < eye.CosHalfArc) continue;
                    }
                    if (physics && !InSight(world.CollisionWorld, ref hits, pose.Position, at, w, ship, towed)) continue;
                    float r = (eye.Rate > 0 ? eye.Rate : WatchRules.BaseRate(eye.Kind)) * WatchRules.Closeness(dist, eye.Range) * dt;
                    raise += r; s.Seen = 1;
                    if (r > best) { best = r; faction = eye.Faction; }
                }

                if (hasNoise)
                    foreach (var n in noise.Current)
                    {
                        if (n.Source != ship) continue;
                        foreach (var w in eyes)
                        {
                            var eye = em.GetComponentData<Watcher>(w);
                            bool alone = eye.Kind == WatcherKind.Ship || eye.Kind == WatcherKind.Crowd;
                            if (!alone && !Powered(power, eye.District)) continue;
                            if (math.distance(em.GetComponentData<LocalTransform>(w).Position, n.Position) > n.Radius) continue;
                            raise += WatchRules.HeardJump; s.Heard = 1; faction = eye.Faction;
                            break;   // one jump per noise, however many heard it
                        }
                    }

                if (raise > 0) { s.Value += raise; s.LastSeenTick = tick; s.Faction = faction; }
                else if ((tick - s.LastSeenTick) * dt > WatchRules.DecayDelaySeconds) s.Value = math.max(0, s.Value - WatchRules.DecayPerSecond * dt);
                if (s.Value >= 1)
                {
                    s.Value = 1;
                    if (s.Faction < 64 && (wanted & (1ul << s.Faction)) == 0)
                    {
                        var mark = new Wanted { FactionMask = wanted | (1ul << s.Faction) };
                        if (em.HasComponent<Wanted>(ship)) ecb.SetComponent(ship, mark); else ecb.AddComponent(ship, mark);
                    }
                }
                em.SetComponentData(ship, s);
            }
            hits.Dispose();
            ecb.Playback(em); ecb.Dispose();
        }

        static bool Powered(NativeArray<District> power, ushort id)
        {
            if (id == 0) return true;
            foreach (var d in power) if (d.Id == id) return d.Powered != 0;
            return true;
        }

        /// <summary>The first thing the ray from the watcher meets (past its mount, never the watcher itself) is the ship or its load.</summary>
        static bool InSight(in CollisionWorld world, ref NativeList<RaycastHit> hits, float3 from, float3 to, Entity self, Entity ship, Entity towed)
        {
            hits.Clear();
            float length = math.distance(from, to);
            var collector = new AllHitsCollector<RaycastHit>(1f, ref hits);
            world.CastRay(new RaycastInput { Start = from, End = to, Filter = CollisionFilter.Default }, ref collector);
            float bestFraction = 2; Entity first = Entity.Null;
            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                if (h.Entity == self || h.Fraction * length < MountClearance) continue;
                if (h.Fraction < bestFraction) { bestFraction = h.Fraction; first = h.Entity; }
            }
            return first == Entity.Null || first == ship || first == towed;
        }
    }
}
