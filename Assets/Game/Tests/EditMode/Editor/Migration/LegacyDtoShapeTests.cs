using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Editor.Migration.Dto;
using NUnit.Framework;

namespace Game.Tests.EditMode.Editor.Migration
{
    /// <summary>MIG-001: every legacy DTO declares exactly the JSON names of SPEC-72 §4.3, in file order.</summary>
    public sealed class LegacyDtoShapeTests
    {
        // Own (not inherited) [JsonProperty] names per DTO, from the SPEC-72 §4.3 table.
        static readonly Dictionary<Type, string[]> Table = new Dictionary<Type, string[]>
        {
            { typeof(LegacyItemDto), new[] { "RegistryKey", "Name", "Description", "BasePrice", "isStackable", "ItemImage", "Category", "Rarity", "EffectType", "GrantsAbilities" } },
            { typeof(LegacySlottableItemDto), new[] { "RequiredSocketType", "Modifiers" } },
            { typeof(LegacyPaintBucketItemDto), new[] { "PaletteIndex" } },
            { typeof(LegacyBlockItemDto), new[] { "BlockMesh", "Health", "BlockSize", "VoxelShape", "RotationLocked", "SocketTypeProvided", "SocketsProvidedCount", "EnergyRegenRate", "MaxEnergyStorage", "PassiveDrainRate", "ClearanceDirections", "Modifiers" } },
            { typeof(LegacyCoreBlockItemDto), new[] { "MaxVoxelsSupported", "MaxBuildDimensions" } },
            { typeof(LegacyShieldBlockItemDto), new[] { "ShieldRadius", "ShieldForwardDirection" } },
            { typeof(LegacyPortalBlockItemDto), new[] { "EntryPosition", "EntryDirection", "ExitPosition", "ExitDirection", "EntryRadius" } },
            { typeof(LegacyWeaponBlockItemDto), new[] { "ProjectileData", "BaseAccuracy", "FireRate", "EnergyCost", "BurstCount", "BurstDelay", "disableInputTrigger", "IsRotating", "RotationSpeed", "MaxRotationAngle", "MuzzlePoint", "MuzzleSoundID" } },
            { typeof(LegacyMirrorBlockItemDto), new[] { "BeamDamageMultiplier", "BeamThickness" } },
            { typeof(LegacyShipItemDto), new[] { "ShipVoxelData", "Thumbnail" } },
            { typeof(LegacyShipVoxelDataDto), new[] { "AbilityIds", "OfficerIds", "GlobalStats", "width", "height", "length", "voxels" } },
            { typeof(LegacyVoxelDto), new[] { "blockKey", "moduleSlots", "colorIndex", "x", "y", "z", "rotX", "rotY", "rotZ", "IsDestroyed" } },
            { typeof(LegacyModifierDto), new[] { "StatToModify", "ModifyType", "ModifierRange", "Value" } },
            { typeof(LegacyGlobalStatDto), new[] { "Type", "Value" } },
            { typeof(LegacyProjectileDto), new[] { "VisualType", "DamageType", "Damage", "ExplosionRadius", "Size", "Speed", "LifetimeMin", "LifetimeMax", "IsHoming", "HomingTurnRate", "ExplodesOnDeath" } },
            { typeof(LegacyFactionDto), new[] { "id", "displayName", "description", "logo", "crest", "color", "shipNamePrefix", "doctrine", "basePlayerRelation" } },
            { typeof(LegacyShieldToggleAbilityDto), new[] { "<ID>k__BackingField", "<Icon>k__BackingField", "<Description>k__BackingField", "enableThreshold" } },
            { typeof(LegacyBoostAbilityDto), new[] { "<ID>k__BackingField", "<Icon>k__BackingField", "<Description>k__BackingField", "maxCharges", "cooldownPerUse", "chargeRechargeTime", "boostDuration" } },
            { typeof(LegacyAudioLibraryDto), new[] { "Sounds", "Music" } },
            { typeof(LegacyLootTableDto), new[] { "lootItems" } },
            { typeof(LegacyShipTableDto), new[] { "ships" } },
            { typeof(LegacySystemEntityDto), new[] { "entityId", "ownerFactionIdOverride", "prefab", "mapIcon", "color", "description", "encounterPool", "encounterIdOverride", "autoInteractOnContact", "orbitType", "orbitSpeedMultiplier", "showOrbitLine", "spawnType", "minOrbitRadius", "maxOrbitRadius", "canHaveSatellites", "spawnRequirement", "spawnBlocker" } },
            { typeof(LegacyPlanetEntityDto), new[] { "planetType", "preferredOrbitDistance", "minRadius", "maxRadius", "hasAtmosphere", "atmosphereColor", "hasRings", "planetTextures" } },
            { typeof(LegacyStarEntityDto), new[] { "minTemperature", "maxTemperature", "minScale", "maxScale", "minLuminosity", "maxLuminosity", "minPlanetOrbitRadius", "maxSystemRadius", "galaxyMapPrefab" } },
            { typeof(LegacyHyperlaneEntityDto), new[] { "routeStrength", "duration" } },
            { typeof(LegacyNpcFleetEntityDto), new[] { "allegiance", "detectionRadius", "patrolRadius", "chasesPlayer", "movementSpeed", "useFactionDoctrine", "doctrineMinShips", "doctrineMaxShips" } },
            { typeof(LegacyCombatEncounterDto), new[] { "encounterId", "isUnique", "repeatable", "possibleFormations" } },
            { typeof(LegacySolarSystemDataDto), new[] { "systemId", "openWorldCoordinates", "minEntities", "maxEntities", "baseOrbitSpeed", "orbitSpacingScale", "starEntities", "connectedSystems", "entityPool", "maxAsteroidBelts", "asteroidBeltEntity", "encounterOverride" } },
            { typeof(LegacyGalaxyDataDto), new[] { "galaxyName", "primaryColor", "secondaryColor", "numLayers", "nodesPerLayerMin", "nodesPerLayerMax", "layerRadiusStep", "centerRadius", "nodePositionJitter", "wormholeCount", "minNodeDistance", "maxConnectionDistance", "seed", "shape", "spiralArms", "spiralTwist", "ellipticalRatio", "lootTable", "systemPool", "boss", "startingSolarSystem", "generateFactionTerritories", "autoFactionCount", "factionTerritoryExpansionJitter", "factionTerritoryCoverage", "factionNoOwnershipInnerLayers", "factionPool" } },
            { typeof(LegacyOpenWorldGalaxyConfigDto), new[] { "solarSystems", "defaultHyperlaneEntity", "systemLoadRadius", "systemUnloadRadius", "floatingOriginThreshold", "solarSystemScaleMultiplier", "orbitDistanceMultiplier" } },
        };

