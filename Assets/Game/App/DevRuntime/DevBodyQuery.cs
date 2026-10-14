#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Game.Data.Definitions;
using Game.Galaxy.Bodies;
using Game.Galaxy.Interaction;
using Game.Galaxy.Services;
using Game.Simulation.Commands;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.App.DevRuntime
{
    /// <summary>DEV-014: loaded bodies by key, display name or story tag; nearest player distance, then stable body key.</summary>
    public static class DevBodyQuery
    {
        public static bool TryFind(EntityManager em, GalaxyService galaxy, string query, out Entity body, out float surfaceRadius, out float interactionRange)
        {
            body = Entity.Null; surfaceRadius = interactionRange = 0;
            if (galaxy?.Galaxy == null || string.IsNullOrWhiteSpace(query)) return false;
            em.CompleteAllTrackedJobs();
            using var players = em.CreateEntityQuery(typeof(PlayerShip), typeof(LocalTransform));
            if (players.CalculateEntityCount() != 1) return false;
            float3 player = players.GetSingleton<LocalTransform>().Position;
            using var bodies = em.CreateEntityQuery(typeof(StarSystemBody), typeof(LocalTransform));
            using var entities = bodies.ToEntityArray(Allocator.Temp);
            float nearest = float.PositiveInfinity; ulong bestKey = ulong.MaxValue;
            foreach (var entity in entities)
            {
                var candidate = em.GetComponentData<StarSystemBody>(entity);
                if (!candidate.Id.System.Equals(galaxy.State.CurrentStarSystem) || (candidate.Flags & BodyFlags.Destroyed) != 0) continue;
                string key = galaxy.GetLocationKey(candidate.Id); int slash = key.LastIndexOf('/');
                bool match = Same(query, key) || slash >= 0 && Same(query, key.Substring(slash + 1)) ||
                    Same(query, BodyName(galaxy, candidate.Id, candidate.TemplateIndex)) || Same(query, galaxy.GetBodyTemplate(candidate.TemplateIndex).StoryTag);
                if (!match) continue;
                float distance = math.distancesq(player, em.GetComponentData<LocalTransform>(entity).Position);
                if (distance > nearest || distance == nearest && (candidate.Id.LocalKey > bestKey ||
                    candidate.Id.LocalKey == bestKey && body != Entity.Null && entity.Index >= body.Index)) continue;
                nearest = distance; bestKey = candidate.Id.LocalKey; body = entity;
                surfaceRadius = math.max(0, candidate.RadiusM);
                var template = galaxy.GetBodyTemplate(candidate.TemplateIndex);
                // T1 anchors exist before their T2 grids. Use the owner's definition until the
                // real Interactable exists; the command still waits for its actual in-range query.
                interactionRange = em.HasComponent<Interactable>(entity) ? em.GetComponentData<Interactable>(entity).DistanceM :
                    candidate.Grid != Entity.Null && em.Exists(candidate.Grid) && em.HasComponent<Interactable>(candidate.Grid)
                        ? em.GetComponentData<Interactable>(candidate.Grid).DistanceM :
                        template.Interaction != InteractionKind.None ? template.InteractionDistanceM : 0;
            }
            return body != Entity.Null;
        }
        // Streamed asteroid sector members have stable IDs but are not content-table bodies.
        static string BodyName(GalaxyService galaxy, Game.Galaxy.Data.BodyId id, ushort templateIndex)
            => galaxy.GetContent(id.System).TryFind(id.LocalKey, out _)
                ? galaxy.GetBodyName(id) : galaxy.GetBodyTemplate(templateIndex).DisplayName;
        static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        internal static string TargetName(DevContext ctx)
        {
            if (!ctx.TryGetInteractionTarget(out var target) || target.Target == Entity.Null || !target.Body.IsValid ||
                !ctx.TryGetService<GalaxyService>(out var galaxy) || galaxy.Galaxy == null || !target.Body.System.Equals(galaxy.State.CurrentStarSystem)) return "";
            return BodyName(galaxy, target.Body, target.TemplateIndex);
        }
    }
}
#endif
