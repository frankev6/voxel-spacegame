# SPEC-72 — Legacy content migration

| | |
|---|---|
| **Status** | Draft |
| **Area code** | MIG (task IDs are `MIG-001`, `MIG-002`, …) |
| **Module(s)** | `Game.Editor` (folder `Assets/Game/Editor/Migration/`), Editor platform only. Output: assets under `Assets/Game/Content/`, plus `Import/Migration/{manifest.json,report.md}` |
| **Depends on specs** | SPEC-04 (definitions, `ContentDatabaseBuilder`, validator, `ValidationReport`, orientation tables, §4.10 block values); SPEC-05 (`VoxsCodec`); SPEC-10 (`ShipBlueprintDefinition`, `Cell`, `PaletteEntry`, `BlockInstance`); SPEC-02 (`StableHash`); target definitions of SPEC-21 (`WeaponDefinition`), SPEC-24 (`AbilityDefinition`), SPEC-64 (`AudioCueDefinition`), SPEC-33 (`FactionDefinition`), SPEC-50 (`LootTableDefinition`), SPEC-30 (`BodyTemplateDefinition`) |
| **Depended on by** | SPEC-40/41/42 (narrative converters plug into the runner and use the JSON utilities and ID map); SPEC-33 (fleet/encounter hand-off); SPEC-30 (star-system reference data); M0 exit gate ("all legacy blocks and ships import with zero validator errors") |
| **Milestone** | M0 (items, blocks, ships); M2 (weapons, abilities, audio); M3 (factions, loot, bodies, hand-off) |

> Read [01-architecture.md](../01-architecture.md), [02-conventions.md](../02-conventions.md) and [03-glossary.md](../03-glossary.md) before this spec. This spec may not contradict them.

## 1. Purpose

The old prototype's content (blocks, ships, items, factions, body templates, loot, audio, abilities, narrative) was exported to neutral JSON in `Import/Legacy/`. This spec defines one-shot Editor converters that turn that JSON into v2 content assets, so the new game starts with every legacy block, ship and item, with stable IDs, valid orientations and correct multi-cell footprints. Converters are idempotent, report every decision through `ValidationReport`, never run in a player build, and never reference old code. They also give the narrative, encounter and galaxy specs a shared, strict JSON reader, a complete legacy-to-v2 ID map and a plug-in point, so those specs convert their own data the same way. Background bugs this prevents: items referenced by a removed numeric ID scheme (05-B2), rotations stored as float Euler angles (06-C13), multi-cell blocks ignored outside the editor (01-B1).

## 2. Scope

**In scope:**
- Reading `Import/Legacy/content/**/*.json` and `Import/Legacy/narrative/*.json` with strict typed DTOs; resolving legacy `{"$ref": path}` references through the export's own `$path` index.
- `LegacyIdMap`: every legacy key (including numeric item IDs `500` and `717185106`) → v2 `ContentId` or "not converted".
- Converters: non-block items → `ItemDefinition`; blocks → `BlockDefinition` (values of SPEC-04 §4.10); ships → `ShipBlueprintDefinition` with VOXS structures; weapons + projectiles → `WeaponDefinition`; abilities → `AbilityDefinition`; audio library → `AudioCueDefinition`; factions → `FactionDefinition`; loot tables → `LootTableDefinition`; solar-system entities → `BodyTemplateDefinition` stubs.
- Legacy Euler → `Orientation` conversion with tolerance; overlap resolution; brick building.
- Idempotent asset writing with hand-edit protection, a manifest, a deterministic report, a menu and a batch entry point.
- The hand-off format for SPEC-30/33/40/41/42 (§4.7).
- The inventory of every legacy file and its target (§4.8).

**Out of scope (and who owns it instead):**
- Narrative conversion (`.yarn`, beats, quests, clues): SPEC-40/41/42, using this spec's utilities (§4.7).
- Encounter definitions and fleets: SPEC-33. Star-system and galaxy generation data: SPEC-30. Both receive typed hand-off data.
- Field design of every non-SPEC-04 target definition: its owning spec. This spec maps *meanings*; the field names come from the owner (decision 7).
- Old UI (`Import/Legacy/ui/`) and input actions (`Import/Legacy/input/`): layout/binding references for SPEC-61 and SPEC-03; not converted.
- Old save files: not supported (SPEC-05 open question 1).

## 3. Decisions (binding)

1. **Converters are Editor-only (`Game.Editor`) and one-shot.** No runtime assembly references them; the game never reads `Import/`. Reason: architecture §3; content is ScriptableObjects at runtime.
2. **`Import/Legacy/` is read-only input.** Converters never write there. Outputs: `Assets/Game/Content/<Area>/<id>.asset` and `Import/Migration/`. Reason: the export is the reference snapshot.
3. **Strict reading.** Every legacy JSON object is read into a DTO that declares every property of that `$type` (§4.3) with Newtonsoft `MissingMemberHandling.Error` (conventions §3.7). An unexpected property is a `MIG-E002` error for that file. Reason: silent field loss is the 05 audit's main pipeline failure.
4. **References resolve through the export.** A `{"$ref": "<old asset path>"}` is resolved by finding the legacy file whose `$path` equals it, then mapping that file's key through `LegacyIdMap`. Old asset paths, GUIDs and `$name` values are never used as v2 identity. Reason: "references are meaningless in v2 except as hints".
5. **One complete ID map.** `LegacyIdMap.Default` (§4.4) lists every legacy key. A key missing from the map is `MIG-E003`. Keys that are not converted map to `null` with a note. Reason: the narrative specs and every converter must agree on IDs.
6. **Idempotent and hand-edit safe.** The same input produces byte-identical assets. An asset changed by hand since the last migration is skipped (unless *Force*), and reported. Link fields (references a later converter adds) are set on hand-edited assets only when they are empty. Reason: the converter may be re-run after fixes without destroying tuning.
7. **Targets are written through `SerializedObject` property paths.** For SPEC-04 types the paths are given here. For other specs' definitions, the converter uses the field names from that spec's §4 that carry the meaning listed in this spec's mapping tables; if no field carries a meaning, the value is dropped with `MIG-I090`. Converters never add fields to another spec's definition. Reason: parallel specs, no access changes needed.
8. **Legacy rotations: Unity Euler order `R = Ry · Rx · Rz`, each angle snapped to a multiple of 90° within 1.0°.** A larger deviation is `MIG-E040` (that voxel is skipped). Reason: legacy data stores `Quaternion.Euler(rotX, rotY, rotZ)`; only cube rotations are representable (SPEC-04 decision 7).
9. **Overlapping legacy blocks: functional blocks win, then list order.** Blocks are placed functional-first, then structural, each group in legacy list order; a block whose footprint hits an occupied cell is dropped with `MIG-W030`. Reason: several legacy ships were authored before `block_thruster_medium` became 3×3×2 and genuinely overlap.
10. **Legacy cell coordinates are kept as v2 grid cell coordinates** (no recentring); grid forward is +Z (SPEC-04 decision 9); cell size 0.5 m.
11. **Errors are per asset.** A `MIG-E` error means that one asset (or voxel) is not written; the run continues and the report lists it. The gate test requires zero errors on the real export.
12. **Converters run in `Order` and the content database is rebuilt after each converter**, so later converters resolve earlier outputs by ID.

## 4. Data model

### 4.1 Folders

| Folder | Namespace | Contents |
|---|---|---|
| `Assets/Game/Editor/Migration/` | `Game.Editor.Migration` | runner, context, index, ID map, asset writer, manifest, report writer, Euler conversion, hand-off |
| `Assets/Game/Editor/Migration/Dto/` | `Game.Editor.Migration.Dto` | strict legacy DTOs (§4.3) |
| `Assets/Game/Editor/Migration/Converters/` | `Game.Editor.Migration.Converters` | this spec's converters |
| `Assets/Game/Editor/Migration/Narrative/`, `…/Encounters/`, `…/Galaxy/` | owned by SPEC-40/41/42, SPEC-33, SPEC-30 | their converters (decision: they plug in through `ILegacyConverter`) |
| `Import/Migration/` | — | `manifest.json`, `report.md` (committed) |

`Dto/` and `Converters/` are new folder names (conventions §1): DTOs mirror legacy types one-to-one and would otherwise crowd the namespace.

### 4.2 Public contract types

```csharp
namespace Game.Editor.Migration
{
    /// <summary>One converter step. Implemented by this spec and by SPEC-30/33/40/41/42 (Order ranges in §4.7).</summary>
    public interface ILegacyConverter
    {
        string Name { get; }        // shown in the report, e.g. "Blocks"
        int Order { get; }          // ascending; ties by Name (ordinal)
        void Convert(LegacyMigrationContext context);
    }

    public sealed class LegacyMigrationContext
    {
        public LegacyContentIndex Index { get; }
        public LegacyIdMap IdMap { get; }
        public LegacyAssetWriter Writer { get; }
        public Game.Data.Validation.ValidationReport Report { get; }
        public Game.Data.Services.ContentCatalog Catalog { get; }      // rebuilt by the runner after every converter
        public LegacyHandOff HandOff { get; }
        public bool Force { get; }                                     // overwrite hand-edited assets
        public string LegacyRoot { get; }                              // absolute path of Import/Legacy
    }

    /// <summary>Every JSON file under Import/Legacy/content, indexed by $path and $type.</summary>
    public sealed class LegacyContentIndex
    {
        public static LegacyContentIndex Load(string legacyRoot, Game.Data.Validation.ValidationReport report);
        public System.Collections.Generic.IReadOnlyList<LegacyFile> Files { get; }        // sorted by RelativePath (ordinal)
        public bool TryGetByAssetPath(string assetPath, out LegacyFile file);
        public System.Collections.Generic.IReadOnlyList<LegacyFile> OfType(string legacyType); // exact $type, sorted by RelativePath
        public bool TryResolve(LegacyRef reference, out LegacyFile file);                // IsNull → false without report
    }

    public sealed class LegacyFile
    {
        public string RelativePath;     // e.g. "content/Items/Blocks/Hulls/block_hull.json"
        public string AssetPath;        // "$path"
        public string Type;             // "$type"
        public string Name;             // "$name"
        public Newtonsoft.Json.Linq.JObject Json;
    }

    /// <summary>A legacy object reference {"$ref","name","type","sub"} or null.</summary>
    public readonly struct LegacyRef
    {
        public readonly string Path; public readonly string Name; public readonly string Type; public readonly string Sub;
        public bool IsNull => Path == null;
    }

    public static class LegacyJson
    {
        public static Newtonsoft.Json.JsonSerializerSettings StrictSettings { get; }      // MissingMemberHandling.Error, FloatParseHandling.Double, custom LegacyRef converter
        public static T Read<T>(LegacyFile file, Game.Data.Validation.ValidationReport report) where T : Game.Editor.Migration.Dto.LegacyDto; // null + MIG-E002 on failure
        public static T ReadFile<T>(string legacyRoot, string relativePath, Game.Data.Validation.ValidationReport report) where T : class; // for files without $path (narrative/*.json)
    }

    public enum LegacyIdKind : byte { Item = 0, Block = 1, Ship = 2, Weapon = 3, Faction = 4, Ability = 5, AudioCue = 6, LootTable = 7, Body = 8, StarSystem = 9, Encounter = 10 }

    public readonly struct LegacyIdMapping
    {
        public readonly LegacyIdKind Kind; public readonly string LegacyKey; public readonly string V2Id;  // V2Id null = not converted
        public readonly string Note;
    }

    public sealed class LegacyIdMap
    {
        public static LegacyIdMap Default { get; }                                         // §4.4, immutable
        public System.Collections.Generic.IReadOnlyList<LegacyIdMapping> All { get; }
        public bool TryMap(LegacyIdKind kind, string legacyKey, out string v2Id);         // false if the key is unknown; true with null for "not converted"
        /// <summary>For narrative DSL tokens: tries Item, Block, Ship in that order; accepts numeric legacy IDs.</summary>
        public bool TryMapItemToken(string token, out string v2Id, out LegacyIdKind kind);
    }

    public enum WriteOutcome : byte { Created = 0, Updated = 1, Unchanged = 2, SkippedHandEdited = 3, Failed = 4 }

    public sealed class LegacyAssetWriter
    {
        public LegacyAssetWriter(string contentRoot, MigrationManifest manifest, bool force, Game.Data.Validation.ValidationReport report);
        public T CreateOrUpdate<T>(string area, string id, System.Action<UnityEditor.SerializedObject> fill, out WriteOutcome outcome)
            where T : Game.Data.Definitions.ContentDefinition;          // area = folder under Assets/Game/Content
        public bool SetLinkIfEmpty(Game.Data.Definitions.ContentDefinition asset, string propertyPath, UnityEngine.Object value);
        public bool SetLinkArrayIfEmpty(Game.Data.Definitions.ContentDefinition asset, string propertyPath, System.Collections.Generic.IReadOnlyList<UnityEngine.Object> values);
        public System.Collections.Generic.IReadOnlyList<(string AssetPath, WriteOutcome Outcome)> Outcomes { get; }
    }

    public sealed class MigrationManifest
    {
        public static MigrationManifest Load(string path);               // missing file → empty
        public bool TryGetHash(string assetPath, out ulong hash);
        public void SetHash(string assetPath, ulong hash);
        public void Save(string path);                                   // JSON object sorted by key, "x16" hex values, LF line endings
    }

    public static class LegacyEuler
    {
        public const float SnapToleranceDegrees = 1.0f;
        public static bool TryToOrientation(float rotX, float rotY, float rotZ, out byte orientation, out float maxSnapErrorDegrees);
    }

    public static class LegacyMigrationRunner
    {
        [UnityEditor.MenuItem("Game/Legacy Migration/Run All")] public static void RunAllMenu();
        [UnityEditor.MenuItem("Game/Legacy Migration/Run All (Force)")] public static void RunAllForceMenu();
        public static Game.Data.Validation.ValidationReport RunAll(bool force);
        public static void RunCli();                                       // -executeMethod; exit code 1 on errors
    }
}
```

