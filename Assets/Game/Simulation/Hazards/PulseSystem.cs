using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Combat;
using Game.Data.Hazards;
using Game.Simulation.Power;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Simulation.Hazards
{
    /// <summary>SPEC-91 HAZ-016 (ADR-0119 decision 4), on the authority: a Pulse zone fires on the first tick of each window of its
    /// ZoneSchedule (PulseMath.Fires). Every live ship whose centre of mass is inside the zone and not in shadow is hit. A ship is in
    /// shadow when a physics ray from the source (PulseMath.Source) to its centre of mass hits another grid first: one raycast per
    /// ship per pulse. A ship with its shield up takes A + B as a shield hit (the shield drains first; ShieldSystem passes any
    /// overflow to the hull); a ship without one takes A as energy damage on the face toward the source. Zones in PersistentId order,
    /// ships in Entity.Index order, so the outcome is deterministic.</summary>
    [UpdateInGroup(typeof(GameplayPostPhysicsGroup)), UpdateAfter(typeof(DamageStreamsSystem)), UpdateBefore(typeof(ShieldSystem))]
    public partial struct PulseSystem : ISystem
    {
        /// <summary>What a pulse ray can be blocked by: grids and loose chunks (wrecks, rocks, structures, debris).</summary>
        public static CollisionFilter ShadowQuery => new CollisionFilter { BelongsTo = PhysicsLayers.QueryBit,
            CollidesWith = PhysicsLayers.GridBit | PhysicsLayers.ChunkBodyBit };

        EntityQuery zones, ships;
        public void OnCreate(ref SystemState state)
        {
            zones = new EntityQueryBuilder(Allocator.Temp).WithAll<WorldZone, ZoneEffect, LocalTransform, ZoneSchedule>().WithNone<ZoneOff>().Build(ref state);
            ships = state.GetEntityQuery(ComponentType.ReadOnly<VoxelGrid>(), ComponentType.ReadOnly<LocalTransform>(), ComponentType.ReadOnly<PhysicsMass>(),
                ComponentType.Exclude<Hulk>());
            state.RequireForUpdate(zones); state.RequireForUpdate<SimClock>(); state.RequireForUpdate<DamageStreams>();
            state.RequireForUpdate<ShieldHitStreams>(); state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        struct Pulse { public ulong Id; public WorldZone Zone; public float2 Centre; public ZoneEffect Effect; }
        struct PulseOrder : System.Collections.Generic.IComparer<Pulse> { public int Compare(Pulse a, Pulse b) => a.Id.CompareTo(b.Id); }

        public void OnUpdate(ref SystemState state)
        {
            uint tick = SystemAPI.GetSingleton<SimClock>().Tick;
            var em = state.EntityManager;
            var pulses = new NativeList<Pulse>(Allocator.Temp);
            using (var entities = zones.ToEntityArray(Allocator.Temp))
                foreach (var e in entities)
                {
                    if (!PulseMath.Fires(true, em.GetComponentData<ZoneSchedule>(e), tick)) continue;
                    var zone = em.GetComponentData<WorldZone>(e); var centre = em.GetComponentData<LocalTransform>(e).Position.xz;
                    foreach (var effect in em.GetBuffer<ZoneEffect>(e, true))
                        if (effect.Kind == ZoneEffectKind.Pulse && (effect.Flags & ZoneEffectFlags.AffectsShips) != 0)
                            pulses.Add(new Pulse { Id = zone.PersistentId, Zone = zone, Centre = centre, Effect = effect });
                }
            if (pulses.Length == 0) { pulses.Dispose(); return; }
            pulses.Sort(new PulseOrder());
            var physics = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            var streams = SystemAPI.GetSingleton<DamageStreams>(); var shields = SystemAPI.GetSingleton<ShieldHitStreams>();
            state.CompleteDependency();
            var damage = streams.Damage.BeginWrite(ref state, ProducerKind.Hazard, 1);
            var shieldHits = shields.Hits.BeginWrite(ref state, ProducerKind.Hazard, 1);
            damage.BeginForEachIndex(0); shieldHits.BeginForEachIndex(0);
            var shipEntities = ships.ToEntityArray(Allocator.Temp);
            shipEntities.Sort(new EntityOrder());
            var hits = new NativeList<RaycastHit>(16, Allocator.Temp);
            for (int z = 0; z < pulses.Length; z++)
            {
                var pulse = pulses[z];
                for (int s = 0; s < shipEntities.Length; s++)
                {
                    var ship = shipEntities[s];
                    if (em.GetComponentData<VoxelGrid>(ship).Kind != GridKind.Ship) continue;
                    if (em.HasComponent<GridSpawning>(ship) && em.IsComponentEnabled<GridSpawning>(ship)) continue;
                    var pose = em.GetComponentData<LocalTransform>(ship);
                    float3 com = pose.TransformPoint(em.GetComponentData<PhysicsMass>(ship).CenterOfMass);
                    if (!PulseMath.Inside(pulse.Zone.Shape, pulse.Zone.HalfExtents, pulse.Centre, pulse.Zone.Yaw, com.xz)) continue;
                    float3 source = PulseMath.Source(pulse.Effect.C, pulse.Zone.HalfExtents, pulse.Centre, pulse.Zone.Yaw, com);
                    if (!Lit(physics.CollisionWorld, hits, ship, source, com, out float3 face)) continue;
                    uint sequence = ((uint)z << 16) | (uint)(s & 0xFFFF);
                    float3 dir = math.normalizesafe(com - source, new float3(0, 0, 1));
                    if (em.HasComponent<ShieldState>(ship) && em.GetComponentData<ShieldState>(ship).Active == 1)
                    {
                        var shield = em.GetComponentData<ShieldState>(ship);
                        float3 u = pose.InverseTransformDirection(-dir), radii = math.max(shield.Radii, 1e-3f);
                        float3 onShell = shield.Center + u / math.max(1e-6f, math.length(u / radii));
                        shieldHits.Write(new ShieldHit { Grid = ship, LocalPoint = onShell, LocalDir = -u, Amount = pulse.Effect.A + pulse.Effect.B,
                            Type = DamageType.Energy, Penetration = 1, Producer = ProducerKind.Hazard, Sequence = sequence });
                        continue;
                    }
                    if (!(pulse.Effect.A > 0)) continue;
                    var grid = em.GetComponentData<VoxelGrid>(ship);
                    damage.Write(new DamageEvent
                    {
                        Grid = ship, LocalPoint = pose.InverseTransformPoint(face), LocalDir = pose.InverseTransformDirection(dir), Amount = pulse.Effect.A,
                        Radius = 2 * math.max(.5f, grid.CellSize), Penetration = 1, Type = DamageType.Energy, Producer = ProducerKind.Hazard,
                        Sequence = sequence, Flags = DamageEventFlags.NoImpulse
                    });
                }
            }
            damage.EndForEachIndex(); shieldHits.EndForEachIndex();
            streams.Damage.EndWrite(default); shields.Hits.EndWrite(default);
            hits.Dispose(); shipEntities.Dispose(); pulses.Dispose();
        }

        struct EntityOrder : System.Collections.Generic.IComparer<Entity> { public int Compare(Entity a, Entity b) => a.Index.CompareTo(b.Index); }

        /// <summary>Whether the ray from source to the ship's centre of mass reaches the ship before any other grid; face is where it lands.</summary>
        public static bool Lit(in CollisionWorld world, NativeList<RaycastHit> hits, Entity ship, float3 source, float3 com, out float3 face)
        {
            hits.Clear(); face = com;
            world.CastRay(new RaycastInput { Start = source, End = com, Filter = ShadowQuery }, ref hits);
            float own = 2, other = 2;
            foreach (var h in hits)
            {
                if (h.Entity == ship) { if (h.Fraction < own) { own = h.Fraction; face = h.Position; } }
                else other = math.min(other, h.Fraction);
            }
            return other >= own;
        }
    }
}
