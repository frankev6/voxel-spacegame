using System;
using System.Collections.Generic;
using Game.Voxel.Rendering;
using Game.Voxel.Damage;
using UnityEngine;
using Game.Data.Components;
using Game.Core;
using Game.Core.Components;
using Game.Data.Definitions;
using Game.Galaxy.Bodies;
using Game.Voxel.PhysicsBody;
using Game.Voxel.Grid;
using Game.Simulation;
using Game.Simulation.Flight;
using Game.Simulation.Weapons;
using Game.Simulation.Power;
using Game.Simulation.Abilities;
using Game.Simulation.Ai;
using Unity.Collections;
using Unity.Entities;

namespace Game.App.Boot
{
    /// <summary>Creates the configuration singletons at boot, in the table order of SPEC-01 §5.3; every entity it makes (or that
    /// ContentService.CreateSingletons made in step 2) carries BootSingleton. Implements SPEC-01 §4.5, §5.3.</summary>
    internal static class BootSingletons
    {
        internal static IDisposable CreateAll(EntityManager em, Game.Data.Services.ContentCatalog catalog, IReadOnlyList<Mesh> propMeshes)
        {
            var created = new List<Entity>();

            // Row 1: BlockDefsRef, OrientationTablesRef (ContentService.CreateSingletons, step 2; DATA-007).
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<BlockDefsRef>()))
            using (var entities = q.ToEntityArray(Allocator.Temp))
                foreach (var e in entities) created.Add(e);