### 4.3 Strict DTOs (`Game.Editor.Migration.Dto`)

Every DTO derives from `LegacyDto` (`[JsonProperty("$path")] Path`, `[JsonProperty("$type")] Type`, `[JsonProperty("$name")] Name`). C# property names are PascalCase; the JSON name is given with `[JsonProperty("…")]` exactly as below. Numbers are `double` unless marked *int*; `ref` = `LegacyRef`; `ref[]` = `LegacyRef[]`; `int3[]` = `int[][]`.

| DTO | `$type` values | Properties (JSON names, in file order) |
|---|---|---|
| `LegacyItemDto` | `ResourceItem` | `RegistryKey`, `Name`, `Description`, `BasePrice` int, `isStackable` bool, `ItemImage` ref, `Category` string, `Rarity` string, `EffectType` string, `GrantsAbilities` ref[] |
| `LegacySlottableItemDto : LegacyItemDto` | `SlottableItem` | + `RequiredSocketType` string, `Modifiers` `LegacyModifierDto[]` |
| `LegacyPaintBucketItemDto : LegacyItemDto` | `PaintBucketItem` | + `PaletteIndex` int |
| `LegacyBlockItemDto : LegacyItemDto` | `BlockItem` | + `BlockMesh` ref, `Health` double, `BlockSize` int[3], `VoxelShape` int3[], `RotationLocked` bool, `SocketTypeProvided` string, `SocketsProvidedCount` int, `EnergyRegenRate`, `MaxEnergyStorage`, `PassiveDrainRate`, `ClearanceDirections` int3[], `Modifiers` `LegacyModifierDto[]` |
| `LegacyCoreBlockItemDto : LegacyBlockItemDto` | `CoreBlockItem` | + `MaxVoxelsSupported` int, `MaxBuildDimensions` int |
| `LegacyShieldBlockItemDto : LegacyBlockItemDto` | `ShieldBlockItem` | + `ShieldRadius`, `ShieldForwardDirection` double[3] |
| `LegacyPortalBlockItemDto : LegacyBlockItemDto` | `PortalBlockItem` | + `EntryPosition` double[3], `EntryDirection` double[3], `ExitPosition` double[3], `ExitDirection` double[3], `EntryRadius` |
| `LegacyWeaponBlockItemDto : LegacyBlockItemDto` | `WeaponBlockItem` | + `ProjectileData` ref, `BaseAccuracy`, `FireRate`, `EnergyCost`, `BurstCount` int, `BurstDelay`, `disableInputTrigger` bool, `IsRotating` bool, `RotationSpeed`, `MaxRotationAngle`, `MuzzlePoint` double[3], `MuzzleSoundID` string |
| `LegacyMirrorBlockItemDto : LegacyWeaponBlockItemDto` | `MirrorBlockItem` | + `BeamDamageMultiplier`, `BeamThickness` |
| `LegacyShipItemDto : LegacyItemDto` | `ShipItem` | + `ShipVoxelData` `LegacyShipVoxelDataDto`, `Thumbnail` ref |
| `LegacyShipVoxelDataDto` (no `$`) | — | `AbilityIds` string[], `OfficerIds` string[], `GlobalStats` `LegacyGlobalStatDto[]`, `width` int, `height` int, `length` int, `voxels` `LegacyVoxelDto[]` |
| `LegacyVoxelDto` (no `$`) | — | `blockKey`, `moduleSlots` string[], `colorIndex` int, `x` int, `y` int, `z` int, `rotX`, `rotY`, `rotZ`, `IsDestroyed` bool |
| `LegacyModifierDto` (no `$`) | — | `StatToModify`, `ModifyType`, `ModifierRange` strings, `Value` |
| `LegacyGlobalStatDto` (no `$`) | — | `Type` string, `Value` { `Additive`, `Multiplicative` } |
| `LegacyProjectileDto` | `Projectile` | `VisualType`, `DamageType` strings, `Damage`, `ExplosionRadius`, `Size`, `Speed`, `LifetimeMin`, `LifetimeMax`, `IsHoming` bool, `HomingTurnRate`, `ExplodesOnDeath` bool |
| `LegacyFactionDto` | `FactionData` | `id`, `displayName`, `description`, `logo` ref, `crest` ref, `color` double[4], `shipNamePrefix`, `doctrine` [{`shipKey`, `weight`}], `basePlayerRelation` int |
| `LegacyShieldToggleAbilityDto` | `ShieldToggleAbilitySO` | `<ID>k__BackingField` string, `<Icon>k__BackingField` ref, `<Description>k__BackingField` string, `enableThreshold` |
| `LegacyBoostAbilityDto` | `BoostAbilitySO` | same first three + `maxCharges` int, `cooldownPerUse`, `chargeRechargeTime`, `boostDuration` |
| `LegacyAudioLibraryDto` | `AudioLibrary` | `Sounds` [{`ID`, `Clip` ref, `Volume`}], `Music` [{`Track`, `Clip` ref, `Volume`}] |
| `LegacyLootTableDto` | `LootTable` | `lootItems` [{`item` ref, `minAmount` int, `maxAmount` int, `dropChance` double}] |
| `LegacyShipTableDto` | `ShipTableSO` | `ships` ref[] |
| `LegacySystemEntityDto` | `SolarSystemEntity` | `entityId`, `ownerFactionIdOverride`, `prefab` ref, `mapIcon` ref, `color` double[4], `description`, `encounterPool` {`poolName`, `encounters` [{`encounter` ref, `probabilityWeight`}]}, `encounterIdOverride`, `autoInteractOnContact` bool, `orbitType`, `orbitSpeedMultiplier`, `showOrbitLine` bool, `spawnType`, `minOrbitRadius`, `maxOrbitRadius`, `canHaveSatellites` bool, `spawnRequirement`, `spawnBlocker` |
| `LegacyPlanetEntityDto : LegacySystemEntityDto` | `PlanetEntity` | + `planetType`, `preferredOrbitDistance`, `minRadius`, `maxRadius`, `hasAtmosphere` bool, `atmosphereColor` double[4], `hasRings` bool, `planetTextures` ref[] |
| `LegacyStarEntityDto : LegacySystemEntityDto` | `StarEntity` | + `minTemperature`, `maxTemperature`, `minScale`, `maxScale`, `minLuminosity`, `maxLuminosity`, `minPlanetOrbitRadius`, `maxSystemRadius`, `galaxyMapPrefab` ref |
| `LegacyHyperlaneEntityDto : LegacySystemEntityDto` | `Assets.Game.Core.Galaxy_Map.HyperlaneEntity` | + `routeStrength`, `duration` |
| `LegacyNpcFleetEntityDto : LegacySystemEntityDto` | `NPCFleetEntity` | + `allegiance`, `detectionRadius`, `patrolRadius`, `chasesPlayer` bool, `movementSpeed`, `useFactionDoctrine` bool, `doctrineMinShips` int, `doctrineMaxShips` int |
| `LegacyCombatEncounterDto` | `CombatEncounter` | `encounterId`, `isUnique` bool, `repeatable` bool, `possibleFormations` [{`ships` ref[]}] |
| `LegacySolarSystemDataDto` | `SolarSystemData` | `systemId`, `openWorldCoordinates` double[2], `minEntities` int, `maxEntities` int, `baseOrbitSpeed`, `orbitSpacingScale`, `starEntities` ref[], `connectedSystems` ref[], `entityPool` [{`entity` ref, `weight`, `maxCount` int}], `maxAsteroidBelts` int, `asteroidBeltEntity` ref, `encounterOverride` ref |
| `LegacyGalaxyDataDto` | `GalaxyData` | `galaxyName`, `primaryColor` double[4], `secondaryColor` double[4], `numLayers` int, `nodesPerLayerMin` int, `nodesPerLayerMax` int, `layerRadiusStep`, `centerRadius`, `nodePositionJitter` double[2], `wormholeCount` int, `minNodeDistance`, `maxConnectionDistance`, `seed` int, `shape` string, `spiralArms` int, `spiralTwist`, `ellipticalRatio`, `lootTable` ref, `systemPool` [{`data` ref, `weight`, `minCount` int, `maxCount` int, `minDistance`}], `boss` ref, `startingSolarSystem` ref, `generateFactionTerritories` bool, `autoFactionCount` int, `factionTerritoryExpansionJitter`, `factionTerritoryCoverage`, `factionNoOwnershipInnerLayers` int, `factionPool` ref[] (hand-off only; read from `Galaxy_01/GalaxyData_01.json` in MIG-001) |
| `LegacyOpenWorldGalaxyConfigDto` | `OpenWorldGalaxyConfig` | `solarSystems` ref[], `defaultHyperlaneEntity` ref, `systemLoadRadius`, `systemUnloadRadius`, `floatingOriginThreshold`, `solarSystemScaleMultiplier`, `orbitDistanceMultiplier` (hand-off only; read from `OpenWorldGalaxyConfig.json` in MIG-001) |

C# names (MIG-001): the item's JSON `Name` is the C# property `ItemName`, because `LegacyDto.Name` holds `$name`. Inline objects are nested classes of their DTO (`LegacyFactionDto.DoctrineEntry`, `LegacyGlobalStatDto.StatValue`, `LegacyAudioLibraryDto.SoundEntry`/`MusicEntry`, `LegacyLootTableDto.LootItem`, `LegacySystemEntityDto.EncounterPoolData`/`WeightedEncounter`, `LegacyCombatEncounterDto.Formation`, `LegacySolarSystemDataDto.PoolEntry`, `LegacyGalaxyDataDto.SystemPoolEntry`). `LegacyRef` reads through `LegacyRefConverter` (MIG-002).

`NarrativeEncounter`, `QuestCatalog`, `QuestTriggerCatalog`, `MissionTemplateCatalog`, `GameSettings`, `VoxelVisualData` get no DTO (not converted; §4.8). If the strict reader meets a property not in the table, that is the export disagreeing with this spec: `MIG-E002`, and the implementer adds the property to the DTO and to this table in the same commit.

### 4.4 `LegacyIdMap.Default`

Unchanged keys are listed once per row. `→ null` = not converted.

