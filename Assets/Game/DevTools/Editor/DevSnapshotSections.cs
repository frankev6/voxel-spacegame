using System.Globalization;
using Game.App.DevRuntime;
using Game.App.Session;
using Game.Core;
using Game.Core.Components;
using Game.Galaxy.Services;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine.SceneManagement;

namespace Game.DevTools.Editor
{
    public static class DevSnapshotSections
    {
        public static string Session()
        {
            Services.TryGet<GameSession>(out var session); Services.TryGet<GalaxyService>(out var galaxy); Services.TryGet<DevRuntimeHost>(out var host);
            if (host?.Context.World != null && host.Context.World.IsCreated) host.Context.EntityManager.CompleteAllTrackedJobs();
            return "seed=" + (session != null ? session.CampaignSeed.ToString(CultureInfo.InvariantCulture) : "unavailable") +
                " scene=" + SceneManager.GetActiveScene().name + " star_system=" + (galaxy != null ? galaxy.State.CurrentStarSystem.ToString() : "unavailable") +
                " tick=" + (host != null ? host.Context.Tick.ToString(CultureInfo.InvariantCulture) : "unavailable");
        }
        public static string Player()
        {
            if (!Services.TryGet<DevRuntimeHost>(out var host) || host.Context.World == null || !host.Context.World.IsCreated) return "unavailable";
            var ctx = host.Context; var em = ctx.EntityManager; em.CompleteAllTrackedJobs(); var root = ctx.PlayerShipRoot;
            if (root == Entity.Null) return "none";
            string id = em.HasComponent<PersistentId>(root) ? em.GetComponentData<PersistentId>(root).Value.ToString(CultureInfo.InvariantCulture) : "unavailable";
            string local = em.HasComponent<LocalTransform>(root) ? DevEcs.Format(em.GetComponentData<LocalTransform>(root).Position) : "unavailable";
            string up = DevEcs.TryGetUniversePosition(ctx, root, out var position) ? DevEcs.Format(position) : "unavailable";
            string speed = em.HasComponent<PhysicsVelocity>(root) ? Unity.Mathematics.math.length(em.GetComponentData<PhysicsVelocity>(root).Linear).ToString("R", CultureInfo.InvariantCulture) : "unavailable";
            return "PersistentId=" + id + " local=" + local + " UniversePosition=" + up + " speed=" + speed + " cells=" + DevEcs.SolidCells(em, root);
        }
    }
}
