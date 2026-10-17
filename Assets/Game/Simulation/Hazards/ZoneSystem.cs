using Game.Core.Components;
using Game.Core.Net;
using Game.Core.Systems;
using Game.Data.Combat;
using Game.Data.Hazards;
using Game.Simulation.Power;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Simulation.Hazards
{
    /// <summary>SPEC-91 §5.2 (HAZ-006, HAZ-007): analytic planar zones push, drag, repel and report gate crossings and bounds. Pushes
    /// go through DamageStreams.Impulses (Environment), wall damage through the damage stream (Hazard, authority only), and gate and
    /// bounds results into ZoneContacts. Zones are processed by PersistentId and bodies by Entity.Index, so every machine agrees.
    /// A zone's live pose is its LocalTransform, which OriginRebaseSystem shifts like any root; path points are relative to it.
    /// Each body's last centre of mass is kept here; a jump over JumpMetres (rebase, respawn, portal) skips that tick's crossings.</summary>
    [BurstCompile, UpdateInGroup(typeof(GameplayPostPhysicsGroup))]
    [UpdateAfter(typeof(DamageStreamsSystem)), UpdateBefore(typeof(ImpulseApplySystem))]
    public partial struct ZoneSystem : ISystem
    {
        internal const float JumpMetres = 50f;
        /// <summary>A wall pushes a body by this fraction of its bounding radius: a sphere round a long hull would hold it metres off.</summary>
        internal const float WallRadiusFraction = .4f;
        /// <summary>The most yaw rate one wall hit adds (rad/s): a glancing hit turns the ship along the wall, never into a spin.</summary>
        internal const float MaxWallSpin = .6f;
        Entity contactsEntity;
        NativeHashMap<Entity, float2> lastCom;
        EntityQuery zones, bodies;

        public void OnCreate(ref SystemState state)
        {
            lastCom = new NativeHashMap<Entity, float2>(64, Allocator.Persistent);
            contactsEntity = state.EntityManager.CreateEntity(typeof(ZoneContacts));
            state.EntityManager.SetName(contactsEntity, "ZoneContacts");
            state.EntityManager.SetComponentData(contactsEntity, new ZoneContacts { Current = new NativeList<ZoneContact>(16, Allocator.Persistent) });
            zones = new EntityQueryBuilder(Allocator.Temp).WithAll<WorldZone, ZoneEffect, LocalTransform>().WithNone<ZoneOff>().Build(ref state);
            bodies = new EntityQueryBuilder(Allocator.Temp).WithAll<LocalTransform, PhysicsVelocity, PhysicsMass>().WithNone<Parent>().Build(ref state);
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<DamageStreams>();
            state.RequireForUpdate(zones);
        }
        public void OnDestroy(ref SystemState state)
        {
            state.CompleteDependency();
            if (state.EntityManager.Exists(contactsEntity))
            {
                var contacts = state.EntityManager.GetComponentData<ZoneContacts>(contactsEntity);
                if (contacts.Current.IsCreated) contacts.Current.Dispose();
            }
            if (lastCom.IsCreated) lastCom.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var contacts = SystemAPI.GetComponent<ZoneContacts>(contactsEntity);
            bool authority = !(SystemAPI.TryGetSingleton<SimRole>(out var role) && role.IsGuest);
            var streams = SystemAPI.GetSingleton<DamageStreams>();
            state.CompleteDependency();

            // Zones, sorted by PersistentId, flattened with their effects and path points.
            var zoneEntities = zones.ToEntityArray(Allocator.TempJob);
            var job = new ZoneJob
            {
                Zones = new NativeList<ZoneRow>(zoneEntities.Length, Allocator.TempJob),
                Effects = new NativeList<ZoneEffect>(Allocator.TempJob), Points = new NativeList<float2>(Allocator.TempJob),
                Bodies = new NativeList<BodyRow>(Allocator.TempJob), LastCom = lastCom, Contacts = contacts.Current,
                Authority = authority, Dt = Game.Core.SimTime.FixedTimestep,
            };
            foreach (var e in zoneEntities)
            {
                var zone = SystemAPI.GetComponent<WorldZone>(e); var pose = SystemAPI.GetComponent<LocalTransform>(e);
                var row = new ZoneRow { Zone = zone, Position = pose.Position.xz, EffectStart = job.Effects.Length, PointStart = job.Points.Length };
                foreach (var effect in SystemAPI.GetBuffer<ZoneEffect>(e)) job.Effects.Add(effect);
                if (SystemAPI.HasBuffer<ZonePathPoint>(e)) foreach (var p in SystemAPI.GetBuffer<ZonePathPoint>(e)) job.Points.Add(p.Point);
                row.EffectCount = job.Effects.Length - row.EffectStart; row.PointCount = job.Points.Length - row.PointStart;
                job.Zones.Add(row);
            }
            zoneEntities.Dispose();
            job.Zones.Sort(new ZoneOrder());

            var bodyEntities = bodies.ToEntityArray(Allocator.TempJob);
            foreach (var e in bodyEntities)
            {
                var mass = SystemAPI.GetComponent<PhysicsMass>(e);
                if (mass.InverseMass <= 0) continue;
                var pose = SystemAPI.GetComponent<LocalTransform>(e);
                var row = new BodyRow
                {
                    Entity = e, Com = pose.TransformPoint(mass.CenterOfMass), Velocity = SystemAPI.GetComponent<PhysicsVelocity>(e).Linear,
                    Mass = 1f / mass.InverseMass, Pose = pose, Radius = 1, Flags = ZoneEffectFlags.AffectsDebris
                };
                float3 yaw = math.rotate(math.inverse(math.mul(pose.Rotation, mass.InertiaOrientation)), math.up());
                row.InvYawInertia = math.dot(yaw * yaw, mass.InverseInertia);
                if (SystemAPI.HasComponent<VoxelGrid>(e))
                {
                    var grid = SystemAPI.GetComponent<VoxelGrid>(e);
                    row.Grid = true;
                    row.Flags = grid.Kind == GridKind.Ship ? ZoneEffectFlags.AffectsShips : grid.Kind == GridKind.Device ? ZoneEffectFlags.AffectsDevices
                        : ZoneEffectFlags.AffectsDebris;
                    row.Radius = math.length((float3)(grid.CellMax - grid.CellMin + 1)) * grid.CellSize * .5f;
                    row.CellSize = grid.CellSize;
                }
                if (SystemAPI.HasComponent<ShipStats>(e) && SystemAPI.GetComponent<ShipStats>(e).BoundingRadius > 0)
                    row.Radius = SystemAPI.GetComponent<ShipStats>(e).BoundingRadius;
                job.Bodies.Add(row);
            }
            bodyEntities.Dispose();
            job.Bodies.Sort(new BodyOrder());

            job.Impulses = streams.Impulses.BeginWrite(ref state, ProducerKind.Environment, 1);
            job.Damage = streams.Damage.BeginWrite(ref state, ProducerKind.Hazard, 1);
            var handle = job.Schedule(state.Dependency);
            streams.Impulses.EndWrite(handle); streams.Damage.EndWrite(handle);
            handle = job.Zones.Dispose(handle); handle = job.Effects.Dispose(handle); handle = job.Points.Dispose(handle); handle = job.Bodies.Dispose(handle);
            state.Dependency = handle;
        }

        internal struct ZoneRow { public WorldZone Zone; public float2 Position; public int EffectStart, EffectCount, PointStart, PointCount; }
        internal struct BodyRow
        {
            public Entity Entity; public float3 Com, Velocity; public float Mass, Radius, CellSize, InvYawInertia; public LocalTransform Pose;
            public ZoneEffectFlags Flags; public bool Grid;
        }
        struct ZoneOrder : System.Collections.Generic.IComparer<ZoneRow>
        { public int Compare(ZoneRow a, ZoneRow b) => a.Zone.PersistentId.CompareTo(b.Zone.PersistentId); }
        struct BodyOrder : System.Collections.Generic.IComparer<BodyRow>
        { public int Compare(BodyRow a, BodyRow b) => a.Entity.Index != b.Entity.Index ? a.Entity.Index.CompareTo(b.Entity.Index) : a.Entity.Version.CompareTo(b.Entity.Version); }

        [BurstCompile]
        struct ZoneJob : IJob
        {
            public NativeList<ZoneRow> Zones; public NativeList<ZoneEffect> Effects; public NativeList<float2> Points; public NativeList<BodyRow> Bodies;
            public NativeHashMap<Entity, float2> LastCom; public NativeList<ZoneContact> Contacts;
            public NativeStream.Writer Impulses, Damage;
            public bool Authority; public float Dt;

            public void Execute()
            {
                Contacts.Clear();
                Impulses.BeginForEachIndex(0); Damage.BeginForEachIndex(0);
                for (int z = 0; z < Zones.Length; z++)
                {
                    var row = Zones[z];
                    float yaw = row.Zone.Yaw;
                    float2 forward = new float2(math.sin(yaw), math.cos(yaw)), right = new float2(forward.y, -forward.x);
                    for (int b = 0; b < Bodies.Length; b++)
                    {
                        var body = Bodies[b];
                        float2 com = body.Com.xz, v = body.Velocity.xz;
                        bool hasLast = LastCom.TryGetValue(body.Entity, out float2 last) && math.distancesq(last, com) < JumpMetres * JumpMetres;
                        uint sequence = ((uint)z << 16) | (uint)(b & 0xFFFF);
                        for (int k = 0; k < row.EffectCount; k++)
                        {
                            var effect = Effects[row.EffectStart + k];
                            if ((effect.Flags & body.Flags) == 0) continue;
                            switch (effect.Kind)
                            {
                                case ZoneEffectKind.Accelerate:
                                    if (!Inside(row, com, forward, right, body.Radius)) break;
                                    float s = math.dot(v, forward), gain = math.min(effect.B * Dt, math.max(0, effect.A - s));
                                    if (gain > 0) Push(body, new float2(forward * gain * body.Mass), true, sequence);
                                    break;
                                case ZoneEffectKind.Drag:
                                    if (!Inside(row, com, forward, right, body.Radius) || math.length(v) <= effect.B) break;
                                    Push(body, -v * math.min(1, effect.A * Dt) * body.Mass, true, sequence);
                                    break;
                                case ZoneEffectKind.Repel:
                                    Repel(row, effect, body, com, v, hasLast, last, sequence);
                                    break;
                                case ZoneEffectKind.Gate:
                                    if (!hasLast || row.Zone.Shape != ZoneShape.Box) break;
                                    sbyte crossed = GateCrossing(row.Position, forward, right, row.Zone.HalfExtents.x, last, com);
                                    if (crossed != 0) Contacts.Add(new ZoneContact { Body = body.Entity, Zone = row.Zone.PersistentId, Kind = effect.Kind, Id = effect.Id, Crossed = crossed });
                                    break;
                                case ZoneEffectKind.Bounds:
                                    bool inside = row.Zone.Shape == ZoneShape.Path ? InPolygon(row, com) : Inside(row, com, forward, right, 0);
                                    Contacts.Add(new ZoneContact { Body = body.Entity, Zone = row.Zone.PersistentId, Kind = effect.Kind, Id = effect.Id, Inside = (byte)(inside ? 1 : 0) });
                                    break;
                            }
                        }
                    }
                }
                Impulses.EndForEachIndex(); Damage.EndForEachIndex();
                LastCom.Clear();
                for (int b = 0; b < Bodies.Length; b++) LastCom[Bodies[b].Entity] = Bodies[b].Com.xz;
            }

            void Push(in BodyRow body, float2 impulse, bool atCentre, uint sequence, float2 point = default)
            {
                if (!math.all(math.isfinite(impulse))) return;
                Impulses.Write(new ImpulseRequest
                {
                    Body = body.Entity, WorldImpulse = new float3(impulse.x, 0, impulse.y), WorldPoint = new float3(point.x, body.Com.y, point.y),
                    Flags = atCentre ? ImpulseRequestFlags.AtCenterOfMass : 0, Producer = ProducerKind.Environment, Sequence = sequence
                });
            }

            /// Yaw torque of an impulse j at lever r in the xz plane: (r × j).y.
            static float Cross(float2 r, float2 j) => r.y * j.x - r.x * j.y;

            bool Inside(in ZoneRow row, float2 com, float2 forward, float2 right, float radius)
            {
                float2 d = com - row.Position;
                switch (row.Zone.Shape)
                {
                    case ZoneShape.Box: return math.abs(math.dot(d, right)) <= row.Zone.HalfExtents.x && math.abs(math.dot(d, forward)) <= row.Zone.HalfExtents.y;
                    case ZoneShape.Circle: return math.lengthsq(d) <= row.Zone.HalfExtents.x * row.Zone.HalfExtents.x;
                    default: return Nearest(row, com, out _, out _, out _) <= row.Zone.PathRadius + radius;
                }
            }

            /// Distance from p to the zone's polyline; the nearest point, its segment's direction and which side p is on (+1 left).
            float Nearest(in ZoneRow row, float2 p, out float2 nearest, out float2 direction, out int segment)
            {
                float best = float.MaxValue; nearest = row.Position; direction = new float2(0, 1); segment = -1;
                for (int i = 0; i + 1 < row.PointCount; i++)
                {
                    float2 a = row.Position + Points[row.PointStart + i], c = row.Position + Points[row.PointStart + i + 1], ab = c - a;
                    float t = math.saturate(math.dot(p - a, ab) / math.max(1e-6f, math.dot(ab, ab)));
                    float2 q = a + t * ab; float d = math.distance(p, q);
                    if (d < best) { best = d; nearest = q; direction = math.normalizesafe(ab, new float2(0, 1)); segment = i; }
                }
                return best;
            }

            bool InPolygon(in ZoneRow row, float2 p)
            {
                bool inside = false;
                for (int i = 0, j = row.PointCount - 1; i < row.PointCount; j = i++)
                {
                    float2 a = row.Position + Points[row.PointStart + i], c = row.Position + Points[row.PointStart + j];
                    if ((a.y > p.y) != (c.y > p.y) && p.x < (c.x - a.x) * (p.y - a.y) / (c.y - a.y) + a.x) inside = !inside;
                }
                return inside;
            }

            /// +1 crossing the gate line along forward, −1 against it, 0 none; the crossing point must lie within the half width.
            internal static sbyte GateCrossing(float2 position, float2 forward, float2 right, float halfWidth, float2 from, float2 to)
            {
                float s0 = math.dot(from - position, forward), s1 = math.dot(to - position, forward);
                bool plus = s0 < 0 && s1 >= 0, minus = s0 >= 0 && s1 < 0;
                if (!plus && !minus) return 0;
                float t = s0 / (s0 - s1);
                float lateral = math.dot(math.lerp(from, to, t) - position, right);
                return math.abs(lateral) <= halfWidth ? (sbyte)(plus ? 1 : -1) : (sbyte)0;
            }

            void Repel(in ZoneRow row, in ZoneEffect effect, in BodyRow body, float2 com, float2 v, bool hasLast, float2 last, uint sequence)
            {
                if (row.Zone.Shape != ZoneShape.Path || row.PointCount < 2) return;
                float reach = row.Zone.PathRadius + body.Radius * WallRadiusFraction;
                float d = Nearest(row, com, out float2 q, out float2 dir, out _);
                if (d >= reach) return;
                // The side the body is on, from where it was last tick: a fast body that crossed the centreline is pushed back.
                float2 left = new float2(-dir.y, dir.x);
                float side = math.dot(com - q, left);
                if (hasLast) { Nearest(row, last, out float2 lq, out _, out _); float lastSide = math.dot(last - lq, left); if (math.abs(lastSide) > 1e-3f) side = lastSide; }
                float2 n = side >= 0 ? left : -left;
                float signed = math.dot(com - q, n);   // < 0 once past the centreline
                float p = reach - signed;
                if (p <= 0) return;
                float u = math.max(0, -math.dot(v, n));
                float dv = effect.A * p * Dt + effect.B * u * Dt + u;
                // A hull meets a wall at its nose or tail first, so a glancing hit turns it. Only the impact (u) turns it, and by at most
                // MaxWallSpin: pushed at the hull end in full, a long hull spun at tens of rad/s, and the scrape spring kept twisting it.
                float2 heading = math.normalizesafe(math.rotate(body.Pose.Rotation, new float3(0, 0, 1)).xz, new float2(0, 1));
                float2 contact = com + heading * (math.dot(heading, -n) >= 0 ? 1 : -1) * body.Radius * .7f - n * body.Radius * WallRadiusFraction;
                float2 impulse = n * (dv * body.Mass), lever = contact - com;
                float full = Cross(lever, impulse), turn = Cross(lever, n * (u * body.Mass));
                float most = body.InvYawInertia > 1e-12f ? MaxWallSpin / body.InvYawInertia : 0;
                turn = math.clamp(turn, -most, most);
                Push(body, impulse, false, sequence, com + lever * (math.abs(full) > 1e-6f ? turn / full : 0));
                if (Authority && body.Grid && u >= 3 && effect.C > 0)
                {
                    float3 world = new float3(contact.x, body.Com.y, contact.y);
                    Damage.Write(new DamageEvent
                    {
                        Grid = body.Entity, LocalPoint = body.Pose.InverseTransformPoint(world), LocalDir = body.Pose.InverseTransformDirection(new float3(-n.x, 0, -n.y)),
                        Amount = effect.C * u, Radius = 1.5f * math.max(.5f, body.CellSize), Penetration = 1, Type = DamageType.Ram,
                        Producer = ProducerKind.Hazard, Sequence = sequence, Flags = DamageEventFlags.NoImpulse
                    });
                }
            }
        }
    }
}