| Kind | Legacy key → v2 ID |
|---|---|
| Block | `block_hull`, `block_hull_angled`, `block_core_small`, `block_core_cross`, `block_capacitor_small`, `block_shield_small`, `block_portal_small`, `block_prismatic_mirror_small`, `block_prismatic_mirror_fixed_small`, `block_thruster_small`, `block_thruster_medium`, `block_thruster_large`, `weapon_plasma_turret_small`, `weapon_plasma_turret_small_fixed`, `weapon_plasma_turret_medium`, `weapon_small_beam_turret`, `weapon_fixed_beam_turret_small`, `weapon_railgun_medium`, `weapon_ark_lance_medium`, `weapon_flak_cannon_medium`, `weapon_missile_launcher_small` → same |
| Weapon | `weapon_<x>` → `wpn_<x>` for the 9 weapon blocks above (e.g. `weapon_railgun_medium` → `wpn_railgun_medium`) |
| Item | `item_regeneration_serum`, `item_research_blueprint`, `item_strange_matter`, `resource_crew`, `resource_fuel`, `module_armor_plating`, `module_coolant_injector`, `module_scrap_collector`, `module_increase_weapon_rotation`, `module_micro_plating`, `module_shield_efficiency`, `module_trained_crew`, `officer_august`, `officer_gunner_vax` → same |
| Item | `palladium_resource` → `resource_palladium`; `slot_expansion_item` → `item_slot_expansion`; `strange_matter_container_empty` → `item_strange_matter_container_empty`; `strange_matter_container_full` → `item_strange_matter_container_full` |
| Item | `PaintBucket_NN_Name` → `paint_NN_name` with the name lower-cased and CamelCase split by `_`: `PaintBucket_00_Default` → `paint_00_default`, `_01_DarkGray` → `paint_01_dark_gray`, `_02_Red` → `paint_02_red`, `_03_Green` → `paint_03_green`, `_04_Blue` → `paint_04_blue`, `_05_Yellow` → `paint_05_yellow`, `_06_Orange` → `paint_06_orange`, `_07_Purple` → `paint_07_purple`, `_08_Cyan` → `paint_08_cyan`, `_09_Pink` → `paint_09_pink`, `_10_Brown` → `paint_10_brown`, `_11_Black` → `paint_11_black`, `_12_MilitaryGreen` → `paint_12_military_green`, `_13_SteelBlue` → `paint_13_steel_blue`, `_14_Tan` → `paint_14_tan`, `_15_DarkRed` → `paint_15_dark_red`, `_16_White` → `paint_16_white` |
| Item (numeric, narrative only) | `717185106` → `item_strange_matter_container_full` (orphaned `Id` of that asset, audit 05-B2); `500` → `resource_palladium` (open question 3) |
| Ship | `ship_base`, `ship_bastion`, `ship_beetle`, `ship_butterfly`, `ship_crescent`, `ship_fighter`, `ship_freighter`, `ship_frigate`, `ship_icarus`, `ship_red_elite`, `ship_shuttle`, `ship_striker`, `ship_vestari` → same |
| Faction | `faction_khuchad_clan`, `faction_tlaxcala`, `faction_vestari` → same; `faction_4` → null (duplicate of `faction_tlaxcala`, referenced nowhere) |
| Ability | `1` and `ability_shield_toggle` → `ability_shield_toggle`; `2` and `ability_speed_boost` → `ability_speed_boost` |
| AudioCue | Exactly the SPEC-64 §6.4 table (legacy ID → `cue_*` ID, e.g. `UI_Button_Click` → `cue_ui_click`, `Impact_Explosion_Small` → `cue_explosion_small`, `MainMenu` → `cue_music_menu`); `None` → null. SPEC-64 owns these IDs; this table never renames them |
| LootTable | `BaseLootTable` → `loot_base`; `CreativeModeLootTable` → `loot_creative`; `StartShipsTable` → null (empty, dead) |
| Body (by asset `$name` and by `entityId`) | `star_entity` → `body_star`; `star_entity_blackhole` → `body_star_blackhole`; `planet_barren` → `body_planet_barren`; `planet_gasgiant` → `body_planet_gas_giant`; `planet_ice` → `body_planet_ice`; `planet_icegiant` → `body_planet_ice_giant`; `planet_rocky` → `body_planet_rocky`; `planet_terrestrial` → `body_planet_terrestrial`; `planet_water` → `body_planet_water` (its `entityId` duplicates `planet_ice`; map by `$name` only); `moon_1`/`satelite_1` → `body_moon`; `station_1` → `body_station`; `ark_station_1`/`ark_station` → `body_station_ark`; `abandoned_station` → `body_station_abandoned`; `asteroidbelt_1` → `body_asteroid_belt`; `exitgate_active` → `body_gate_active`; `exitgate_dormant` → `body_gate_dormant`; `hyperlane_default` → `body_jump_point`; `derelict_ship_entity`/`derelict_ship` → `body_derelict_ship`; `debris_field_entity`/`derelict_ship_1` → `body_debris_field`; `resrource_cache_entity` → `body_resource_cache` |
| Encounter (SPEC-40 §4.13 / SPEC-33 §4.1 IDs) | `npc_enemy_1` → `encounter_raiders`; `npc_probe_tutorial` → `encounter_xeno_probe_tutorial`; `npc_story_hunter` → `encounter_whisper_hunter`; `npc_traveling_merchant` → `encounter_traveling_merchant`; `encounter_combat_01`, `encounter_combat_probe_tutorial`, `encounter_boss` → null (their formations are folded into the fleets, §4.7) |
| StarSystem | `ark_start`, `base_solar_system` → null (authored star systems are SPEC-30's; values handed off) |

### 4.5 Value tables for SPEC-04 targets

**Non-block items** (`ItemDefinition`, area `Items`). Property paths: `id`, `displayName`, `description`, `category`, `rarity`, `basePrice`, `maxStack`, `icon`. `maxStack` = 999 if `isStackable` else 1. `icon` = the sub-sprite of the sheet assigned to the Editor-only `LegacyMigrationSources` asset whose name equals legacy `ItemImage.name` (all legacy icons are `spritetest2_transparent_<n>`). The initial source is the existing `item_icons_sheet.png`; converter code never references the placeholder folder (Q14). Description: legacy text unchanged.

| v2 ID | Display name | Category | Rarity | Price | Stack | Dropped legacy data (`MIG-I091`) |
|---|---|---|---|---|---|---|
| `item_regeneration_serum` | Regeneration Serum | Resource | Epic | 100 | 999 | — |
| `item_research_blueprint` | Research Blueprint | Resource | Rare | 0 | 999 | — |
| `item_strange_matter` | Strange Matter | Quest | Legendary | 0 | 1 | legacy category Block |
| `resource_palladium` | Palladium [Pd] | Resource | Common | 100 | 1 | — |
| `resource_crew` | Crew | Resource | Common | 5 | 999 | — |
| `resource_fuel` | Fuel | Resource | Common | 100 | 999 | — |
| `item_slot_expansion` | Slot Expansion | Consumable | Epic | 500 | 999 | `EffectType AddModuleSlot` |
| `item_strange_matter_container_empty` | Strange Matter Container [EMPTY] | Quest | Legendary | 1000 | 999 | — |
| `item_strange_matter_container_full` | Strange Matter Container [FULL] | Quest | Legendary | 2500 | 1 | `RequiredSocketType`, 3 modifiers |
| `module_armor_plating` | Armor Plating | Module | Rare | 10 | 999 | modifiers |
| `module_coolant_injector` | Coolant Injector | Module | Common | 10 | 999 | modifiers |
| `module_scrap_collector` | Scrap Collector | Module | Common | 15 | 999 | modifiers |
| `module_increase_weapon_rotation` | Targeting Computer | Module | Common | 10 | 999 | modifiers; legacy category Block |
| `module_micro_plating` | Micro-Plating | Module | Common | 10 | 999 | modifiers |
| `module_shield_efficiency` | Shield Efficiency | Module | Common | 20 | 999 | modifiers |
| `module_trained_crew` | Trained Crew | Module | Legendary | 19 | 999 | modifiers |
| `officer_august` | August | Officer | Unique | 0 | 1 | — |
| `officer_gunner_vax` | Vax Vandervalk | Officer | Unique | 1000 | 1 | modifiers |
| `paint_00_default` … `paint_16_white` (17) | legacy `Name` ("Paint Bucket (Yellow)") | Paint | Common | 1 | 999 | `PaletteIndex` (equals NN), `EffectType Paint` |

Rarity names map by name (`Common, Rare, Epic, Legendary, Unique` exist in `ItemRarity`); an unknown name is `MIG-E021`.

**Blocks** (`BlockDefinition`, area `Blocks`): every value in SPEC-04 §4.10, plus the item fields: `displayName` = legacy `Name`, `description` = legacy `Description`, `category` = Block, `rarity` = legacy, `basePrice` = legacy `BasePrice`, `maxStack` = 999, `icon` as for items. Property paths (SPEC-04 §4.5/§4.6): `kind`, `shape`, `cells` (Vector3Int array), `clearance`, `allowedOrientations.Bits`, `attachFaces`, `mirrorPartner` (null), `cellHp`, `functionHp`, `cellMass`, `armorClass`, `materialLayer`, `propMesh`, `blockCategory`, and `modules`: one managed reference per module listed in the §4.10 "Category: modules" cell, in that order, assigned with `SerializedProperty.managedReferenceValue = new ThrusterModule { Params = new ThrusterParams { … } }` (SPEC-04 §4.6, ADR-0013); the weapon blocks get `WeaponMountModule { MuzzleOffsetCells }` and MIG-009 sets its `weapon`. Enums are written with `intValue`. Footprint and clearance are computed from the legacy data with the rules in §5.3 and must equal the SPEC-04 table (asserted by a test). Prop mesh: legacy `BlockMesh` `$ref` file name (`shipVoxels.obj` or `hulls.obj`) → the matching model assigned in `LegacyMigrationSources.blockModels` (Q14), sub-asset `Mesh` named `BlockMesh.name`; structural blocks get no mesh.

Dropped block data (`MIG-I091`): `RotationLocked`, `SocketTypeProvided`, `SocketsProvidedCount`, `Modifiers` (thrust is per block now), `EnergyRegenRate`/`MaxEnergyStorage`/`PassiveDrainRate` where SPEC-04 §4.10 doesn't use them, `ShieldRadius`, `ShieldForwardDirection`, portal `Entry*`, `ExitDirection`, `EntryRadius`, mirror `BeamThickness`, `IsRotating`/`RotationSpeed`/`MaxRotationAngle` of mirrors, `disableInputTrigger`, legacy `MuzzlePoint` (v2 muzzle offset is computed).

### 4.6 Meaning maps for other specs' targets

The implementer finds, in the owning spec's §4, the field carrying each meaning. Units: legacy length unit = 1 cell; v2 lengths in metres = legacy × 0.5 unless the target field is declared in cells.

**`ShipBlueprintDefinition` (SPEC-10, area `Ships`, ID = ship key):** display name ← `Name`; description ← `Description`; structure bytes ← VOXS (`VoxsEncodeOptions.ForAsset`) of the structure built by §5.4; price ← `BasePrice` (if SPEC-10/50 defines one). Dropped: `Thumbnail`, `ItemImage`, `AbilityIds`, `OfficerIds`, `GlobalStats`, `width`, `height`, `length`, `moduleSlots`.

**`WeaponDefinition` (SPEC-21, area `Weapons`, ID `wpn_<x>`):** one per legacy weapon block, merged with its `ProjectileData`. **MIG-009 writes the v2 values of SPEC-21 §4.9 for every numeric field** (not the unit-converted legacy values below); from the legacy data it takes only the identity: ID, delivery (projectile, beam, missile), damage type into `Game.Data.Combat.DamageType` (`Kinetic` → `Kinetic`, `Energy` → `Energy`, `HighExplosive` → `Explosive`), projectile visual and fire sound cue. The legacy tables stay as the reference for the mapping test (SPEC-21 Q8).

| Meaning | Source |
|---|---|
| fire rate (shots or pulses per second) | `FireRate` |
| accuracy 0..1 | `BaseAccuracy` |
| energy per shot (E) | `EnergyCost` |
| burst count, burst interval (s) | `BurstCount`, `BurstDelay` |
| turret traverse: half-arc (°), turn rate (°/s); fixed if `IsRotating == false` or `MaxRotationAngle == 0` | `MaxRotationAngle`, `RotationSpeed`, `IsRotating` |
| fire sound cue | `MuzzleSoundID` mapped through the AudioCue row of §4.4 (`None` → null) |
| delivery: beam if projectile `Speed == 0` and `VisualType == LaserBeam`; missile if `IsHoming`; else projectile | projectile |
| damage per hit / per pulse | projectile `Damage` |
| damage type | projectile `DamageType`: `Kinetic` → `Kinetic`, `Energy` → `Energy`, `HighExplosive` → `Explosive` |
| explosion radius (m) | projectile `ExplosionRadius × 0.5` when `ExplodesOnDeath` or `DamageType == HighExplosive`, else 0 |
| projectile speed (m/s), size (m) | projectile `Speed × 0.5`, `Size × 0.5` |
| lifetime min/max (s) | projectile `LifetimeMin`, `LifetimeMax` |
| homing, homing turn rate (°/s) | projectile `IsHoming`, `HomingTurnRate` |
| projectile visual | projectile `VisualType` (`StandardBullet`, `RailgunSlug`, `FlakShell`, `Missile`, `LaserBeam`) |

Legacy weapon values (for the test that checks the mapping):

| Weapon block | Projectile | FireRate | Acc. | Energy | Burst / delay | Rotating, speed, arc | Sound |
|---|---|---|---|---|---|---|---|
| `weapon_plasma_turret_small` | bullet | 2 | 0.80 | 1 | 1 / 0 | yes, 5000, 5 | Weapon_Plasma_Fire |
| `weapon_plasma_turret_small_fixed` | bullet | 2 | 0.95 | 1 | 1 / 0.1 | yes, 0, 0 (fixed) | Weapon_Plasma_Fire |
| `weapon_plasma_turret_medium` | bullet | 4 | 0.95 | 2 | 3 / 0.05 | yes, 8000, 90 | Weapon_Plasma_Fire |
| `weapon_small_beam_turret` | beam | 0.6 | 1.0 | 1 | 1 / 0.05 | yes, 3000, 90 | Weapon_Laser_Fire |
| `weapon_fixed_beam_turret_small` | beam | 0.6 | 1.0 | 1 | 1 / 0.1 | no (fixed) | Weapon_Laser_Fire |
| `weapon_railgun_medium` | energybullet | 0.3 | 0.90 | 1 | 4 / 0.1 | yes, 2000, 30 | None |
| `weapon_ark_lance_medium` | beam | 0.1 | 0.80 | 1 | 1 / 0.1 | yes, 5000, 35 | None |
| `weapon_flak_cannon_medium` | flak_shell | 1 | 0.60 | 1 | 15 / 0 | no (fixed) | None |
| `weapon_missile_launcher_small` | missile | 0.3 | 0.50 | 0 | 6 / 0.05 | no (fixed) | None |

| Projectile | Visual | Damage type | Damage | Expl. radius | Size | Speed | Life | Homing |
|---|---|---|---|---|---|---|---|---|
| `projectile_bullet` | StandardBullet | Kinetic | 20 | 0.5 | 0.5 | 150 | 1–1.5 | no |
| `projectile_energybullet` | RailgunSlug | Kinetic | 100 | 1 | 0.5 | 150 | 2–3 | no |
| `projectile_flak_shell` | FlakShell | HighExplosive | 0 | 1 | 0.5 | 150 | 0.5–2 | no, explodes |
| `projectile_missile` | Missile | Kinetic | 0 | 1 | 0.5 | 150 | 5 | yes, 60°/s, explodes |
| `projectile_beam` | LaserBeam | Energy | 20 | 1 | 0.5 | 0 | 1 | no |

Prismatic mirrors get no `WeaponDefinition` (they migrate as `Prop` blocks without modules; SPEC-04 open question 6).

**`AbilityDefinition` (SPEC-24, area `Abilities`):** MIG-010 writes the v2 values of SPEC-24 §4.1 (speed boost: `MaxSpeed` ×1.8, `Thrust` ×2.0, cost 15 E, 2 charges; shield toggle: threshold 0.3, cooldown 1 s); the legacy meanings below give the IDs, names, icons and kinds.

| v2 ID | Display name | Meaning → value |
|---|---|---|
| `ability_shield_toggle` | Shield Toggle | kind: shield toggle; minimum shield fraction to enable = `enableThreshold` 0.3; icon `spritetest2_transparent_40`; description "" (legacy "shield shield shield" is placeholder text, `MIG-I091`) |
| `ability_speed_boost` | Speed Boost | kind: boost; charges = 2; cooldown per use = 0.5 s; charge recharge = 3 s; boost duration = 1 s; icon `spritetest2_transparent_25` |

Ability grants (written by MIG-010 into `BlockDefinition.grantedAbilities`, SPEC-04 DATA-010): `block_thruster_small`, `block_thruster_medium`, `block_thruster_large` → `[ability_speed_boost]`; `block_shield_small` → `[ability_shield_toggle]`. Ship-level `AbilityIds` are dropped (abilities come from blocks).

**`AudioCueDefinition` (SPEC-64, area `Audio`):** one per Sounds/Music entry. Meanings: clip ← legacy `Clip` path with `Assets/Game/SFX/Music/<file>` → `Assets/Game/Audio/Music/<file>` and `Assets/Game/SFX/<file>` → `Assets/Game/Audio/Sfx/<file>` (all 12 distinct referenced files exist there); volume ← `Volume`; category ← music for `cue_music_*`, sfx otherwise. A missing clip file is `MIG-E060`. SPEC-64 §3.14 applies before writing: require exactly one provenance row, allow `Unverified` for development or `Confirmed`, and reject `Blocked`/missing/duplicate provenance with `MIG-E061`. MIG-011 imports only these legacy meanings; AUD-007 owns SPEC-64 §6.4 playback tuning, additional cues and config wiring.

**`FactionDefinition` (SPEC-33 §4.1, area `Factions`):** display name ← `displayName`; description ← `description`; `liveryColor` ← `color` RGBA; `shipNamePrefix` ← `shipNamePrefix`; `mapColorToken` = `--color-faction-<id without the faction_ prefix, underscores as hyphens>` (`--color-faction-khuchad-clan`, `--color-faction-tlaxcala`, `--color-faction-vestari`; SPEC-80 tokens); `isPlayerFaction` = false; doctrine ← list of (ship blueprint reference by mapped `shipKey`, weight); `basePlayerRelation` ← `basePlayerRelation`. Values: `faction_khuchad_clan` (Khuchad Clan, prefix KC, doctrine `ship_fighter` 0.5 + `ship_shuttle` 1, relation 0); `faction_tlaxcala` (Tlaxcala, TXS, no doctrine, −20); `faction_vestari` (Vestari, VSS, no doctrine, 0). `logo`, `crest` are null in the export (dropped).

**`LootTableDefinition` (SPEC-50, area `Loot`):** entries in file order: item reference (mapped through the Item/Block rows), min, max, chance percent (`dropChance`, 0–100). Entries referencing a `ShipItem` (`ship_beetle`, `ship_shuttle`, `ship_crescent`, `ship_fighter` in `BaseLootTable`) map to a ship entry only if SPEC-50 defines one; otherwise they are dropped with `MIG-W070`. `loot_base` has 19 legacy entries; `loot_creative` 37.

**`BodyTemplateDefinition` stubs (SPEC-30, area `Bodies`):** display name, role, description, map colour and `storyTag` (the SPEC-42 §4.10 tag of the template, e.g. `body_resource_cache` → `resource_cache`) only; everything else (radius, orbit ranges, textures, prefabs, icons, spawn rules) is dropped with one `MIG-I091` per body and re-authored by SPEC-30. Description: legacy text, except the placeholder "This is a placeholder description blah blah blah" → empty string.

| v2 ID | Display name | Role | v2 ID | Display name | Role |
|---|---|---|---|---|---|
| `body_star` | Star | Star | `body_station` | Station | Station |
| `body_star_blackhole` | Black Hole | Star | `body_station_ark` | ARK Station | Station |
| `body_planet_barren` | Barren Planet | Planet | `body_station_abandoned` | Abandoned Station | Station |
| `body_planet_gas_giant` | Gas Giant | Planet | `body_asteroid_belt` | Asteroid Belt | AsteroidBelt |
| `body_planet_ice` | Ice Planet | Planet | `body_gate_active` | Active Gate | Gate |
| `body_planet_ice_giant` | Ice Giant | Planet | `body_gate_dormant` | Dormant Gate | Gate |
| `body_planet_rocky` | Rocky Planet | Planet | `body_jump_point` | Jump Point | Gate |
| `body_planet_terrestrial` | Terrestrial Planet | Planet | `body_derelict_ship` | Derelict Ship | PointOfInterest |
| `body_planet_water` | Ocean Planet | Planet | `body_debris_field` | Debris Field | PointOfInterest |
| `body_moon` | Moon | Moon | `body_resource_cache` | Resource Cache | PointOfInterest |

Roles map to SPEC-30's `BodyRole` by name, except `Gate` → `LaneGate` and `AsteroidBelt` → `Belt`; any other unknown role → `MIG-W080` and the stub is written without a role.

### 4.7 Hand-off to SPEC-30, SPEC-33, SPEC-40/41/42

```csharp
namespace Game.Editor.Migration
{
    /// <summary>Typed legacy data that other specs convert. Built once per run, after this spec's own converters.</summary>
    public sealed class LegacyHandOff
    {
        public static LegacyHandOff Build(LegacyContentIndex index, LegacyIdMap map, Game.Data.Validation.ValidationReport report);
        public System.Collections.Generic.IReadOnlyList<LegacyBodyEncounterLink> BodyEncounterLinks { get; }
        public System.Collections.Generic.IReadOnlyList<LegacyFleetTemplate> Fleets { get; }
        public System.Collections.Generic.IReadOnlyList<LegacyStarSystemTemplate> StarSystems { get; }
        public Game.Editor.Migration.Dto.LegacyGalaxyDataDto Galaxy { get; }
        public Game.Editor.Migration.Dto.LegacyOpenWorldGalaxyConfigDto OpenWorld { get; }
        public const string NarrativeFolder = "narrative";   // Import/Legacy/narrative/*.json (authoritative narrative source)
    }

    /// <summary>Which legacy narrative encounters a body offered (for SPEC-41/42 beat locations).</summary>
    public readonly struct LegacyBodyEncounterLink
    { public readonly string BodyId; public readonly string LegacyEncounterId; public readonly float Weight; public readonly bool AutoInteract; public readonly string EncounterIdOverride; }

    public sealed class LegacyFleetTemplate
    {
        public string LegacyEntityId; public string SuggestedId;                     // §4.4 Encounter rows
        public string Allegiance;                                                   // "Enemy" | "Neutral"
        public float DetectionRadius, PatrolRadius, MovementSpeed;                  // legacy units
        public bool ChasesPlayer, UseFactionDoctrine, AutoInteractOnContact;
        public int DoctrineMinShips, DoctrineMaxShips;
        public System.Collections.Generic.IReadOnlyList<System.Collections.Generic.IReadOnlyList<string>> Formations; // v2 ship IDs, from the CombatEncounter(s) in its pool
        public string LegacyNarrativeEncounterId;                                    // npc_traveling_merchant → "event_merchant_shady"
    }

    public sealed class LegacyStarSystemTemplate
    {
        public string LegacySystemId; public Game.Editor.Migration.Dto.LegacySolarSystemDataDto Dto;
        public System.Collections.Generic.IReadOnlyList<string> StarBodyIds;
        public System.Collections.Generic.IReadOnlyList<(string BodyId, float Weight, int MaxCount)> EntityPool;
        public System.Collections.Generic.IReadOnlyList<string> ConnectedSystemIds;   // legacy systemIds
    }
}
```

Hand-off rules for the receiving specs:
1. Narrative sources are `Import/Legacy/narrative/{NarrativeEvents,StoryEvents,QuestDefinitions,QuestTriggers,MissionTemplates}.json`. The copies under `content/Encounters/Narrative/` and `content/Quests/` are importer outputs of the old game and must not be used.
2. Receivers write their own DTOs and converters under `Assets/Game/Editor/Migration/<Narrative|Encounters|Galaxy>/`, implement `ILegacyConverter`, and use `Order` ranges: SPEC-40 500–509, SPEC-41 510–519, SPEC-42 520–529, SPEC-33 600–609, SPEC-30 700–709.
3. They read files with `LegacyJson.ReadFile<T>` (strict), map every item/ship/body/faction token with `LegacyIdMap` (`TryMapItemToken` for DSL strings such as `REQ_ITEM(500, 3)` and `ADD_ITEM(ship_fighter, 1)`), write assets with `context.Writer`, and report with their own code prefix (`NAR-`, `DLG-`, `QST-`, `ENC-`, `GAL-`) into `context.Report`.
4. A token `TryMapItemToken` can't map is an error in the receiver's report, never a silent drop. `ADD_ITEM(ship_*)` resolves with `kind == Ship`; the receiver turns it into its "grant ship" effect.

### 4.8 Inventory of legacy files and targets (155 files under `content/`)

| Legacy files | # | Legacy type | Target | Task |
|---|---|---|---|---|
| `Items/Blocks/Hulls/{block_hull,block_hull_angled}` | 2 | BlockItem | `BlockDefinition` same ID | MIG-006 |
| `Items/Blocks/Cores/{block_core_small,block_core_cross}` | 2 | CoreBlockItem | `BlockDefinition` | MIG-006 |
| `Items/Blocks/Misc/{block_capacitor_small,block_thruster_small,block_thruster_medium,block_thruster_large}` | 4 | BlockItem | `BlockDefinition` | MIG-006 |
| `Items/Blocks/Misc/block_shield_small` | 1 | ShieldBlockItem | `BlockDefinition` | MIG-006 |
| `Items/Blocks/Misc/block_portal_small` | 1 | PortalBlockItem | `BlockDefinition` (Prop, no module; behaviour deferred) | MIG-006 |
| `Items/Blocks/Misc/{block_prismatic_mirror_small,block_prismatic_mirror_fixed_small}` | 2 | MirrorBlockItem | `BlockDefinition` (Prop, no module; behaviour deferred) | MIG-006 |
| `Items/Blocks/Weapons/weapon_*` | 9 | WeaponBlockItem | `BlockDefinition` + `WeaponDefinition wpn_*` | MIG-006, MIG-009 |
| `Items/Blocks/Weapons/Projectiles/projectile_*` | 5 | Projectile | merged into `WeaponDefinition`s | MIG-009 |
| `Items/Consumables/*` (excluding Paint) | 8 | ResourceItem | `ItemDefinition` | MIG-005 |
| `Items/Consumables/Paint/PaintBucket_*` | 17 | PaintBucketItem | `ItemDefinition paint_*` | MIG-005 |
| `Items/Modules/*` | 8 | SlottableItem | `ItemDefinition` | MIG-005 |
| `Items/Officers/*` | 2 | SlottableItem | `ItemDefinition` | MIG-005 |
| `Items/Ships/ship_*` | 13 | ShipItem | `ShipBlueprintDefinition` | MIG-008 |
| `Abilities/{ability_shield_toggle,ability_speed_boost}` | 2 | ShieldToggleAbilitySO, BoostAbilitySO | `AbilityDefinition` | MIG-010 |
| `Audio/AudioLibrary` (16 sounds, 4 tracks) | 1 | AudioLibrary | 20 × `AudioCueDefinition` | MIG-011 |
| `Factions/{faction_khuchad_clan,faction_tlaxcala,faction_vestari}` | 3 | FactionData | `FactionDefinition` | MIG-012 |
| `Factions/faction_4` | 1 | FactionData | not converted (duplicate) | MIG-012 |
| `Loot Tables/{BaseLootTable,CreativeModeLootTable}` | 2 | LootTable | `LootTableDefinition` | MIG-013 |
| `Loot Tables/StartShipsTable` | 1 | ShipTableSO | not converted (dead) | MIG-013 |
| `Galaxies/Solar System Entities/Planets/*` | 7 | PlanetEntity | `BodyTemplateDefinition` stub | MIG-014 |
| `Galaxies/Solar System Entities/Stars/*` | 2 | StarEntity | `BodyTemplateDefinition` stub | MIG-014 |
| `Galaxies/Solar System Entities/{abandoned_station,ark_station_1,asteroidbelt_1,debris_field_entity,derelict_ship_entity,exitgate_active,exitgate_dormant,moon_1,resrource_cache_entity,station_1}` | 10 | SolarSystemEntity | `BodyTemplateDefinition` stub | MIG-014 |
| `Galaxies/Solar System Entities/hyperlane_default` | 1 | HyperlaneEntity | `BodyTemplateDefinition` stub (`body_jump_point`) | MIG-014 |
| `Galaxies/Solar System Entities/Fleets/*` | 4 | NPCFleetEntity | hand-off `Fleets` (SPEC-33) | MIG-015 |
| `Encounters/Combat/*` | 3 | CombatEncounter | folded into `Fleets.Formations` (SPEC-33) | MIG-015 |
| `Galaxies/{ARK_StartSolarSystemData 1,BaseSolarSystemData,ExtractStablePoint}` | 3 | SolarSystemData | hand-off `StarSystems` (SPEC-30); `ExtractStablePoint` (empty `systemId`, roguelike leftover) is skipped with `MIG-I091` | MIG-015 |
| `Galaxies/Galaxy_01/GalaxyData_01`, `Galaxies/OpenWorldGalaxyConfig` | 2 | GalaxyData, OpenWorldGalaxyConfig | hand-off reference (SPEC-30) | MIG-015 |
| `Encounters/Narrative/*` | 34 | NarrativeEncounter | not used (copies; SPEC-41 converts `narrative/*.json`) | — |
| `Quests/{QuestCatalog,QuestTriggerCatalog,MissionTemplateCatalog}` | 3 | catalogs | not used (copies; SPEC-42 converts `narrative/*.json`) | — |
| `GameSettings/NewGameSettings` | 1 | GameSettings | not converted (pointed new games at the creative loot table, 04-B8; SPEC-01/50 define the loadout) | — |
| `GameSettings/VoxelVisualData` | 1 | VoxelVisualData | not converted (materials come from SPEC-11/80 config) | — |

Outside `content/`: `narrative/*.json` (5 files) → SPEC-40/41/42 via §4.7; `ui/*` → layout reference for SPEC-61; `input/legacy_InputSystem_Actions.inputactions` → binding reference for SPEC-03. None are converted here.

## 5. Systems, services and flow

No runtime systems. Everything runs in the Editor on the main thread.

### 5.1 Runner (`LegacyMigrationRunner.RunAll(force)`)

1. `report = new ValidationReport("MIG")`; `legacyRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../Import/Legacy"))`; missing folder → `MIG-E001`, stop.
2. `index = LegacyContentIndex.Load(legacyRoot, report)`: every `content/**/*.json` (ordinal order), parsed as `JObject`; parse failure → `MIG-E002` for that file; a file without `$path`/`$type` → `MIG-E002`.
3. `manifest = MigrationManifest.Load("Import/Migration/manifest.json")`; `writer = new LegacyAssetWriter("Assets/Game/Content", manifest, force, report)`.
4. Converters: `TypeCache.GetTypesDerivedFrom<ILegacyConverter>()`, non-abstract with a parameterless constructor, sorted by `Order` then `Name`.
5. `AssetDatabase.StartAssetEditing()` is **not** used (assets must be loadable between converters). For each converter: `context.Catalog = ContentCatalog.Build(database.Definitions, scratchReport)` (the scratch report is discarded; content errors are reported once at step 8); run `Convert(context)` inside try/catch (exception → `MIG-E099` naming the converter; continue); then `AssetDatabase.SaveAssets()` and `ContentDatabaseBuilder.Rebuild()`.
6. `context.HandOff` is built lazily the first time a converter with `Order ≥ 500` runs.
7. Save the manifest. The writer refreshes hashes immediately after owned/forced writes and owned link fills; filling a hand-edited asset’s empty link must not adopt its other fields as migration-owned (Q13).
8. `ContentValidatorRunner.Run(out contentReport)` (SPEC-04); `report.Merge(contentReport)`.
9. Write `Import/Migration/report.md` (§5.6). Log one summary line; `report.LogTo("Migration")`. Return `report`.

`RunCli()`: `RunAll(force: false)`, print `report.ToText()`, `EditorApplication.Exit(report.HasErrors ? 1 : 0)`.

### 5.2 Asset writer (`CreateOrUpdate<T>(area, id, fill, out outcome)`)

1. `path = $"{contentRoot}/{area}/{id}.asset"`; create the folder if missing.
2. `existing = AssetDatabase.LoadMainAssetAtPath(path)`. Different type than `T` → `MIG-E010`, `Failed`.
3. If `existing != null` and not `force`: `current = StableHash.Hash64(File.ReadAllBytes(path))`; if the manifest has no hash for `path`, or the hash differs → `SkippedHandEdited`, `MIG-I011` "kept hand-edited asset", return `existing`.
4. `tmp = ScriptableObject.CreateInstance<T>()`; `so = new SerializedObject(tmp)`; set `id`; call `fill(so)`; `so.ApplyModifiedPropertiesWithoutUndo()`. Converter fills use the internal `writer.Set(so, path, setter)` helper: missing property → `MIG-W090` and that setter is skipped; subsequent fields still run (Q13).
5. New asset: `AssetDatabase.CreateAsset(tmp, path)` → `Created`. Existing: `before = File.ReadAllBytes(path)`; `EditorUtility.CopySerialized(tmp, existing)` (keeps the GUID); `AssetDatabase.SaveAssetIfDirty(existing)`; compare bytes → `Unchanged` or `Updated`; `Object.DestroyImmediate(tmp)`.
6. Never writes outside `contentRoot`.

`SetLinkIfEmpty(asset, path, value)`: open `SerializedObject(asset)`; property missing → `MIG-W090`; current reference non-null and different → leave it (`MIG-I012`); null → set, apply, save, record `Updated`. `SetLinkArrayIfEmpty` does the same for an array property (empty array counts as empty).

### 5.3 Block conversion rules (MIG-006)

For each legacy file of types `BlockItem`, `CoreBlockItem`, `ShieldBlockItem`, `PortalBlockItem`, `MirrorBlockItem`, `WeaponBlockItem` (21 files, ordinal path order):
1. `id = IdMap(Block, RegistryKey)`.
2. Footprint: `VoxelShape` if non-empty, else every `(x, y, z)` with `0 ≤ x < BlockSize[0]`, `0 ≤ y < BlockSize[1]`, `0 ≤ z < BlockSize[2]`.
3. Clearance: `ClearanceDirections` minus cells inside the footprint (`MIG-W033` per dropped cell); for `block_portal_small` also drop `(1,0,0)` (`MIG-W034`, keeps the block achiral).
4. Footprint and clearance must equal the SPEC-04 §4.10 row (`LegacyBlockValues` table in code transcribes §4.10); mismatch → `MIG-E030`, asset not written.
5. Mesh: resolve per §4.5; missing file or sub-mesh → `MIG-E031`. Bounds check: with `b = mesh.bounds` (Unity space, cell units) and footprint bounds `[min − 0.5, max + 0.5]`, any face differing by more than 0.6 → `MIG-W032` (expected only for `weapon_ark_lance_medium` and `weapon_plasma_turret_medium`).
6. Write every property of §4.5 with the §4.10 values; assign each module a stable managed-reference ID equal to its unique BlockModuleId (Q15); `MuzzleOffsetCells` computed as `(centre x, centre y, BoundsMax.z + 0.5)`.

### 5.4 Ship conversion (MIG-008)

For each `ShipItem` file (13, ordinal path order), with `catalog` = current content:
1. `voxels = ShipVoxelData.voxels`. For each index `i`: `blockKey == ""` → `MIG-W050`, skip. `IsDestroyed` → skip (`MIG-I051`; none in the export). Unknown key → `MIG-E003`, skip. Resolve `def = catalog.Get<BlockDefinition>(id)`; `blockIndex = catalog.GetBlockIndex(def)`.
2. Orientation: `LegacyEuler.TryToOrientation(rotX, rotY, rotZ, out o, out err)`; false → `MIG-E040`, skip; `err > 0.001` → `MIG-W041`. If `!def.AllowedOrientations.Allows(o)` → `MIG-W042` (kept).
3. Colour: `colorIndex` 0..16 → that byte; else `MIG-W043` and 0. `moduleSlots` entries other than `""` → `MIG-W044` (dropped).
4. Processing order: all voxels whose block `RequiresInstance` (Functional or multi-cell) in list order, then the rest in list order.
5. Placement: cells = `origin + OrientationTables.Rotate(c, o)` for `c` in `def.Cells` (root first), `origin = (x, y, z)`. If any cell is in `occupied` (`Dictionary<int3, int>`) → `MIG-W030` naming both blocks, skip. Else mark all cells.
6. Encoding into structure arrays (SPEC-10 types, `CellSize = 0.5`):
   - Palette: entry 0 = `(Block 0, Orient 0, Color 0)` (air). Single-cell blocks that don't `RequireInstance`: find-or-append `(blockIndex, o, color)`; cell `Ref` = palette index.
   - Instances: append `BlockInstance { Block = blockIndex, Orient = o, Color = color, Origin = origin, FunctionHp = def.FunctionHp, Functional = Entity.Null }`; each cell `Ref = 0x8000 | instanceIndex`.
   - Every placed cell: `Hp = 255`, `Flags = 0` (instance root uses CellFlags.InstanceRoot per SPEC-10 §5.2; other cells remain 0).
   - Bricks: `brick = cell >> 4` (arithmetic shift, correct for negatives), `local = cell & 15`; bricks are created on first use in a `Dictionary<int3, int>` with 4,096 zero cells each; `Cells[brick × 4096 + VoxsCodec.CellIndex(local)]`.
   - `AnchorInstance` = the instance index of the core if exactly one placed block has a `Core` module; none → −1 and `MIG-W052`; more than one → −1 and `MIG-W053`.
7. `bytes = VoxsCodec.Encode(source, VoxsBlockNames.FromCatalog(catalog), VoxsEncodeOptions.ForAsset)`; verify by `TryDecode` and comparing palette/instances/cells (mismatch → `MIG-E054`, not written).
8. Write the `ShipBlueprintDefinition` (§4.6). Add a row to the report's ship table.

Expected results with the real export (asserted by MIG-016; "palette" excludes entry 0):

| Ship | Legacy voxels | Empty keys | Placed | Dropped overlaps | Cells | Palette | Instances | Cores |
|---|---|---|---|---|---|---|---|---|
| `ship_base` | 4 | 0 | 4 | 0 | 4 | 1 | 1 | 1 |
| `ship_bastion` | 343 | 0 | 334 | 9 (5 thruster_medium, 4 hull) | 361 | 3 | 3 | 0 |
| `ship_beetle` | 66 | 0 | 64 | 2 (thruster_medium) | 91 | 2 | 6 | 1 |
| `ship_butterfly` | 757 | 0 | 757 | 0 | 815 | 14 | 21 | 1 |
| `ship_crescent` | 270 | 2 | 259 | 9 (6 thruster_medium, 3 hull) | 306 | 11 | 24 | 1 |
| `ship_fighter` | 965 | 0 | 965 | 0 | 1005 | 46 | 29 | 1 |
| `ship_freighter` | 343 | 0 | 334 | 9 (5 thruster_medium, 4 hull) | 361 | 3 | 3 | 0 |
| `ship_frigate` | 119 | 0 | 119 | 0 | 127 | 19 | 9 | 1 |
| `ship_icarus` | 270 | 2 | 258 | 10 (6 thruster_medium, 4 hull) | 305 | 3 | 23 | 0 |
| `ship_red_elite` | 789 | 0 | 787 | 2 (hull) | 819 | 2 | 22 | 0 |
| `ship_shuttle` | 119 | 0 | 119 | 0 | 127 | 19 | 9 | 1 |
| `ship_striker` | 50 | 0 | 44 | 6 (3 plasma_turret_small, 1 thruster_medium, 2 hull) | 74 | 2 | 14 | 0 |
| `ship_vestari` | 946 | 0 | 946 | 0 | 972 | 36 | 21 | 1 |

Coreless ships (`ship_bastion`, `ship_freighter`, `ship_icarus`, `ship_red_elite`, `ship_striker`) produce `MIG-W052` and import (open question 1).

### 5.5 Euler conversion (`LegacyEuler.TryToOrientation`)

1. For each of `rotX, rotY, rotZ`: `q = (int)Math.Round(a / 90.0, MidpointRounding.AwayFromZero)`; `err = |a − 90q|`; `err > 1.0` → return false. `q = ((q % 4) + 4) % 4`.
2. `c(q) = [1, 0, −1, 0][q]`, `s(q) = [0, 1, 0, −1][q]`.
3. `Rx = [[1,0,0],[0,c,−s],[0,s,c]]`, `Ry = [[c,0,s],[0,1,0],[−s,0,c]]`, `Rz = [[c,−s,0],[s,c,0],[0,0,1]]`; `R = Ry · Rx · Rz` (integer).
4. `OrientationTables.TryFromMatrix(R, out orientation)` (always true for these inputs); `maxSnapErrorDegrees` = the largest `err`.

Test vectors (every rotation present in the export, `(rotX, rotY, rotZ) → orientation`): `(0,0,0)→0`, `(0,90,0)→4`, `(0,180,0)→8`, `(0,270,0)→12`, `(0,0,90)→1`, `(0,0,180)→2`, `(0,0,270)→3`, `(0,90,90)→5`, `(0,90,180)→6`, `(0,90,270)→7`, `(0,180,90)→9`, `(0,180,180)→10`, `(0,180,270)→11`, `(0,270,90)→13`, `(0,270,180)→14`, `(0,270,270)→15`, `(0,0.99999988,0)→0` with warning, `(0,0.70710671,0)→0` with warning; extra: `(90,0,0)→20`, `(270,0,0)→16`, `(0,45,0)` → false.

### 5.6 Report (`Import/Migration/report.md`)

Deterministic (no timestamps, ordinal sorting, LF line endings):
1. `# Legacy migration report` and a line `Converters: <Name (Order)>, …`.
2. `## Assets` — table `| Asset | Outcome |` for every writer outcome, sorted by path.
3. `## Ships` — the §5.4 table computed from this run.
4. `## Entries` — `report.ToText(2000)`.
5. `## Summary` — counts per severity and per outcome.

### 5.7 Report codes

| Code | Severity | Meaning |
|---|---|---|
| MIG-E001 | Error | `Import/Legacy` not found |
| MIG-E002 | Error | JSON unreadable, missing `$path`/`$type`, or property not declared in its DTO |
| MIG-E003 | Error | Legacy key not in `LegacyIdMap` |
| MIG-E004 | Error | `$ref` that resolves to no legacy file |
| MIG-E010 | Error | Asset at target path has another type |
| MIG-I011 / I012 | Info | Hand-edited asset kept / link already set |
| MIG-E021 | Error | Unknown legacy enum name (rarity, category, damage type) |
| MIG-E030 | Error | Computed footprint/clearance differs from SPEC-04 §4.10 |
| MIG-E031 | Error | Prop mesh file or sub-mesh missing |
| MIG-W032 | Warning | Prop mesh bounds differ from the footprint by > 0.6 cell |
| MIG-W033 / W034 | Warning | Clearance cell dropped (inside footprint / to keep the block achiral) |
| MIG-W030 | Warning | Overlapping block dropped |
| MIG-E040 / W041 / W042 / W043 / W044 | Error / Warning | Rotation not a cube rotation / snapped / orientation not allowed / bad colour / module slots dropped |
| MIG-W050 / I051 | Warning / Info | Empty block key / destroyed voxel skipped |
| MIG-W052 / W053 | Warning | Ship without core / with several cores |
| MIG-E054 | Error | VOXS verification mismatch |
| MIG-E060 | Error | Audio clip file missing |
| MIG-E061 | Error | Audio clip blocked or provenance missing, duplicated or invalid; cue skipped |
| MIG-W070 | Warning | Loot entry dropped (target cannot hold ships) |
| MIG-W080 | Warning | Body role not available in SPEC-30 |
| MIG-W090 | Warning | Target property path not found |
| MIG-I091 | Info | Legacy data intentionally dropped (lists the fields) |
| MIG-E099 | Error | Converter threw |

## 6. Public API

| Call | Who | Notes |
|---|---|---|
| Menu *Game/Legacy Migration/Run All* | Developers | Safe to re-run; hand-edited assets kept |
| Menu *Run All (Force)* | Developers | Overwrites hand-edited assets (asks for confirmation with `EditorUtility.DisplayDialog`) |
| `-executeMethod Game.Editor.Migration.LegacyMigrationRunner.RunCli` | CI | Exit code 1 on errors |
| `ILegacyConverter` | SPEC-30/33/40/41/42 | Order ranges §4.7 |
| `LegacyJson`, `LegacyContentIndex`, `LegacyIdMap`, `LegacyHandOff`, `LegacyAssetWriter` | Receiving converters | Through `LegacyMigrationContext` |

## 7. Edge cases and failure behaviour

| Situation | Required behaviour |
|---|---|
| Running twice with no changes | Every asset `Unchanged`; two unchanged runs have byte-identical reports and manifests (the initial run truthfully reports Created; Q13) |
| Asset tuned by hand, then re-run | `SkippedHandEdited` + `MIG-I011`; nothing overwritten; links filled only if empty |
| Asset deleted by hand, then re-run | Re-created |
| Hand-made asset already at a target path (not in manifest) | Treated as hand-edited (kept) unless Force |
| A target definition type doesn't exist yet (its spec isn't implemented) | Its converter isn't present yet; nothing to do. A converter whose target type exists but lacks a field → `MIG-W090` per field |
| Legacy file with an extra property | `MIG-E002`; the file is not converted; fix the DTO and this spec |
| Ship voxel with non-cube rotation | `MIG-E040`, voxel skipped, ship still written |
| Overlapping blocks in a ship | Functional-first rule, `MIG-W030` per drop (expected counts in §5.4) |
| Ship without a core | Imported with `AnchorInstance = −1`, `MIG-W052` |
| Empty `blockKey` | `MIG-W050`, skipped |
| Numeric item ID in narrative DSL | `TryMapItemToken` maps `500`, `717185106`; any other number fails (receiver reports) |
| Clip path with a different folder | Mapped by the two path rules; missing file → `MIG-E060` |
| Audio clip `Blocked`, or without exactly one valid provenance row | `MIG-E061`, cue skipped; other entries continue. `Unverified` clips remain development-only under SPEC-64's release-build check |
| Converter throws | `MIG-E099`; later converters still run; database rebuilt |
| Run during Play Mode | Menu items disabled (`validate` functions return `!EditorApplication.isPlayingOrWillChangePlaymode`) |
| `Import/Migration/` missing | Created |

