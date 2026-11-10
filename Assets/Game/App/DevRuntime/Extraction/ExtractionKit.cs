#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using Game.App.Session;
using Game.App.Spawning;
using Game.Core;
using Game.Core.Components;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Galaxy.Components;
using Game.Galaxy.Services;
using Game.Simulation;
using Game.Simulation.Ai;
using Game.Simulation.Mechanisms;
using Game.Voxel.Damage;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;

namespace Game.App.DevRuntime.Extraction
{
    /// <summary>SPEC-93: what the extraction places share: spawning grids and AI ships at a place's 3D local positions (the place's
    /// anchor in the star system plus local metres, y up), factions, mechanism requests, impulses and lamps. Dev runtime, main thread,
    /// authority (the playground runs solo or on the host).</summary>
    internal sealed class ExtractionKit
    {
        internal readonly DevContext Ctx;
        internal readonly List<string> Missing = new();
        internal ExtractionKit(DevContext ctx) { Ctx = ctx; }

        internal bool Ready(out GameSession session, out ContentCatalog catalog, out FrameOrigin frame)
        {
            session = null; catalog = null; frame = default;
            if (!Ctx.HasGameplay || !Ctx.TryGetService(out session) || !Ctx.TryGetService<ContentService>(out var content) || !Ctx.TryGetFrame(out frame)) return false;
            catalog = content.Catalog; return true;
        }

        internal static float3 Sim(double3 anchor, float3 local, in FrameOrigin frame) => (float3)(anchor + (double3)local - frame.OriginInStarSystem);
        internal static float3 Local(double3 anchor, float3 sim, in FrameOrigin frame) => (float3)((double3)sim + frame.OriginInStarSystem - anchor);

        internal Entity Grid(GameSession session, ContentCatalog catalog, in FrameOrigin frame, double3 anchor, string id, float3 local, quaternion rotation,
            GridKind kind, GridMotion motion, GridProfile profile = GridProfile.Dummy)
        {
            if (!catalog.TryGet<ShipBlueprintDefinition>(new ContentId(id), out var blueprint)) { if (!Missing.Contains(id)) Missing.Add(id); return Entity.Null; }
            var em = Ctx.EntityManager;
            var root = GridSpawnApi.RequestSpawnBlueprint(em, blueprint.Id.Hash, new GridSpawnParams
            {
                PersistentId = session.Allocate(), Pose = new RigidTransform(rotation, Sim(anchor, local, frame)),
                Kind = kind, Motion = motion, Profile = profile, Flags = GridSpawnFlags.Transient
            });
            em.AddComponentData(root, new UniversePosition { Value = anchor + (double3)local });
            return root;
        }

        internal PersistentId Ship(ContentCatalog catalog, in FrameOrigin frame, double3 anchor, string blueprint, float3 local, float yaw, Allegiance allegiance, string profile)
        {
            if (!Ctx.TryGetService<ShipSpawnService>(out var spawner)) return default;
            if (!catalog.TryGet<ShipBlueprintDefinition>(new ContentId(blueprint), out _)) { if (!Missing.Contains(blueprint)) Missing.Add(blueprint); return default; }
            return spawner.Spawn(new ShipSpawnArgs { Blueprint = new ContentId(blueprint), Position = Sim(anchor, local, frame), Rotation = quaternion.RotateY(yaw),
                Allegiance = allegiance, WithAi = true, AiProfile = new ContentId(profile) });
        }

        internal bool Root(PersistentId id, out Entity root)
        {
            root = Entity.Null;
            return !id.IsNone && Ctx.TryGetService<ShipSpawnService>(out var spawner) && spawner.TryGetRoot(id, out root) && Ctx.EntityManager.Exists(root);
        }

        internal void Despawn(PersistentId id) { if (!id.IsNone && Ctx.TryGetService<ShipSpawnService>(out var spawner)) spawner.Despawn(id); }

        internal byte Faction(ContentCatalog catalog, string id)
        {
            if (catalog.TryGet<FactionDefinition>(new ContentId(id), out var faction) && Ctx.TryGetService<FactionService>(out var factions))
                return factions.Indices.IndexOf(faction);
            return 255;
        }

        internal void Relation(ContentCatalog catalog, string id, int value)
        {
            if (catalog.TryGet<FactionDefinition>(new ContentId(id), out var faction) && Ctx.TryGetService<FactionService>(out var factions))
                factions.SetRelation(faction, value);
        }

        internal ushort Profile(ContentCatalog catalog, string id) => AiSetup.ProfileIndexOf(catalog, new ContentId(id));

        /// <summary>Asks the mechanism owners for an action (doors, zones, portals) through MechanismRequests (SPEC-93 decision 8).</summary>
        internal void Request(MechanismActionKind kind, ushort group, float value)
        {
            var em = Ctx.EntityManager;
            using var q = em.CreateEntityQuery(ComponentType.ReadWrite<MechanismRequests>());
            if (q.CalculateEntityCount() != 1) return;
            em.CompleteDependencyBeforeRW<MechanismRequests>();
            q.GetSingleton<MechanismRequests>().Pending.Add(new MechanismAction { Kind = kind, Group = group, Value = value });
        }

        /// <summary>A Toggle door opens (to its second point) or closes (to its first).</summary>
        internal void Door(ushort group, bool open)
        {
            Request(MechanismActionKind.MoverReverse, group, open ? -1 : 1);
            Request(MechanismActionKind.MoverPlay, group, 0);
        }

        internal void Impulse(Entity body, float3 impulse, float3 at, bool centre)
        {
            var em = Ctx.EntityManager;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<DamageStreams>());
            if (q.CalculateEntityCount() != 1) return;
            q.GetSingleton<DamageStreams>().Impulses.PostFromMainThread(new ImpulseRequest { Body = body, WorldImpulse = impulse, WorldPoint = at,
                Producer = Game.Data.Combat.ProducerKind.MainThread, Flags = centre ? ImpulseRequestFlags.AtCenterOfMass : ImpulseRequestFlags.None });
        }

        internal static float Mass(EntityManager em, Entity e) => em.HasComponent<PhysicsMass>(e) && em.GetComponentData<PhysicsMass>(e).InverseMass > 0 ? 1 / em.GetComponentData<PhysicsMass>(e).InverseMass : 0;

        internal static Light Lamp(Transform parent, Vector3 at, Color colour, float range, float intensity, LightType type = LightType.Point)
        {
            var go = new GameObject("lamp"); go.transform.SetParent(parent, false); go.transform.localPosition = at;
            var l = go.AddComponent<Light>(); l.type = type; l.shadows = LightShadows.None; l.color = colour; l.range = range; l.intensity = intensity;
            return l;
        }

        internal uint Tick
        {
            get
            {
                if (!Ctx.HasGameplay) return 0;
                using var q = Ctx.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<SimClock>());
                return q.CalculateEntityCount() == 1 ? q.GetSingleton<SimClock>().Tick : 0;
            }
        }
    }
}
#endif
