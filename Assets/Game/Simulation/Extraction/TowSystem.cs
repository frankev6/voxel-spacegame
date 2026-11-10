using Game.Core;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Combat;
using Game.Voxel.Damage;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Simulation.Extraction
{
    /// <summary>SPEC-93 EXT-002, every machine (ADR-0117 applier: every machine has the same poses, so the same pulls). Each TowLink pulls
    /// its ship and load toward each other when the cable is taut, as a spring and damper tuned on the pair's reduced mass, through
    /// DamageStreams.Impulses (ProducerKind.Tow): the ship's flight assist carries the pull (SPEC-91), so a heavy load makes the ship
    /// heavy. Over BreakForce the cable snaps. A load whose velocity changed by more than KnockSpeed in a tick beyond what the cable
    /// did knocked into something: a NoiseEvent at its side. Main thread: there are a handful of links.</summary>
    [UpdateInGroup(typeof(GameplayPostPhysicsGroup)), UpdateAfter(typeof(DamageStreamsSystem)), UpdateBefore(typeof(ImpulseApplySystem))]
    public partial struct TowSystem : ISystem
    {
        public const float DefaultFrequency = 1.1f, DefaultDamping = .8f;
        /// <summary>m/s of unexplained velocity change in one tick that counts as a knock.</summary>
        public const float KnockSpeed = 3.5f;
        /// <summary>m a knock carries per m/s of the hit, at least MinKnockRadius.</summary>
        public const float KnockRadiusPerMs = 40f, MinKnockRadius = 120f, SnapRadius = 400f;
        /// <summary>Ticks between two knocks of the same load (a load scraping along a wall knocks a few times a second, not every tick).</summary>
        public const uint KnockCooldownTicks = 12;

        EntityQuery links;

        public void OnCreate(ref SystemState state)
        {
            links = SystemAPI.QueryBuilder().WithAll<TowLink, LocalTransform>().Build();
            if (!SystemAPI.HasSingleton<NoiseEvents>())
            {
                var e = state.EntityManager.CreateEntity(typeof(NoiseEvents));
                state.EntityManager.SetName(e, "NoiseEvents");
                state.EntityManager.SetComponentData(e, new NoiseEvents { Current = new NativeList<NoiseEvent>(16, Allocator.Persistent) });
            }
            state.RequireForUpdate<DamageStreams>(); state.RequireForUpdate<SimClock>();
        }

        public void OnDestroy(ref SystemState state)
        {
            if (SystemAPI.TryGetSingletonRW<NoiseEvents>(out var noise) && noise.ValueRO.Current.IsCreated) noise.ValueRW.Current.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            uint tick = SystemAPI.GetSingleton<SimClock>().Tick;
            var noiseRef = SystemAPI.GetSingletonRW<NoiseEvents>();
            noiseRef.ValueRW.Tick = tick;
            var noise = noiseRef.ValueRO.Current;   // a NativeList is a handle: adds through the copy land in the singleton's list
            noise.Clear();
            if (links.IsEmpty) return;
            state.CompleteDependency();
            var em = state.EntityManager;
            var streams = SystemAPI.GetSingleton<DamageStreams>();
            using var entities = links.ToEntityArray(Allocator.Temp);
            uint sequence = 0;
            var ecb = new EntityCommandBuffer(Allocator.Temp);   // removals after the loop: no structural change while reading
            var writer = streams.Impulses.BeginWrite(ref state, ProducerKind.Tow, 1);
            writer.BeginForEachIndex(0);
            foreach (var ship in entities)
            {
                var link = em.GetComponentData<TowLink>(ship);
                if (!Valid(em, ship, link.Load))
                {
                    if (link.Load != Entity.Null && em.Exists(link.Load) && em.HasComponent<TowLoad>(link.Load)
                        && em.GetComponentData<TowLoad>(link.Load).Tower == ship) ecb.RemoveComponent<TowLoad>(link.Load);
                    ecb.RemoveComponent<TowLink>(ship);
                    continue;
                }
                var a = em.GetComponentData<LocalTransform>(ship); var b = em.GetComponentData<LocalTransform>(link.Load);
                var ma = em.GetComponentData<PhysicsMass>(ship); var mb = em.GetComponentData<PhysicsMass>(link.Load);
                var va = em.GetComponentData<PhysicsVelocity>(ship).Linear; var vb = em.GetComponentData<PhysicsVelocity>(link.Load).Linear;
                var load = em.GetComponentData<TowLoad>(link.Load);

                // knocks: the load's velocity changed by more than the cable explains
                if (load.ExpectedTick != 0 && tick - load.ExpectedTick <= 2 && tick - load.ExpectedTick > 0)
                {
                    float hit = math.length(vb - load.ExpectedVelocity);
                    if (hit > KnockSpeed && (load.LastKnockTick == 0 || tick >= load.LastKnockTick + KnockCooldownTicks))
                    {
                        float3 dir = math.normalizesafe(load.ExpectedVelocity - vb, new float3(0, 0, 1));
                        noise.Add(new NoiseEvent { Position = b.Position + dir * math.max(2, Radius(em, link.Load)),
                            Radius = math.max(MinKnockRadius, hit * KnockRadiusPerMs), Source = ship, Kind = (byte)NoiseKind.Knock });
                        load.LastKnockTick = tick;
                    }
                }

                float3 pa = a.TransformPoint(link.ShipAnchor), pb = b.TransformPoint(link.LoadAnchor);
                float3 d = pb - pa; float dist = math.length(d);
                float3 dvb = float3.zero;
                link.Tension = 0;
                if (dist > link.Length && dist > 1e-4f)
                {
                    float3 dir = d / dist;
                    float inva = ma.InverseMass, invb = mb.InverseMass;
                    if (inva + invb > 0)
                    {
                        float mu = 1 / (inva + invb);                 // reduced mass (kg); a static anchor counts as infinite
                        float w = 2 * math.PI * (link.Frequency > 0 ? link.Frequency : DefaultFrequency);
                        float z = link.DampingRatio > 0 ? link.DampingRatio : DefaultDamping;
                        float stretch = dist - link.Length, closing = math.dot(vb - va, dir);   // > 0: moving apart
                        float force = math.max(0, mu * w * w * stretch + 2 * z * mu * w * closing);
                        link.Tension = force;
                        if (link.BreakForce > 0 && force > link.BreakForce)
                        {
                            noise.Add(new NoiseEvent { Position = (pa + pb) * .5f, Radius = SnapRadius, Source = ship, Kind = (byte)NoiseKind.Snap });
                            ecb.RemoveComponent<TowLoad>(link.Load); ecb.RemoveComponent<TowLink>(ship);
                            continue;
                        }
                        float j = force * SimTime.FixedTimestep;
                        if (inva > 0)
                            writer.Write(new ImpulseRequest { Body = ship, WorldImpulse = dir * j, WorldPoint = pa,
                                Flags = ImpulseRequestFlags.AtCenterOfMass, Producer = ProducerKind.Tow, Sequence = sequence++ });
                        if (invb > 0)
                        {
                            writer.Write(new ImpulseRequest { Body = link.Load, WorldImpulse = -dir * j, WorldPoint = pb,
                                Producer = ProducerKind.Tow, Sequence = sequence++ });
                            dvb = -dir * j * invb;
                        }
                    }
                }
                load.ExpectedVelocity = vb + dvb; load.ExpectedTick = tick;
                em.SetComponentData(link.Load, load);
                em.SetComponentData(ship, link);
            }
            writer.EndForEachIndex();
            streams.Impulses.EndWrite(default);
            ecb.Playback(em); ecb.Dispose();
        }

        static bool Valid(EntityManager em, Entity ship, Entity load) =>
            load != Entity.Null && em.Exists(load) && em.HasComponent<TowLoad>(load) && em.GetComponentData<TowLoad>(load).Tower == ship
            && em.HasComponent<LocalTransform>(load) && em.HasComponent<PhysicsVelocity>(load) && em.HasComponent<PhysicsMass>(load)
            && em.HasComponent<PhysicsVelocity>(ship) && em.HasComponent<PhysicsMass>(ship)
            && !em.HasComponent<Hulk>(ship);

        static float Radius(EntityManager em, Entity e)
        {
            if (!em.HasComponent<PhysicsMass>(e)) return 2;
            var m = em.GetComponentData<PhysicsMass>(e);
            return math.max(2, m.AngularExpansionFactor);
        }
    }
}