## 8. Performance budget

No runtime cost. Editor budgets: full run on the real export ≤ 60 s (13 ships, ~170 assets, database rebuilds included); each ship ≤ 200 ms; memory bounded by the largest ship (< 10 MB). Measured once by MIG-016 and logged in the ledger.

## 9. Tasks

### MIG-001 — Public contract types
- **Milestone:** M0
- **Tier:** standard — transcribes the exact code of §4.2 and §4.7 (bodies throw NotImplementedException)
- **Depends on:** `DATA-001`
- **Do:** Create the types of §4.2 and §4.7 with the given members; DTO classes of §4.3 with properties and `[JsonProperty]` names exactly as listed (DTO bodies are data, write them fully); `LegacyRef` JSON converter stub.
- **Files:** `Assets/Game/Editor/Migration/{ILegacyConverter,LegacyMigrationContext,LegacyContentIndex,LegacyFile,LegacyRef,LegacyJson,LegacyIdKind,LegacyIdMapping,LegacyIdMap,WriteOutcome,LegacyAssetWriter,MigrationManifest,LegacyEuler,LegacyMigrationRunner,LegacyHandOff,LegacyBodyEncounterLink,LegacyFleetTemplate,LegacyStarSystemTemplate}.cs`; `Assets/Game/Editor/Migration/Dto/*.cs` (one file per DTO of §4.3)
- **Acceptance criteria:**
  - [ ] `tools/compile.sh` passes; all types are in `Game.Editor` (Editor platform only).
  - [ ] Every DTO property has the exact JSON name of §4.3.
