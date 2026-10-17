using System;
using Game.Core;
using Game.Data.Definitions;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Hazards
{
    /// <summary>The one spawn API for zones and mine fields (SPEC-91 §4.5, ADR-0118 decision 8). Tracks, encounters, star-system
    /// locations and the playground all use it. HAZ-001 contract: implemented by HAZ-006 (zones) and HAZ-008 (mine fields).</summary>
    public static class HazardSpawn
    {
        public static Entity Zone(EntityManager em, ZoneDefinition def, float2 position, float yaw, ushort group, ulong persistentId)
        {
            if (def == null) throw new ArgumentException("HazardSpawn.Zone: no definition");
            if (def.Shape == Game.Data.Hazards.ZoneShape.Path) throw new ArgumentException($"HazardSpawn.Zone: {def.IdString} is a path zone; use PathZone");
            return Create(em, def, position, yaw, group, persistentId, def.HalfExtents, default);
        }

        public static Entity PathZone(EntityManager em, ZoneDefinition def, ReadOnlySpan<float2> points, ushort group, ulong persistentId)
            => PathZone(em, def, points, group, persistentId, def != null ? def.PathRadius : 0);

        /// <summary>A path zone with its own capsule radius (a track's field walls share one definition at several widths).</summary>
        public static Entity PathZone(EntityManager em, ZoneDefinition def, ReadOnlySpan<float2> points, ushort group, ulong persistentId, float radius)
        {
            if (def == null) throw new ArgumentException("HazardSpawn.PathZone: no definition");
            if (points.Length < 2) throw new ArgumentException($"HazardSpawn.PathZone: {def.IdString} needs at least two points");
            float2 origin = points[0];
            var e = Create(em, def, origin, 0, group, persistentId, default, radius);
            var buffer = em.AddBuffer<ZonePathPoint>(e);
            foreach (var p in points) buffer.Add(new ZonePathPoint { Point = p - origin });
            var zone = em.GetComponentData<WorldZone>(e); zone.Shape = Game.Data.Hazards.ZoneShape.Path; em.SetComponentData(e, zone);
            return e;
        }

        // The zone's live pose is its LocalTransform (OriginRebaseSystem shifts it like any root); WorldZone.Position is the spawn pose.
        static Entity Create(EntityManager em, ZoneDefinition def, float2 position, float yaw, ushort group, ulong persistentId, UnityEngine.Vector2 half, float radius)
        {
            var e = em.CreateEntity(typeof(WorldZone), typeof(Unity.Transforms.LocalTransform));
            em.SetName(e, def.IdString);
            em.SetComponentData(e, new WorldZone { Shape = def.Shape, HalfExtents = new float2(half.x, half.y), PathRadius = radius, Yaw = yaw,
                Position = position, PersistentId = persistentId, Group = group });
            em.SetComponentData(e, Unity.Transforms.LocalTransform.FromPositionRotation(new float3(position.x, 0, position.y), quaternion.RotateY(yaw)));
            var effects = em.AddBuffer<ZoneEffect>(e);
            foreach (var x in def.Effects) effects.Add(new ZoneEffect { Kind = x.Kind, A = x.A, B = x.B, C = x.C, Id = x.Id, Flags = x.Flags });
            return e;
        }

        /// <summary>Requests the field's mines as grids; returns how many were requested into <paramref name="outRequests"/>.</summary>
        public static int MineField(EntityManager em, MineFieldDefinition def, float2 position, float yaw, ulong seed, ushort group,
                                    IPersistentIdAllocator ids, Span<Entity> outRequests)
        {
            if (def == null || def.Mine == null || def.Mine.Blueprint == null) throw new ArgumentException("HazardSpawn.MineField: incomplete definition");
            // Deterministic scatter (SPEC-91 decision 9): the same seed places the same mines on every machine and every run.
            var random = new Unity.Mathematics.Random((uint)(seed ^ (seed >> 32) ^ 0x9E3779B9u) | 1u);
            var placed = new System.Collections.Generic.List<float2>();
            float2 forward = new float2(math.sin(yaw), math.cos(yaw)), right = new float2(forward.y, -forward.x);
            for (int i = 0; i < def.Count && placed.Count < outRequests.Length; i++)
                for (int attempt = 0; attempt < 30; attempt++)
                {
                    float2 local = def.Area == Game.Data.Hazards.ZoneShape.Circle
                        ? math.sqrt(random.NextFloat()) * def.HalfExtents.x * new float2(math.cos(random.NextFloat(math.PI * 2)), math.sin(random.NextFloat(math.PI * 2)))
                        : new float2(random.NextFloat(-def.HalfExtents.x, def.HalfExtents.x), random.NextFloat(-def.HalfExtents.y, def.HalfExtents.y));
                    float2 p = position + right * local.x + forward * local.y;
                    bool clear = true; foreach (var q in placed) if (math.distancesq(p, q) < def.MinSpacing * def.MinSpacing) { clear = false; break; }
                    if (!clear) continue;
                    placed.Add(p); break;
                }
            uint tick = 0;
            using (var clock = em.CreateEntityQuery(ComponentType.ReadOnly<Game.Core.Components.SimClock>()))
                if (clock.TryGetSingleton<Game.Core.Components.SimClock>(out var c)) tick = c.Tick;
            var mine = def.Mine;
            for (int i = 0; i < placed.Count; i++)
            {
                var p = placed[i];
                var root = Game.Voxel.Grid.GridSpawnApi.RequestSpawnBlueprint(em, mine.Blueprint.Id.Hash, new Game.Voxel.Grid.GridSpawnParams
                {
                    PersistentId = ids != null ? ids.Allocate() : 0, Pose = new RigidTransform(quaternion.RotateY(i * 1.3f), new float3(p.x, 0, p.y)),
                    Kind = Game.Voxel.Model.GridKind.Device, Motion = Game.Voxel.Model.GridMotion.Dynamic, Profile = Game.Voxel.Model.GridProfile.Dummy
                });
                em.AddComponentData(root, new MineState { Phase = MinePhase.Arming, PhaseTick = tick });
                em.AddComponentData(root, new MineFuse { Params = mine.Fuse, Group = group });
                if (mine.Anchored) em.AddComponentData(root, new MineAnchor { Point = p, Stiffness = mine.AnchorStiffness, Damping = mine.AnchorDamping, MaxAccel = mine.AnchorMaxAccel });
                outRequests[i] = root;
            }
            return placed.Count;
        }

        /// <summary>Destroys the zones and devices of a group.</summary>
        public static void DespawnGroup(EntityManager em, ushort group)
        {
            using (var query = em.CreateEntityQuery(typeof(WorldZone)))
            using (var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp))
                foreach (var e in entities) if (em.GetComponentData<WorldZone>(e).Group == group) em.DestroyEntity(e);
            using (var query = em.CreateEntityQuery(typeof(MineFuse)))
            using (var mines = query.ToEntityArray(Unity.Collections.Allocator.Temp))
                foreach (var e in mines)
                {
                    if (em.GetComponentData<MineFuse>(e).Group != group) continue;
                    if (em.HasComponent<Game.Voxel.Model.GridDespawnRequest>(e)) Game.Voxel.Grid.GridSpawnApi.RequestDespawn(em, e); else em.DestroyEntity(e);
                }
        }

        /// <summary>Sets one effect's Id on a spawned zone (gates carry their gate id; one gate definition serves every gate).</summary>
        public static void SetEffectId(EntityManager em, Entity zone, ushort id)
        {
            var effects = em.GetBuffer<ZoneEffect>(zone);
            for (int i = 0; i < effects.Length; i++) { var x = effects[i]; x.Id = id; effects[i] = x; }
        }
    }
}