            Create(em, VoxelConfigFactory.Create(catalog.Get<VoxelConfigDefinition>(new ContentId("config_voxel"))), created);
            var renderConfig = VoxelRenderConfigFactory.Create(catalog.Get<VoxelRenderConfigDefinition>(new ContentId("config_voxel_render")), propMeshes);
            var lifetime = new RenderConfigLifetime(em.World, renderConfig);
            try
            {
                lifetime.ConfigEntity = Create(em, renderConfig, created);
                var physics = catalog.Get<PhysicsVoxelConfigDefinition>(new ContentId("config_physics"));
                Create(em, PhysicsVoxelConfigFactory.Create(physics), created);
                Create(em, PhysicsVoxelConfigFactory.CreatePhysicsStep(physics), created);
                var damage = DamageConfig.FromDefinition(catalog.Get<DamageConfigDefinition>(new ContentId("config_damage")));
                lifetime.DamageTable = DamageContent.BuildTable(catalog);
                damage.Table = lifetime.DamageTable;
                using (var query = em.CreateEntityQuery(ComponentType.ReadOnly<DamageConfig>()))
                {
                    // The damage stream owner creates its fallback in OnCreate, before boot content exists.
                    lifetime.DamageEntity = query.IsEmpty ? Create(em, damage, created) : query.GetSingletonEntity();
                    em.SetComponentData(lifetime.DamageEntity, damage);
                    if (!created.Contains(lifetime.DamageEntity)) created.Add(lifetime.DamageEntity);
                }
                int firstWeaponEntity = created.Count;
                Game.Simulation.Factions.FactionContent.CreateSingletons(em, catalog,
                    catalog.Get<EncounterConfigDefinition>(new ContentId("config_encounters")), created);
                lifetime.FactionEntity = created[created.Count - 1];
                lifetime.Factions = em.GetComponentData<FactionRelations>(lifetime.FactionEntity);
                WeaponContent.CreateSingletons(em, catalog, created);
                using (var weapons = em.CreateEntityQuery(ComponentType.ReadOnly<WeaponDefsRef>()))
                { lifetime.WeaponEntity = weapons.GetSingletonEntity(); lifetime.Weapons = weapons.GetSingleton<WeaponDefsRef>().Value; }
                // Track only this factory's newly created matrix; an existing faction owner keeps its lifetime.
                for (int i = firstWeaponEntity; i < created.Count; i++)
                {
                    if (em.HasComponent<WeaponTuning>(created[i])) lifetime.WeaponTuningEntity = created[i];
                    if (em.HasComponent<FactionRelations>(created[i]))
                    { lifetime.FactionEntity = created[i]; lifetime.Factions = em.GetComponentData<FactionRelations>(created[i]); }
                }
                new PowerContent(catalog).CreateSingletons(em);
                using (var power = em.CreateEntityQuery(ComponentType.ReadOnly<PowerTuning>()))
                    created.Add(power.GetSingletonEntity());
                AbilityContent.CreateSingletons(em, catalog, created);
                using (var abilities = em.CreateEntityQuery(ComponentType.ReadOnly<AbilityDefsRef>()))
                {
                    lifetime.AbilityEntity = abilities.GetSingletonEntity();
                    lifetime.Abilities = abilities.GetSingleton<AbilityDefsRef>().Value;
                }
                Create(em, FlightConfigFactory.Create(catalog.Get<FlightConfigDefinition>(new ContentId("config_flight"))), created);
                int firstAiEntity = created.Count;
                AiContent.CreateSingletons(em, catalog, created);
                for (int i = firstAiEntity; i < created.Count; i++)
                {
                    if (!em.HasComponent<AiConfig>(created[i])) continue;
                    lifetime.AiEntity = created[i];
                    lifetime.AiProfiles = em.GetComponentData<AiConfig>(created[i]).Profiles;
                }
                Create(em, StreamingConfigFactory.Create(catalog.Get<StreamingConfigDefinition>(new ContentId("config_streaming"))), created);
                // Later rows (ENC-001, ECO-007) are added by their tasks (SPEC-01 §5.3).

                Create(em, default(PersistentIdAllocator), created);

                foreach (var e in created)
                    if (!em.HasComponent<BootSingleton>(e)) em.AddComponent<BootSingleton>(e);
                return lifetime;
            }
            catch { lifetime.Dispose(); throw; }
        }

        sealed class RenderConfigLifetime : IDisposable
        {
            readonly World world;
            VoxelRenderConfig config;
            internal Entity ConfigEntity, DamageEntity, WeaponEntity, WeaponTuningEntity, FactionEntity, AbilityEntity;
            internal Entity AiEntity;
            internal BlobAssetReference<AiProfilesBlob> AiProfiles;
            internal FactionRelations Factions;
            internal BlobAssetReference<AbilityDefsBlob> Abilities;
            internal BlobAssetReference<WeaponDefsBlob> Weapons;
            internal BlobAssetReference<DamageTableBlob> DamageTable;
            public RenderConfigLifetime(World world, VoxelRenderConfig config) { this.world = world; this.config = config; }
            public void Dispose()
            {
                if (world.IsCreated)
                {
                    world.EntityManager.CompleteAllTrackedJobs();
                    if (ConfigEntity != Entity.Null && world.EntityManager.Exists(ConfigEntity)) world.EntityManager.DestroyEntity(ConfigEntity);
                    if (DamageEntity != Entity.Null && world.EntityManager.Exists(DamageEntity))
                        world.EntityManager.SetComponentData(DamageEntity, DamageConfig.Default);
                }
                if (world.IsCreated && AiEntity != Entity.Null && world.EntityManager.Exists(AiEntity))
                    world.EntityManager.DestroyEntity(AiEntity);
                AiEntity = Entity.Null;
                if (AiProfiles.IsCreated) AiProfiles.Dispose(); AiProfiles = default;
                if (Abilities.IsCreated) Abilities.Dispose(); Abilities = default;
                if (world.IsCreated && world.EntityManager.Exists(AbilityEntity)) world.EntityManager.DestroyEntity(AbilityEntity);
                AbilityEntity = Entity.Null;
                if (world.IsCreated)
                {
                    var em = world.EntityManager;
                    if (WeaponEntity != Entity.Null && em.Exists(WeaponEntity)) em.DestroyEntity(WeaponEntity);
                    if (WeaponTuningEntity != Entity.Null && em.Exists(WeaponTuningEntity)) em.DestroyEntity(WeaponTuningEntity);
                    if (FactionEntity != Entity.Null && em.Exists(FactionEntity))
                    {
                        if (em.HasComponent<FactionRelations>(FactionEntity)) Factions = em.GetComponentData<FactionRelations>(FactionEntity);
                        em.DestroyEntity(FactionEntity);
                    }
                }
                if (Weapons.IsCreated) Weapons.Dispose(); Weapons = default;
                Factions.Dispose();
                WeaponEntity = WeaponTuningEntity = FactionEntity = Entity.Null;
                if (DamageTable.IsCreated) DamageTable.Dispose();
                DamageTable = default; DamageEntity = Entity.Null;
                VoxelRenderConfigFactory.Destroy(ref config);
                ConfigEntity = Entity.Null;
            }
        }

        /// <summary>Writes a component-value singleton with the editor name "Cfg.&lt;Type&gt;" and records it (for factory rows).</summary>
        internal static Entity Create<T>(EntityManager em, T value, List<Entity> created) where T : unmanaged, IComponentData
        {
            var e = em.CreateSingleton(value);
#if UNITY_EDITOR
            em.SetName(e, "Cfg." + typeof(T).Name);
#endif
            created.Add(e);
            return e;
        }
    }
}