- **Tests:** `LegacyDtoShapeTests.EveryDto_HasDeclaredJsonNames` (reflection over `[JsonProperty]` vs a table in the test) (`Tests/EditMode/Editor/Migration/`).

### MIG-002 — Legacy JSON index and strict reading
- **Milestone:** M0
- **Tier:** standard — explicit strict-reading rules and DTO table
- **Depends on:** `MIG-001`
- **Do:** Implement `LegacyContentIndex.Load`, `TryResolve`, `LegacyJson.StrictSettings` (`MissingMemberHandling.Error`, `NullValueHandling.Include`, `LegacyRef` converter that accepts `null` or `{"$ref", "name", "type", "sub"}`), `Read<T>`, `ReadFile<T>`, error reporting with file path and JSON path (`JsonSerializationException.Path`).
- **Files:** `Assets/Game/Editor/Migration/{LegacyContentIndex,LegacyJson,LegacyRef}.cs`; `Assets/Game/Tests/EditMode/Editor/Migration/LegacyJsonTests.cs`
- **Acceptance criteria:**
  - [ ] Loading the real export indexes 155 files with zero errors.
  - [ ] Every file of every `$type` in §4.3 reads into its DTO with zero errors.
  - [ ] An in-memory JSON with an extra property yields `MIG-E002` naming the property path.
  - [ ] Every `$ref` in the export that points into `Assets/Resources/` resolves to an indexed file, except references to `.png`, `.obj`, `.wav`, `.ogg`, `.prefab`, `.mat`, `.fbx` (assets, not content).
