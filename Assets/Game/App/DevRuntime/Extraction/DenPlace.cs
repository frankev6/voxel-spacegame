#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Components;
using Game.Galaxy.Components;
using Game.Data.Combat;
using Game.Data.Definitions;
using Game.Simulation;
using Game.Simulation.Ai;
using Game.Simulation.Extraction;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Game.Voxel.Rendering;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace Game.App.DevRuntime.Extraction
{
    /// <summary>SPEC-93 EXT-008/010: the Rock, the crew's den (owner, 2026-11-10: "starting out with a natural looking asteroid and building
    /// around it, the player should be able to dig themselves and place buildings/blocks in their den like how they edit the ship ...
    /// buidling defenses as well, because some scenarios where the den gets attacked and you must defend it"). Day one: a natural rock
    /// with a cave facing its shard, battery lamps, a socket at the back of the cave and no power. Dig from the ship (the rock's 4 m
    /// stone cells come out as ore); build from the ship (plate into the rock, guns, lamps; move the socket); a core in the socket powers
    /// the den's guns. Then they come for it: an attack wave, the Alliance following the core home.</summary>
    internal sealed class DenPlace : IDisposable
    {
        internal const ushort DenId = 1;
        internal const float DigRadius = 7, Reach = 90, GunCost = 40, PlateCost = 1, LampCost = 2;
        internal static readonly float3 CaveMouth = new float3(118, 0, 6), CaveCentre = new float3(92, 0, 4), ShardCentre = new float3(240, 10, 30);
        internal static readonly float3 SocketStart = new float3(86, -2, 4), Table = new float3(100, -18, 20);   // clear of a 40 m core in the socket
        internal static readonly string[] Kinds = { "plate", "gun", "lamp", "socket" };
        readonly ExtractionKit kit;
        internal readonly double3 Anchor;
        readonly List<Entity> grids = new(), entities = new(), guns = new();
        readonly List<PersistentId> wave = new();
        readonly List<Light> lamps = new();
        GameObject dressing;
        internal Entity Rock, Shard, Socket;
        internal bool Built, Attacking;
        internal int Ore, Kind;
        Entity measuring; int before = -1;
        double attackAt = -1;
        internal Action<string> Toast = _ => { };

        internal DenPlace(ExtractionKit kit, double3 anchor) { this.kit = kit; Anchor = anchor; }
        internal float3 Sim(float3 local) => kit.Ready(out _, out _, out var frame) ? ExtractionKit.Sim(Anchor, local, frame) : local;
        internal string KindName => Kinds[Kind];

        internal void Build()
        {
            if (Built || !kit.Ready(out var session, out var catalog, out var frame)) return;
            var em = kit.Ctx.EntityManager; em.CompleteAllTrackedJobs();
            Built = true;
            // the rock and its shard: stone you can dig (not DamageImmune), held where they are
            Rock = kit.Grid(session, catalog, frame, Anchor, ExtractionIds.Rock, float3.zero, quaternion.identity, GridKind.Station, GridMotion.Static);
            Shard = kit.Grid(session, catalog, frame, Anchor, ExtractionIds.Shard, ShardCentre, quaternion.RotateY(.7f), GridKind.Station, GridMotion.Static);
            // scenery to the spatial hash: shots stop on the stone, but a gun on its face does not read the 130 m rock as an ally
            // in its line of fire, and targeting skips it (it is still dug: damage reaches it)
            foreach (var g in new[] { Rock, Shard }) if (g != Entity.Null) { grids.Add(g); em.AddComponent<CameraSeeThrough>(g); em.AddComponent<Game.Simulation.Spatial.SpatialScenery>(g); em.AddComponent<RamImmune>(g); }
            // the socket at the back of the cave: where a core gives the den its power
            Socket = em.CreateEntity(typeof(LocalTransform), typeof(CoreSocket));
            em.SetComponentData(Socket, LocalTransform.FromPosition(Sim(SocketStart)));
            em.SetComponentData(Socket, new CoreSocket { Radius = 30, Den = DenId });
            entities.Add(Socket);
            // battery lamps in the cave, and the Table (the job board)
            dressing = new GameObject("The Rock dressing");
            lamps.Add(ExtractionKit.Lamp(dressing.transform, (Vector3)(CaveCentre + new float3(10, 14, 0)), new Color(1f, .8f, .55f), 70, 2200));
            lamps.Add(ExtractionKit.Lamp(dressing.transform, (Vector3)Table, new Color(.3f, .95f, 1f), 30, 900));
            lamps.Add(ExtractionKit.Lamp(dressing.transform, (Vector3)SocketStart, new Color(.6f, .7f, 1f), 50, 900));
        }

        internal void Tick(double now)
        {
            if (!Built || !kit.Ready(out _, out var catalog, out var frame)) return;
            var em = kit.Ctx.EntityManager;
            if (dressing != null) dressing.transform.position = (Vector3)(float3)(Anchor - frame.OriginInStarSystem);
            // ore: what the last dig took out, counted once the damage has resolved
            if (measuring != Entity.Null && before >= 0 && em.Exists(measuring))
            {
                int after = CoreTetherSystem.SolidCells(em, measuring);
                if (after < before) { Ore += before - after; Toast($"+{before - after} ore (ore {Ore})"); }
                measuring = Entity.Null; before = -1;
            }
            bool powered = CoreSocketSystem.DenHasPower(em, DenId);
            for (int i = 0; i < lamps.Count; i++) if (lamps[i] != null) lamps[i].intensity = powered ? lamps[i].range * lamps[i].range * 1.4f : lamps[i].range * lamps[i].range * .45f;
            if (attackAt > 0 && now > attackAt) { attackAt = -1; Attack(catalog, frame); }
            if (Attacking)
            {
                bool any = false;
                foreach (var id in wave) if (kit.Root(id, out var root) && !em.HasComponent<Hulk>(root)) { any = true; Hunt(em, root); }
                if (!any)
                {
                    Attacking = false; wave.Clear();
                    kit.Relation(catalog, LanternPlace.Alliance, 0);
                    Toast("The attack is broken. The Rock holds.");
                }
            }
        }

        void Hunt(EntityManager em, Entity root)
        {
            if (!em.HasComponent<AiBrain>(root)) return;
            var brain = em.GetComponentData<AiBrain>(root);
            if (brain.Initialized == 0) return;
            brain.Anchor = Sim(CaveMouth + new float3(160, 0, 0)); em.SetComponentData(root, brain);   // off the mouth, not jammed into the rock
        }

        /// <summary>Where the ship's nose points at the den's stone, within Reach: the hit (sim frame), its normal and the grid.</summary>
        bool Aim(EntityManager em, out float3 hit, out float3 normal, out Entity grid)
        {
            hit = normal = default; grid = Entity.Null;
            var ship = kit.Ctx.PlayerShipRoot;
            if (ship == Entity.Null || !em.Exists(ship)) return false;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<PhysicsWorldSingleton>());
            if (q.CalculateEntityCount() != 1) return false;
            em.CompleteAllTrackedJobs();
            var world = q.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
            var pose = em.GetComponentData<LocalTransform>(ship);
            float3 forward = math.mul(pose.Rotation, new float3(0, 0, 1));
            var hits = new NativeList<Unity.Physics.RaycastHit>(8, Allocator.Temp);
            var collector = new AllHitsCollector<Unity.Physics.RaycastHit>(1f, ref hits);
            world.CastRay(new RaycastInput { Start = pose.Position, End = pose.Position + forward * Reach, Filter = CollisionFilter.Default }, ref collector);
            float best = 2;
            foreach (var h in hits)
            {
                if (h.Entity == ship || h.Fraction >= best) continue;
                best = h.Fraction; hit = h.Position; normal = h.SurfaceNormal; grid = h.Entity;
            }
            hits.Dispose();
            return grid != Entity.Null;
        }

        internal string Dig()
        {
            var em = kit.Ctx.EntityManager;
            if (!Built) return "the den is not here";
            if (!Aim(em, out var hit, out _, out var grid) || (grid != Rock && grid != Shard)) return "point the nose at the rock (within 90 m)";
            var pose = em.GetComponentData<LocalTransform>(grid);
            float3 local = math.mul(math.inverse(pose.Rotation), hit - pose.Position);
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<DamageStreams>());
            if (q.CalculateEntityCount() != 1) return "no damage streams";
            measuring = grid; before = CoreTetherSystem.SolidCells(em, grid);
            q.GetSingleton<DamageStreams>().Damage.PostFromMainThread(new DamageEvent { Grid = grid, LocalPoint = local, Amount = 4e5f, Radius = DigRadius,
                Type = DamageType.Explosive, Producer = ProducerKind.MainThread, Source = kit.Ctx.PlayerShipRoot, Instigator = kit.Ctx.PlayerShipRoot });
            return "digging";
        }

        internal void Cycle() { Kind = (Kind + 1) % Kinds.Length; Toast("Build: " + KindName); }

        internal string Place(string kind = null)
        {
            var em = kit.Ctx.EntityManager;
            if (!Built || !kit.Ready(out var session, out var catalog, out var frame)) return "the den is not here";
            kind ??= KindName;
            if (!Aim(em, out var hit, out var normal, out var grid)) return "point the nose at something within 90 m";
            switch (kind)
            {
                case "plate":
                {
                    if (grid != Rock && grid != Shard) return "plate goes onto the den's stone";
                    var pose = em.GetComponentData<LocalTransform>(grid);
                    float cell = em.GetComponentData<VoxelGrid>(grid).CellSize;
                    float3 local = math.mul(math.inverse(pose.Rotation), hit - pose.Position), n = math.mul(math.inverse(pose.Rotation), normal);
                    int3 c = (int3)math.floor(local / cell + n * .5f);
                    int axis = math.abs(n.x) > math.abs(n.y) ? (math.abs(n.x) > math.abs(n.z) ? 0 : 2) : (math.abs(n.y) > math.abs(n.z) ? 1 : 2);
                    int placed = 0;
                    ushort block = catalog.GetBlockIndex(catalog.Get<BlockDefinition>(new ContentId("block_hull")));
                    using var q = em.CreateEntityQuery(ComponentType.ReadOnly<DamageStreams>());
                    var streams = q.GetSingleton<DamageStreams>();
                    for (int a = -1; a <= 1; a++) for (int b = -1; b <= 1; b++)
                    {
                        if (Ore < PlateCost * (placed + 1)) break;
                        int3 d = axis == 0 ? new int3(0, a, b) : axis == 1 ? new int3(a, 0, b) : new int3(a, b, 0);
                        streams.Restores.PostFromMainThread(new CellRestoreRequest { Grid = grid, Mode = CellRestoreMode.Place, CellMin = c + d, Block = block,
                            Color = 64, Producer = ProducerKind.MainThread, Sequence = (uint)placed });
                        placed++;
                    }
                    Ore -= (int)(placed * PlateCost);
                    return placed > 0 ? $"plated {placed} cells (ore {Ore})" : "no ore: dig first";
                }
                case "gun":
                {
                    if (Ore < GunCost) return $"a gun takes {GunCost} ore (you have {Ore}): dig";
                    float3 at = hit + normal * 6;
                    float yaw = math.atan2(normal.x, normal.z);
                    var root = kit.Grid(session, catalog, frame, Anchor, ExtractionIds.Gun, ExtractionKit.Local(Anchor, at, frame), quaternion.RotateY(yaw),
                        GridKind.Station, GridMotion.Static, GridProfile.Station);
                    if (root == Entity.Null) return "gun content missing: run Game/Content/Build Hollow";
                    em.AddComponentData(root, Allegiance.Player);
                    em.AddComponentData(root, new AiBrain { ProfileIndex = kit.Profile(catalog, "ai_turret") });
                    em.AddComponentData(root, new DenPowered { Den = DenId, On = (byte)(CoreSocketSystem.DenHasPower(em, DenId) ? 1 : 0) });
                    em.AddComponentData(root, new Game.Simulation.Weapons.WeaponControl());
                    grids.Add(root); guns.Add(root);
                    Ore -= (int)GunCost;
                    return CoreSocketSystem.DenHasPower(em, DenId) ? $"gun placed, powered (ore {Ore})" : $"gun placed, dark until a core is socketed (ore {Ore})";
                }
                case "lamp":
                {
                    if (Ore < LampCost) return "a lamp takes 2 ore";
                    Ore -= (int)LampCost;
                    lamps.Add(ExtractionKit.Lamp(dressing.transform, (Vector3)ExtractionKit.Local(Anchor, hit + normal * 3, frame), new Color(1f, .85f, .6f), 50, 1500));
                    return $"lamp placed (ore {Ore})";
                }
                case "socket":
                {
                    var s = em.GetComponentData<CoreSocket>(Socket);
                    if (s.Core != Entity.Null) return "the socket holds a core: it stays";
                    em.SetComponentData(Socket, LocalTransform.FromPosition(hit + normal * 24));
                    lamps[2].transform.localPosition = (Vector3)ExtractionKit.Local(Anchor, hit + normal * 24, frame);
                    return "socket moved: dig a chamber round it big enough for a 40 m core";
                }
            }
            return "build plate|gun|lamp|socket";
        }

        internal void ScheduleAttack(double now, double inSeconds) { attackAt = now + inSeconds; Toast($"They followed the core home: an Alliance wave in {inSeconds:0} s. Man the guns."); }

        internal void Attack(Game.Data.Services.ContentCatalog catalog, in FrameOrigin frame)
        {
            if (Attacking) return;
            Attacking = true;
            kit.Relation(catalog, LanternPlace.Alliance, -100);
            var alliance = new Allegiance { Faction = kit.Faction(catalog, LanternPlace.Alliance), Team = 6 };
            for (int i = 0; i < 4; i++)
            {
                var at = CaveMouth + new float3(900 + i * 60, 0, -300 + i * 140);
                var id = kit.Ship(catalog, frame, Anchor, i == 3 ? "ship_nocturne_vesper" : "ship_nocturne_wraith", at, -math.PI / 2, alliance, "ai_default");
                if (!id.IsNone) wave.Add(id);
            }
            Toast("DEN ATTACK: four Alliance ships off the canyon. Powered guns engage.");
        }

        internal string Status(EntityManager em)
        {
            if (!Built) return "the Rock: not built";
            var s = Socket != Entity.Null && em.Exists(Socket) ? em.GetComponentData<CoreSocket>(Socket) : default;
            int rock = Rock != Entity.Null && em.Exists(Rock) ? CoreTetherSystem.SolidCells(em, Rock) : 0;
            int powered = 0; foreach (var g in guns) if (em.Exists(g) && em.HasComponent<DenPowered>(g) && em.GetComponentData<DenPowered>(g).On != 0) powered++;
            return $"den ore={Ore} rockCells={rock} socket={(s.Core != Entity.Null ? "core" : "empty")} powered={s.Powered} guns={guns.Count} poweredGuns={powered} build={KindName} attack={Attacking} wave={wave.Count}";
        }

        internal void Clear()
        {
            if (!Built) return;
            var em = kit.Ctx.EntityManager; em.CompleteAllTrackedJobs();
            foreach (var g in grids) if (em.Exists(g)) Game.Voxel.Grid.GridSpawnApi.RequestDespawn(em, g);
            foreach (var e in entities) if (em.Exists(e)) em.DestroyEntity(e);
            foreach (var id in wave) kit.Despawn(id);
            grids.Clear(); entities.Clear(); guns.Clear(); wave.Clear(); lamps.Clear();
            if (dressing != null) UnityEngine.Object.Destroy(dressing);
            dressing = null; Built = Attacking = false; Rock = Shard = Socket = Entity.Null; attackAt = -1;
        }

        public void Dispose() => Clear();
    }
}
#endif
