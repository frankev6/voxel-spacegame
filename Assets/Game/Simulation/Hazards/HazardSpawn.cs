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

        /// <summary>Requests the field's mines as grids; returns how many were requested into <paramref name="outRequests"/>. A point
        /// `blocked` rejects is drawn again from the same seeded sequence (a track keeps mines off its walls and posts).</summary>
        public static int MineField(EntityManager em, MineFieldDefinition def, float2 position, float yaw, ulong seed, ushort group,
                                    IPersistentIdAllocator ids, Span<Entity> outRequests, Func<float2, bool> blocked = null)
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
                    if (!clear || blocked != null && blocked(p)) continue;
                    placed.Add(p); break;
                }
            uint tick = 0;
            using (var clock = em.CreateEntityQuery(ComponentType.ReadOnly<Game.Core.Components.SimClock>()))
                if (clock.TryGetSingleton<Game.Core.Components.SimClock>(out var c)) tick = c.Tick;
            for (int i = 0; i < placed.Count; i++)
                outRequests[i] = Mine(em, def.Mine, placed[i], i * 1.3f, group, ids != null ? ids.Allocate() : 0, tick);
            return placed.Count;
        }

        /// <summary>Requests one mine grid at p (arming from `tick`); MineField's and the dev `race mine`'s path.</summary>
        public static Entity Mine(EntityManager em, MineDefinition mine, float2 p, float yaw, ushort group, ulong persistentId, uint tick)
        {
            var root = Game.Voxel.Grid.GridSpawnApi.RequestSpawnBlueprint(em, mine.Blueprint.Id.Hash, new Game.Voxel.Grid.GridSpawnParams
            {
                PersistentId = persistentId, Pose = new RigidTransform(quaternion.RotateY(yaw), new float3(p.x, 0, p.y)),
                Kind = Game.Voxel.Model.GridKind.Device, Motion = Game.Voxel.Model.GridMotion.Dynamic, Profile = Game.Voxel.Model.GridProfile.Dummy
            });
            em.AddComponentData(root, new MineState { Phase = MinePhase.Arming, PhaseTick = tick });
            em.AddComponentData(root, new MineFuse { Params = mine.Fuse, Group = group });
            if (mine.Anchored) em.AddComponentData(root, new MineAnchor { Point = p, Stiffness = mine.AnchorStiffness, Damping = mine.AnchorDamping, MaxAccel = mine.AnchorMaxAccel });
            return root;
        }

        /// <summary>HAZ-015/016: puts a spawned zone on a schedule: on for onSeconds of every periodSeconds, starting phaseSeconds into
        /// the period. A Pulse zone fires on the first tick of each on window.</summary>
        public static void Schedule(EntityManager em, Entity zone, float periodSeconds, float onSeconds, float phaseSeconds = 0)
        {
            uint period = (uint)math.max(1, math.round(periodSeconds * 60));
            var s = new ZoneSchedule { PeriodTicks = period, OnTicks = (uint)math.clamp(math.round(onSeconds * 60), 1, period),
                PhaseTicks = (uint)math.round(math.max(0, phaseSeconds) * 60) % period, Enabled = 1 };
            if (em.HasComponent<ZoneSchedule>(zone)) em.SetComponentData(zone, s); else em.AddComponentData(zone, s);
        }

        /// <summary>HAZ-019: requests one energy canister (a kinematic device of one Pickup + Explosive block) at p.</summary>
        public static Entity Canister(EntityManager em, ShipBlueprintDefinition blueprint, float2 p, float yaw, ushort group, ulong persistentId)
        {
            var root = Game.Voxel.Grid.GridSpawnApi.RequestSpawnBlueprint(em, blueprint.Id.Hash, new Game.Voxel.Grid.GridSpawnParams
            {
                PersistentId = persistentId, Pose = new RigidTransform(quaternion.RotateY(yaw), new float3(p.x, 0, p.y)),
                Kind = Game.Voxel.Model.GridKind.Device, Motion = Game.Voxel.Model.GridMotion.Kinematic, Profile = Game.Voxel.Model.GridProfile.Dummy
            });
            em.AddComponentData(root, new Canister { Group = group, Instance = -1 });
            return root;
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
            using (var query = em.CreateEntityQuery(typeof(Canister)))
            using (var canisters = query.ToEntityArray(Unity.Collections.Allocator.Temp))
                foreach (var e in canisters)
                {
                    if (em.GetComponentData<Canister>(e).Group != group) continue;
                    if (em.HasComponent<Game.Voxel.Model.GridDespawnRequest>(e)) Game.Voxel.Grid.GridSpawnApi.RequestDespawn(em, e); else em.DestroyEntity(e);
                }
        }

        /// <summary>Sets one effect's Id on a spawned zone (gates carry their gate id; one gate definition serves every gate).</summary>
        /// <summary>HAZ-013: makes a spawned kinematic grid root a moving structure (ADR-0119 decision 1). Its pose follows the path from
        /// mover.StartTick; give a root spawned with GridMotion.Kinematic, never a dynamic one.</summary>
        public static void AddMover(EntityManager em, Entity root, in Game.Simulation.Mechanisms.Mover mover, ReadOnlySpan<Game.Simulation.Mechanisms.MoverPoint> points = default)
        {
            em.AddComponentData(root, mover);
            var buffer = em.AddBuffer<Game.Simulation.Mechanisms.MoverPoint>(root);
            foreach (var p in points) buffer.Add(p);
        }

        /// <summary>HAZ-014: places a mechanism's rules with its group (ADR-0119 decision 2). Roots whose switches or instances
        /// should fire it carry MechanismGroup; movers and zones it drives carry the same group (Mover.Group, WorldZone.Group).</summary>
        public static Entity Mechanism(EntityManager em, MechanismDefinition def, ushort group)
        {
            if (def == null) throw new ArgumentException("HazardSpawn.Mechanism: no definition");
            var e = em.CreateEntity(typeof(Game.Simulation.Mechanisms.Mechanism));
            em.SetComponentData(e, new Game.Simulation.Mechanisms.Mechanism { Group = group });
            var rules = em.AddBuffer<Game.Simulation.Mechanisms.MechanismRule>(e);
            var actions = em.AddBuffer<Game.Simulation.Mechanisms.MechanismRuleAction>(e);
            foreach (var rule in def.Rules)
            {
                var row = new Game.Simulation.Mechanisms.MechanismRule { Kind = (Game.Simulation.Mechanisms.MechanismTriggerKind)(byte)rule.When.Kind,
                    Group = rule.When.Group, Value = rule.When.Value, Offset = rule.When.Offset, ActionStart = (ushort)actions.Length };
                if (rule.Do != null)
                    foreach (var a in rule.Do)
                        actions.Add(new Game.Simulation.Mechanisms.MechanismRuleAction { Kind = (Game.Simulation.Mechanisms.MechanismActionKind)(byte)a.Kind,
                            Group = a.Group, Value = a.Value, DelayTicks = (uint)math.max(0, math.round(a.DelaySeconds * 60)) });
                row.ActionCount = (ushort)(actions.Length - row.ActionStart);
                rules = em.GetBuffer<Game.Simulation.Mechanisms.MechanismRule>(e); rules.Add(row);
                actions = em.GetBuffer<Game.Simulation.Mechanisms.MechanismRuleAction>(e);
            }
            return e;
        }

        public static void SetEffectId(EntityManager em, Entity zone, ushort id)
        {
            var effects = em.GetBuffer<ZoneEffect>(zone);
            for (int i = 0; i < effects.Length; i++) { var x = effects[i]; x.Id = id; effects[i] = x; }
        }
    }
}