- **Tests:** `LegacyJsonTests.RealExport_IndexesAllFiles`, `.RealExport_AllTypesReadStrictly`, `.ExtraProperty_IsE002`, `.ContentRefs_AllResolve`.

### MIG-003 — Legacy ID map
- **Milestone:** M0
- **Tier:** standard — transcription of §4.4
- **Depends on:** `MIG-001`
- **Do:** Implement `LegacyIdMap.Default` as a static readonly array transcribing every row of §4.4; `TryMap`, `TryMapItemToken` (Item, then Block, then Ship; trims whitespace).
- **Files:** `Assets/Game/Editor/Migration/LegacyIdMap.cs`; `Assets/Game/Tests/EditMode/Editor/Migration/LegacyIdMapTests.cs`
- **Acceptance criteria:**
  - [ ] Every `RegistryKey`, faction `id`, ability `<ID>` and `$name`, audio `ID`/`Track`, loot table `$name`, body `$name`/`entityId` and NPC fleet `entityId` in the export maps (possibly to null).
  - [ ] Every non-null v2 ID passes SPEC-04's ID format and prefix rules for its target type; no two legacy keys of different meaning map to the same v2 ID (aliases within one row excepted).
  - [ ] `TryMapItemToken("500")` → `resource_palladium`; `("717185106")` → `item_strange_matter_container_full`; `("ship_fighter")` → Ship.
