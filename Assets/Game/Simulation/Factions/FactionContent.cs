using System;
using System.Collections.Generic;
using Game.Data.Definitions;
using Game.Data.Services;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Factions
{
    /// <summary>Boot-owned faction state and matrix, before WeaponContent (SPEC-33 §5.1).</summary>
    public static class FactionContent
    {
        public static void CreateSingletons(EntityManager em, ContentCatalog catalog, EncounterConfigDefinition config, List<Entity> created)
        {
            if (config == null || created == null) throw new ArgumentNullException(config == null ? nameof(config) : nameof(created));
            using var existing = em.CreateEntityQuery(ComponentType.ReadOnly<FactionRelations>());
            if (!existing.IsEmptyIgnoreFilter) throw new InvalidOperationException("Faction content must precede weapon content.");
            var map = FactionIndexMap.Build(catalog);
            var relations = new FactionRelations { Count = map.Count, Stance = new NativeArray<byte>(map.Count * map.Count, Allocator.Persistent) };
            Entity entity = Entity.Null;
            try
            {
                entity = em.CreateEntity(typeof(FactionRelations), typeof(FactionState));
                em.SetName(entity, "Cfg.FactionRelations");
                em.SetComponentData(entity, relations);
                em.SetComponentData(entity, new FactionState { Count = map.Count, HostileAtOrBelow = config.HostileAtOrBelow,
                    FriendlyAtOrAbove = config.FriendlyAtOrAbove, ProvokePenalty = config.ProvokePenalty, ProvokeIntervalTicks = config.ProvokeIntervalTicks });
                var standings = em.AddBuffer<FactionStanding>(entity);
                for (byte i = 0; i < map.Count; i++)
                {
                    var definition = map.ByIndex(i);
                    int relation = i == 0 ? 100 : math.clamp(definition.BasePlayerRelation, -100, 100);
                    var stance = relation <= config.HostileAtOrBelow ? FactionStance.Hostile : relation >= config.FriendlyAtOrAbove ? FactionStance.Friendly : FactionStance.Neutral;
                    standings.Add(new FactionStanding { Faction = definition != null ? definition.Id.Hash : default,
                        Relation = relation, Locked = (byte)(i == 0 || definition.RelationLocked ? 1 : 0), PlayerStance = stance });
                    relations.Stance[i * map.Count + i] = (byte)FactionStance.Friendly;
                    if (i == 0) continue;
                    foreach (var entry in definition.Stances)
                    {
                        byte other = map.IndexOf(entry.other);
                        if (other == 255 || other == 0 || other == i) continue;
                        relations.Stance[i * map.Count + other] = relations.Stance[other * map.Count + i] = (byte)entry.stance;
                    }
                }
                for (int i = 1; i < map.Count; i++)
                    relations.Stance[i] = relations.Stance[i * map.Count] = (byte)standings[i].PlayerStance;
                em.AddBuffer<FactionRelationRequest>(entity);
                created.Add(entity);
            }
            catch
            {
                if (entity != Entity.Null && em.Exists(entity)) em.DestroyEntity(entity);
                relations.Dispose(); throw;
            }
        }
    }
}