        // Newtonsoft's JsonPropertyAttribute read by reflection: the test assembly does not reference Newtonsoft.Json.
        static string JsonName(PropertyInfo p)
        {
            var a = p.GetCustomAttributes().FirstOrDefault(x => x.GetType().FullName == "Newtonsoft.Json.JsonPropertyAttribute");
            return a == null ? null : (string)a.GetType().GetProperty("PropertyName").GetValue(a);
        }

        static string[] OwnJsonNames(Type t) => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .OrderBy(p => p.MetadataToken)
            .Select(p => JsonName(p) ?? "(missing [JsonProperty] on " + p.Name + ")")
            .ToArray();

        [Test]
        public void EveryDto_HasDeclaredJsonNames()
        {
            CollectionAssert.AreEqual(new[] { "$path", "$type", "$name" }, OwnJsonNames(typeof(LegacyDto)));
            var dtoTypes = typeof(LegacyDto).Assembly.GetTypes()
                .Where(t => t.Namespace == "Game.Editor.Migration.Dto" && !t.IsNested && t != typeof(LegacyDto)).ToArray();
            CollectionAssert.AreEquivalent(Table.Keys, dtoTypes, "one DTO per §4.3 row");
            foreach (var kv in Table)
                CollectionAssert.AreEqual(kv.Value, OwnJsonNames(kv.Key), kv.Key.Name);
            foreach (var nested in dtoTypes.SelectMany(t => t.GetNestedTypes()))
                foreach (var p in nested.GetProperties())
                    Assert.IsNotNull(JsonName(p), $"{nested.FullName}.{p.Name} has no [JsonProperty]");
        }
    }
}