- **Tests:** `LegacyIdMapTests.EveryExportKey_IsMapped`, `.V2Ids_AreValidAndUnique`, `.ItemTokens_Numeric_And_Ship`.

### MIG-004 — Asset writer, manifest, runner and report
- **Milestone:** M0
- **Tier:** frontier — F3: a bug silently overwrites hand-tuned content or makes migration non-idempotent
- **Depends on:** `MIG-002`, `MIG-003`, `SPEC-04`
- **Do:** Implement `LegacyAssetWriter` (§5.2), `MigrationManifest`, `LegacyMigrationRunner` (§5.1) including menu items, Force confirmation, CLI, `report.md` (§5.6). Tests use a temporary content root `Assets/Game/Content/_MigrationTest/` and a temporary manifest, deleted in `TearDown`.
- **Files:** `Assets/Game/Editor/Migration/{LegacyAssetWriter,MigrationManifest,LegacyMigrationRunner,MigrationReportWriter}.cs`; `Assets/Game/Tests/EditMode/Editor/Migration/LegacyAssetWriterTests.cs`
- **Acceptance criteria:**
  - [ ] Create → `Created`; same fill again → `Unchanged` with identical bytes and GUID.
  - [ ] Hand-edit (change a field, save) → next run `SkippedHandEdited`; with Force → `Updated`.
  - [ ] `SetLinkIfEmpty` sets a null reference on a hand-edited asset and leaves a non-null one.
  - [ ] Unknown property path → `MIG-W090`, other properties still written.
  - [ ] `report.md` and `manifest.json` are byte-identical across two runs.
- **Tests:** `LegacyAssetWriterTests.Create_ThenUnchanged`, `.HandEdited_IsKept_ForceOverwrites`, `.SetLinkIfEmpty_Rules`, `.MissingProperty_IsW090`, `.ReportAndManifest_AreDeterministic`.

### MIG-005 — Item converter
- **Milestone:** M0
- **Tier:** standard — explicit field map (§4.5)
- **Depends on:** `MIG-004`
- **Do:** `ItemConverter` (`Order` 100, `Name` "Items"): every `ResourceItem`, `SlottableItem`, `PaintBucketItem` file → `ItemDefinition` per the §4.5 item table; icon sprite lookup in `item_icons_sheet.png` sub-assets; `MIG-I091` lists dropped fields per asset.
- **Files:** `Assets/Game/Editor/Migration/Converters/ItemConverter.cs`; `Assets/Game/Content/Items/*.asset` (35 assets, generated); `Assets/Game/Tests/EditMode/Editor/Migration/ItemConverterTests.cs`
- **Acceptance criteria:**
  - [ ] 35 item assets exist with the IDs, names, categories, rarities, prices and stack sizes of §4.5.
  - [ ] Every icon is assigned and named `spritetest2_transparent_<n>` as in the legacy file.
  - [ ] SPEC-04 validation of these assets has zero errors.
- **Tests:** `ItemConverterTests.RealExport_ProducesExpectedItems` (table-driven over §4.5), `.Icons_Assigned`, `.DroppedFields_Reported`.

### MIG-006 — Block converter
- **Milestone:** M0
- **Tier:** standard — explicit value table (SPEC-04 §4.10) and footprint rules (§5.3)
- **Depends on:** `MIG-005`
- **Do:** `BlockConverter` (`Order` 110, `Name` "Blocks") per §5.3 with a `LegacyBlockValues` table transcribing SPEC-04 §4.10; mesh lookup and bounds check.
- **Files:** `Assets/Game/Editor/Migration/Converters/{BlockConverter,LegacyBlockValues}.cs`; `Assets/Game/Content/Blocks/*.asset` (21 assets, generated); `Assets/Game/Tests/EditMode/Editor/Migration/BlockConverterTests.cs`
- **Acceptance criteria:**
  - [ ] 21 block assets whose every field equals SPEC-04 §4.10 (table-driven test reading back the assets).
  - [ ] `MIG-W032` exactly for `weapon_ark_lance_medium` and `weapon_plasma_turret_medium`; `MIG-W033` for railgun (1 cell) and ark lance (2 cells); `MIG-W034` for the portal.
  - [ ] `ProjectContentTests.ProjectContent_HasZeroValidationErrors` (SPEC-04) still passes.
- **Tests:** `BlockConverterTests.RealExport_MatchesSpec04Table`, `.Footprints_FromBlockSizeAndVoxelShape`, `.ClearanceDrops_Reported`, `.MeshBounds_Warnings`.

### MIG-007 — Legacy Euler to orientation
- **Milestone:** M0
- **Tier:** frontier — F3: a wrong mapping silently rotates every migrated block
- **Depends on:** `MIG-001`, `SPEC-04`
- **Do:** Implement `LegacyEuler.TryToOrientation` per §5.5.
- **Files:** `Assets/Game/Editor/Migration/LegacyEuler.cs`; `Assets/Game/Tests/EditMode/Editor/Migration/LegacyEulerTests.cs`
- **Acceptance criteria:**
  - [ ] Every test vector of §5.5 passes, including the two snapped values and the failing 45°.
  - [ ] For all 64 combinations of `(rotX, rotY, rotZ) ∈ {0,90,180,270}³`, `OrientationTables.Rotate(v, o)` equals `Quaternion.Euler(rotX, rotY, rotZ) * v` rounded, for v = (1,0,0), (0,1,0), (0,0,1).
- **Tests:** `LegacyEulerTests.TestVectors`, `.MatchesUnityQuaternionEuler_All64`.

### MIG-008 — Ship blueprint converter
- **Milestone:** M0
- **Tier:** frontier — F3: footprint placement, overlap resolution and voxel encoding
- **Depends on:** `MIG-006`, `MIG-007`, `SPEC-05`, `VOX-001`
- **Do:** `ShipConverter` (`Order` 300, `Name` "Ships") per §5.4, with the placement/encoding logic in a pure class `LegacyShipBuilder` (input: DTO + a block resolver; output: `VoxsSource` arrays + statistics) so it can be unit-tested with in-memory definitions (SPEC-04 `TestContentFactory`).
- **Files:** `Assets/Game/Editor/Migration/Converters/{ShipConverter,LegacyShipBuilder,LegacyShipBuildResult}.cs`; `Assets/Game/Content/Ships/*.asset` (13, generated); `Assets/Game/Tests/EditMode/Editor/Migration/{LegacyShipBuilderTests,ShipConverterTests}.cs`
- **Acceptance criteria:**
  - [ ] The per-ship numbers of the §5.4 table are reproduced exactly.
  - [ ] Every ship's VOXS decodes (`TryDecode`) to the same arrays; every multi-cell block's cells all reference its instance; no cell is referenced by two blocks.
  - [ ] Unit cases: two overlapping thrusters keep the first; a hull overlapping a thruster is dropped even if listed first; negative coordinates land in the right brick (`(−1, 0, 0)` → brick `(−1,0,0)`, local `(15,0,0)`); a core yields `AnchorInstance` = its instance index.
  - [ ] Running the converter twice leaves all 13 assets `Unchanged`.
- **Integration prerequisite (Q16):** implement the existing ShipBlueprintDefinition.Validate contract here so imported M0 assets can pass content validation. VOX-010 retains the payload setter and config factory.
- **Tests:** `ShipConverterTests.Blueprint_ValidationForMigration`; `LegacyShipBuilderTests.Overlap_FunctionalFirst`, `.NegativeCoordinates_BrickMath`, `.MultiCellBlocks_ReferenceInstance`, `.Anchor_FromSingleCore`; `ShipConverterTests.RealExport_MatchesExpectedTable`, `.Voxs_RoundTrips`, `.SecondRun_Unchanged`.

### MIG-009 — Weapon converter and weapon links
- **Milestone:** M2
- **Tier:** standard — meaning map with legacy values given (§4.6)
- **Depends on:** `MIG-006`, `MIG-011`, `WPN-001`, `DATA-007`
- **Do:** `WeaponConverter` (`Order` 220, `Name` "Weapons"): one `WeaponDefinition wpn_<x>` per weapon block merged with its projectile (§4.6); then `SetLinkIfEmpty(block, "modules.Array.data[k].weapon", wpn)` on the block's `WeaponMountModule` (index `k` in `modules`) for each of the 9 weapon blocks (SPEC-04 DATA-010 field).
- **Files:** `Assets/Game/Editor/Migration/Converters/WeaponConverter.cs`; `Assets/Game/Content/Weapons/*.asset` (9, generated); `Assets/Game/Tests/EditMode/Editor/Migration/WeaponConverterTests.cs`
- **Acceptance criteria:**
  - [ ] 9 weapon assets whose numeric fields equal SPEC-21 §4.9 row by row; delivery, `DamageType`, visual and sound cue follow the legacy mapping of §4.6.
  - [ ] Every weapon block's `WeaponMountModule.weapon` references its `wpn_` asset; SPEC-04 `DATA-E047` reports nothing.
- **Tests:** `WeaponConverterTests.RealExport_ValuesMatchSpec21`, `.DamageTypeMapped`, `.WeaponBlocks_AreLinked`.

### MIG-010 — Ability converter and ability grants
- **Milestone:** M2
- **Tier:** standard — two assets with given values and four links
- **Depends on:** `MIG-006`, `ABL-001`, `SPEC-04`
- **Do:** `AbilityConverter` (`Order` 210, `Name` "Abilities") per §4.6; then `SetLinkArrayIfEmpty(block, "grantedAbilities", …)` for the four blocks listed.
- **Files:** `Assets/Game/Editor/Migration/Converters/AbilityConverter.cs`; `Assets/Game/Content/Abilities/*.asset` (2); `Assets/Game/Tests/EditMode/Editor/Migration/AbilityConverterTests.cs`
- **Acceptance criteria:**
  - [ ] Both abilities exist with the SPEC-24 §4.1 values; thrusters grant `ability_speed_boost`, the shield grants `ability_shield_toggle`.
- **Tests:** `AbilityConverterTests.RealExport_ValuesAndGrants`.

### MIG-011 — Audio cue converter
- **Milestone:** M2
- **Tier:** standard — 20 rows with explicit path rules
- **Depends on:** `MIG-004`, `AUD-001`
- **Do:** `AudioCueConverter` (`Order` 200, `Name` "Audio") per §4.6 and the AudioCue rows of §4.4.
- **Files:** `Assets/Game/Editor/Migration/Converters/AudioCueConverter.cs`; `Assets/Game/Content/Audio/*.asset` (20); `Assets/Game/Tests/EditMode/Editor/Migration/AudioCueConverterTests.cs`
- **Acceptance criteria:**
  - [ ] 20 cue assets; each clip references the file under `Assets/Game/Audio/{Sfx,Music}/` with the legacy file name; volumes equal legacy.
- **Tests:** `AudioCueConverterTests.RealExport_TwentyCuesWithClips`.

### MIG-012 — Faction converter
- **Milestone:** M3
- **Tier:** standard — explicit meaning map and values (§4.6)
- **Depends on:** `MIG-008`, `ENC-001`
- **Do:** `FactionConverter` (`Order` 400, `Name` "Factions"); skip `faction_4` with `MIG-I091`.
- **Files:** `Assets/Game/Editor/Migration/Converters/FactionConverter.cs`; `Assets/Game/Content/Factions/*.asset` (3); `Assets/Game/Tests/EditMode/Editor/Migration/FactionConverterTests.cs`
- **Acceptance criteria:**
  - [ ] 3 factions with §4.6 values, `mapColorToken` per the rule and `isPlayerFaction == false`; Khuchad doctrine references `ship_fighter` (0.5) and `ship_shuttle` (1).
- **Tests:** `FactionConverterTests.RealExport_ThreeFactions`.

### MIG-013 — Loot table converter
- **Milestone:** M3
- **Tier:** standard — explicit meaning map (§4.6)
- **Depends on:** `MIG-008`, `ECO-001`
- **Do:** `LootTableConverter` (`Order` 410, `Name` "Loot").
- **Files:** `Assets/Game/Editor/Migration/Converters/LootTableConverter.cs`; `Assets/Game/Content/Loot/*.asset` (2); `Assets/Game/Tests/EditMode/Editor/Migration/LootTableConverterTests.cs`
- **Acceptance criteria:**
  - [ ] `loot_base` has 19 entries (or 15 + 4 × `MIG-W070` if SPEC-50 has no ship entries) in legacy order with legacy amounts and chances; `loot_creative` has its 37 entries.
- **Tests:** `LootTableConverterTests.RealExport_EntriesInOrder`.

### MIG-014 — Body template stubs
- **Milestone:** M3
- **Tier:** standard — 20 rows with given names and roles
- **Depends on:** `MIG-004`, `GAL-001`
- **Do:** `BodyTemplateConverter` (`Order` 420, `Name` "Bodies") per the §4.6 body table.
- **Files:** `Assets/Game/Editor/Migration/Converters/BodyTemplateConverter.cs`; `Assets/Game/Content/Bodies/*.asset` (20); `Assets/Game/Tests/EditMode/Editor/Migration/BodyTemplateConverterTests.cs`
- **Acceptance criteria:**
  - [ ] 20 stubs with the table's names and roles (`Gate` → `LaneGate`, `AsteroidBelt` → `Belt`) and the SPEC-42 §4.10 `storyTag`; placeholder descriptions are empty strings.
- **Tests:** `BodyTemplateConverterTests.RealExport_TwentyStubs`.

### MIG-015 — Hand-off package
- **Milestone:** M3
- **Tier:** standard — typed data extraction with explicit rules (§4.7)
- **Depends on:** `MIG-002`, `MIG-003`
- **Do:** Implement `LegacyHandOff.Build`: body encounter links from every entity's `encounterPool` and `encounterIdOverride` (body ID via `LegacyIdMap` Body rows; fleets excluded), fleet templates (formations resolved from each referenced `CombatEncounter` to v2 ship IDs; `npc_traveling_merchant` gets `LegacyNarrativeEncounterId = "event_merchant_shady"`), star-system templates for `ark_start` and `base_solar_system` (skip `ExtractStablePoint`), galaxy DTOs. Receivers use §4.7 as their documentation.
- **Files:** `Assets/Game/Editor/Migration/LegacyHandOff.cs`; `Assets/Game/Tests/EditMode/Editor/Migration/LegacyHandOffTests.cs`
- **Acceptance criteria:**
  - [ ] 4 fleets; `npc_probe_tutorial` has one formation of 3 × `ship_frigate`; `npc_enemy_1` has the 3 formations of `encounter_combat_01`.
  - [ ] Every link's `BodyId` is a valid v2 body ID; `body_station_ark` links `demo_station_hub`.
  - [ ] 2 star systems with their entity pools mapped to body IDs.
- **Tests:** `LegacyHandOffTests.Fleets_FormationsResolved`, `.BodyLinks_UseV2Ids`, `.StarSystems_PoolsMapped`.

### MIG-016 — Migration gate test
- **Milestone:** M0
- **Tier:** standard — test of already-specified results
- **Depends on:** `MIG-008`
- **Do:** EditMode test that runs `LegacyMigrationRunner.RunAll(force: false)` on the real export twice and checks the results; logs the run time in the ledger.
- **Files:** `Assets/Game/Tests/EditMode/Editor/Migration/MigrationGateTests.cs`
- **Acceptance criteria:**
  - [ ] Zero `MIG-E*` and zero SPEC-04 content errors after the run (M0 exit gate).
  - [ ] Second run: every outcome `Unchanged`; `report.md` and `manifest.json` byte-identical.
  - [ ] The ship table in `report.md` equals §5.4.
  - [ ] Full run ≤ 60 s.
- **Tests:** `MigrationGateTests.RealExport_ZeroErrors`, `.SecondRun_IsIdempotent`, `.ShipTable_MatchesSpec`, `.RunTime_Under60Seconds` (`[Category("Performance")]`).

## 10. Open questions

| # | Question | Default if unanswered | Decider |
|---|---|---|---|
| 1 | Coreless legacy ships (bastion, freighter, icarus, red_elite, striker): add a core? | No; import as-is with `AnchorInstance = −1`; SPEC-10's blueprint validator treats a missing core as a warning | Owner |
| 2 | Re-author ships whose thrusters overlap (bastion, freighter, beetle, crescent, icarus, striker) | Accept the functional-first drops of §5.4; owner re-authors in the Blueprint Lab later | Owner |
| 3 | Legacy numeric item `500` ("raw materials" in quests) | `resource_palladium` | Owner (with SPEC-42) |
| 4 | Module/officer modifiers and paint palette indices are dropped | Drop (inert items); SPEC-50/51 decide whether modules return | Owner |
| 5 | `ShipBlueprintDefinition` field names | `displayName`, `description`, `structureData` (byte[] VOXS), `basePrice` — use SPEC-10's names if different | SPEC-10 |
| 6 | Instance cells needing a `Cell.Flags` bit | 0 unless SPEC-10 defines one; then MIG uses SPEC-10's helper | SPEC-10 |
| 7 | Weapon unit conversions (legacy units × 0.5 m) | Keep; SPEC-21 retunes | SPEC-21 |
| 8 | Should `loot_creative` exist | Yes, for Creative mode / Blueprint Lab testing; never used for new games | SPEC-50 |
| 9 | Narrative beat/quest IDs | Owners decide; suggestion `beat_<legacy encounter id>`, quests keep `quest_*` | SPEC-41 / SPEC-42 |
| 10 | Who owns `BodyTemplateDefinition` | SPEC-30 | SPEC-30 / SPEC-31 |
| 11 | Ship thumbnails from the old game | Not migrated (files not in v2); SPEC-51 renders new ones | SPEC-51 |
| 12 | Nonfinite Euler angles and integer-cast overflow for very large finite inputs | **Answer (resolved in MIG-007):** reject NaN/infinity with Orientation.Invalid and infinite error; reduce finite angles modulo 360 in double precision before rounding to quarters, preserving the specified one-degree tolerance and rotation order | Tech lead |
| 13 | Manifest link updates can adopt hand edits; report Created vs Unchanged conflicts with first-run byte equality; SerializedObject cannot intercept missing properties | **Answer (resolved in MIG-004):** refresh hashes at owned/forced writes only, including owned link fills; never adopt protected bytes through a link. Unsaved dirty assets are protected too. Invalid manifests fail closed. Compare report bytes across unchanged runs; first-run Created remains truthful. Converter fill callbacks use internal writer.Set to warn and skip individual missing fields. | Tech lead |
| 14 | Hard-coded placeholder art paths conflict with AGENTS; the dropped-data table omits socket fields and some normalized categories | **Answer (resolved in MIG-005):** Editor-only LegacyMigrationSources holds the icon-sheet and (MIG-006) prop-model references, so converters depend on that asset and sprite names, not placeholder paths. Keep existing source art. Report every dropped nonempty RequiredSocketType, Modifiers, non-None EffectType, PaletteIndex and changed legacy Category, including omissions in the abbreviated table. | Tech lead |
| 15 | How should regenerated SerializeReference modules retain byte-stable identities? | **Answer (resolved in MIG-006):** before assigning a module through SerializedProperty, register its positive BlockModuleId as the Unity managed-reference ID on that asset (Unity rejects changes after first serialization). Module IDs are unique per block (SPEC-04), stable and append-only; verify real re-import bytes and manifests. | Tech lead |
| 16 | M0 imports blueprints, but ShipBlueprintDefinition.Validate is a VOX-010 (M1) stub | **Answer (resolved in MIG-008):** bring the already specified VOX-E001/E002/E003 validator forward into MIG-008; retain setter/config work and its tests in VOX-010. Game.Data checks the supported v1 asset contract without referencing Game.Voxel; migration verifies that VoxsCodec.Version is accepted. Finite positive cell size also rejects NaN/infinity. | Tech lead |
| 17 | Imported ships turn the previously deferred FND-E001 start-blueprint check into an error | **Answer (resolved in MIG-008):** assign ship_base to an empty shipped config_game.startBlueprint once for development. It is the smallest imported blueprint (four cells) with a core. Preserve any existing assignment; the migration runner does not rewrite the config. Frank can choose the eventual starting ship when gameplay is available. | Tech lead |

| 19 | MIG-009 depends on all SPEC-04 tasks, but DATA-010 strict link validation needs the missing migrated weapons first | **Answer (resolved, Tech lead):** depend on completed DATA-007 plus MIG-006/MIG-011/WPN-001. WeaponMountModule.weapon, its bake lookup and WeaponDefinition already exist from FND-020, so migration needs no remaining DATA-010 behavior. Supply canonical mapped assets/links before integrating strict E047; never waive missing links or invent weapon definitions. | Tech lead |

## 11. References

- Legacy export: `Import/Legacy/content/**` (155 files), `Import/Legacy/narrative/*.json`.
- Audit background: `docs/reference/audit-2026-10/00-synthesis.md` §7 (what to keep: 69 RegistryKeys, ship templates); `01-voxel-core.md` B1, §6.6, §6.9; `04-persistence-data.md` §6.2, §6.9; `05-narrative-quests.md` B2, §4, §5.13; `06-ui-shipeditor.md` C3, C13.
- Unity: `SerializedObject`, `EditorUtility.CopySerialized`, `TypeCache`, `Quaternion.Euler` (applies Z, then X, then Y), OBJ importer (negates X).
