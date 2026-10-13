# SPEC-30 — Galaxy generation and coordinate frames

| | |
|---|---|
| **Status** | Draft |
| **Area code** | GAL (task IDs are `GAL-001`, `GAL-002`, …) |
| **Module(s)** | `Game.Galaxy` (folder `Assets/Game/Galaxy/`); world content definitions in `Game.Data` (folder `Assets/Game/Data/Definitions/World/`); one bridge system in `Game.Presentation` (folder `Assets/Game/Presentation/World/`); content assets in `Assets/Game/Content/Galaxy/` and `Assets/Game/Content/Bodies/` |
| **Depends on specs** | SPEC-02 (`StableHash`, `RngStreams`, `SimClock`, `EventBus<T>`, `ISaveParticipant`, `SaveWriter`/`SaveReader`, `GameInitGroup`, `GameplayActive`, `ContentHash`, `PersistentId`), SPEC-04 (`ContentDefinition`, `ContentCatalog`, validation API), SPEC-10 (`ShipBlueprintDefinition`, `VoxelStructure`, `VoxelGrid`, `GridSpawnRequest`), SPEC-12 (`PhysicsLayers`, graphical smoothing), SPEC-40 (`CompiledCondition`, `ILogicSourceOwner`, `ILogicContext`), SPEC-01 (`GameSession` holds `GalaxyState`; service construction) |
| **Depended on by** | SPEC-31 (content, deltas, frames), SPEC-32 (lane graph, frames), SPEC-33 (star-system seeds, danger), SPEC-05 (`GLXY` chunk), SPEC-42 (resolve star systems/bodies, reveal), SPEC-61 (galaxy map model), SPEC-62/63 (`OriginShifted`), SPEC-01 (new game / restore entry points) |
| **Milestone** | M3 |

> Read [01-architecture.md](../01-architecture.md), [02-conventions.md](../02-conventions.md), [03-glossary.md](../03-glossary.md) and [07-implementation-traps.md](../07-implementation-traps.md) §5 before this spec. This spec may not contradict them.

## 1. Purpose

The galaxy is pure data computed from `(GalaxySeed, GeneratorVersion)` plus the world content definitions: a set of star systems at galaxy-frame positions connected by lanes, some of them authored (the start star system, story star systems) and the rest procedural. This spec defines that data (`GalaxyDef`), the per-star-system body list (`StarSystemContent`) generated on demand from a star-system seed, the saved changes to it (`StarSystemDelta`, applied strictly after generation), the three coordinate frames, and `OriginRebaseSystem`, the single owner of the floating origin. It also gives the UI a read-only galaxy map model. It prevents the old prototype's "each star system generated twice, differently" (R9, audit 03-B1/D3), IDs that drift because they were derived from rolls (03-D3), seeds never assigned (04-B11), and ad-hoc origin shifting that left belts, the camera and debris behind (03-B9a–d).

## 2. Scope

**In scope:**
- IDs: `StarSystemId`, `BodyId`, `BodyRole`; seeds and sub-seeds per purpose.
- Galaxy layout (positions, authored overrides merged), lane graph (with fact-gated lanes), galaxy hash.
- Star-system content generation (star, planets, moons, belts, stations, points of interest, lane gates) as data, and its hash.
- `StarSystemDelta` model, the generate-then-apply-deltas order, `GalaxyState` and the `GLXY` save chunk.
- Coordinate frames, `UniversePosition`, `FrameOrigin`, `OriginShifted`, `OriginRebaseSystem` (threshold 2,000 m and on star-system entry), `UniversePosition` sync.
- `GalaxyService` (managed facade), `GalaxyFrameTicker` and the Presentation bridge that ticks Galaxy services once per frame.
- `GalaxyMapModel` (data the galaxy map screen reads).
- World content definitions: `GalaxyGenerationDefinition`, `StarSystemTemplateDefinition`, `AuthoredStarSystemDefinition`, `BodyTemplateDefinition`, `BodyVisualDefinition`, and their validator.
- Regression R9 (EditMode part) and the world perf gates "galaxy generation ≤ 50 ms" and "origin rebase ≤ 0.5 ms".

**Out of scope (and who owns it instead):**
- Creating entities for bodies, streaming tiers, orbits at runtime, interaction, asteroid belts, the R9 PlayMode check, M3 world content assets → SPEC-31.
- Jumps, lane fuel cost, lane-open evaluation (`ILaneAccess` implementation), routes, supercruise → SPEC-32.
- Factions, territory, encounters (they read `StarSystemDef.Danger` and seeds) → SPEC-33.
- The save container, slots and participant registration → SPEC-05; `GameSession`, `PersistentId` allocation, new-game/load orchestration → SPEC-01.
- Camera, trail and VFX reaction to `OriginShifted` → SPEC-62, SPEC-63.
- Galaxy map and system map screens (views) → SPEC-61. Rumor markers on the map → SPEC-42.
- `CompiledCondition`, `ILogicContext`, fact names → SPEC-40 / SPEC-42.

## 3. Decisions (binding)

1. **The galaxy is never saved.** `GalaxyDef` = f(`GalaxySeed`, `GeneratorVersion`, world definitions). Only `GalaxyState` (seed, version, deltas, knowledge, player location) is saved. Reason: deltas are small and survive content additions (fixes 04-B1).
2. **`GeneratorVersion` freezes output.** Current value `GalaxyConstants.CurrentGeneratorVersion = 1`. Any change to generation output (algorithm, constant, RNG call order) bumps the version and keeps the previous code path selectable through `switch (generatorVersion)`. A save is always regenerated with the version it was created with. Reason: deltas are keyed by structural IDs that must keep pointing at the same things.
3. **All randomness comes from `RngStreams.For(seed, purpose, salt)`** (SPEC-02, SplitMix64-based) with one `RngPurpose` per concern (§4.1). Seeds are combined only with `StableHash.Combine`. No `UnityEngine.Random`, `System.Random`, `GetHashCode`, `DateTime` or `Guid` anywhere in this spec's code. Reason: purpose separation means adding a loot roll cannot move a planet.
4. **IDs are structural.** Procedural `StarSystemId.Value = 1 + acceptance index`; authored `StarSystemId.Value = 0x80000000 | (ContentHash.Value & 0x7FFFFFFF)` (if the low 31 bits are 0, use 1). `BodyId = (StarSystemId, BodyRole, Slot, SubSlot)` where Slot/SubSlot are generation positions (orbit index, moon index, lane index), never rolled values. Reason: a destroyed body can never shift another body's identity (03-D3).
5. **Counts come from one layout stream; each body's attributes come from its own stream** seeded by its `BodyId.LocalKey`. A stream always consumes the same number of draws regardless of earlier outcomes (draw first, then decide). RNG is never consumed conditionally on saved state. Reason: determinism under deltas.
6. **Generate, then apply deltas.** Content generation never reads `StarSystemDelta`. `StarSystemDeltaApplier` runs afterwards in a fixed order (§5.6). Reason: R9 / 03-D3.
7. **Three frames** (architecture §9): galaxy `double2` light-years; star system `double3` metres relative to the star; local `float3` relative to `FrameOrigin`. Galaxy-frame direction `(x, y)` maps to star-system direction `(x, 0, y)` (galaxy +y = star-system +Z). Ships fly on the star-system XZ plane at Y = 0.
8. **`OriginRebaseSystem` is the only code that moves the origin or shifts root transforms.** It rebases when the player ship's local position length exceeds `GalaxyConstants.OriginRebaseThresholdM = 2000` and whenever an `OriginRebaseRequest` is enabled (star-system entry, new game, load). For roots with `UniversePosition` the new local position is recomputed in double precision from `UniversePosition − newOrigin` (no float error accumulates); roots without it are shifted by the float delta.
9. **`UniversePosition` is synced from the physics transform once per frame** by `OriginRebaseSystem` before any rebase decision: `UniversePosition = FrameOrigin + (double3)LocalTransform.Position` for every root that has both. Nobody else writes `UniversePosition` after an entity exists (creators set the initial value).
10. **Generation runs in Burst.** Galaxy layout, lanes and star-system content are `IJob`s over blittable parameter arrays. Managed code only extracts parameters from definitions before and resolves names after.
11. **Exactly one galaxy generation per new game or load** (`GalaxyService.GenerationCount` increments once). Star-system content is generated at most once per load request and cached until released. Reason: R9.
12. **Deltas are sorted or append-only** (Destroyed, Interacted, Replacements, StructureDiffs sorted by body key ascending; StorySpawns append-only, removal only marks), so save → load → save is byte-identical.
13. **Spawned things are referenced by `PersistentId`, generated things by `BodyId`, never by `Entity`** (architecture §6.1). Story spawns are generated content (`BodyRole.StorySpawn`, slot = record index); runtime grids of bodies get a fresh `PersistentId` per spawn that is never saved.
14. **Galaxy events follow the SPEC-02 §5.6 bridge rules.** Payload structs live in `Assets/Game/Core/Events/` (namespace `Game.Core.Events`) and use only Core-visible types, so a star system is carried as `uint StarSystemValue` (= `StarSystemId.Value`). ECS systems in Galaxy enqueue into `ManagedEventQueue<T>` (drained by `ManagedEventDrainSystem`). Galaxy managed services call `EventBus<T>.Publish` directly. ECS systems in Galaxy never call managed code.
15. **M3 galaxy = authored star systems only** (`proceduralStarSystemCount = 0` in `galaxy_main`), but the generator is tested at 1,000 procedural star systems. Reason: vision M3 scope is 2–4 star systems.

## 4. Data model

### 4.1 Requested additions to other specs' types (defaults used until answered)

| Owner | Addition | Used for |
|---|---|---|
| SPEC-02 `RngPurpose` | Existing values used as: `GalaxyLayout` (1) star-system placement; `StarSystemGeneration` (2) per-star-system attributes; `BodyGeneration` (3) per-body attributes; `AsteroidField` (4) belt sectors; `EncounterDirector` (5) encounter visits. **Appended** (SPEC-02 owns the enum and lists them): `GalaxyLanes = 18`, `StarSystemLayout = 19`, `AsteroidShape = 20`, `StationShape = 21`, `EncounterFleet = 22`, `EncounterPlacement = 23`, `Territory = 24` | All Galaxy randomness (SPEC-30…33) |
| SPEC-04 prefix registry | `GalaxyGenerationDefinition` `galaxy_`; `StarSystemTemplateDefinition` `systpl_`; `AuthoredStarSystemDefinition` `starsys_`; `BodyVisualDefinition` `bodyvis_`; (`BodyTemplateDefinition` `body_` already listed) | Content IDs |
| SPEC-02 / SPEC-01 | `Game.Core.IPersistentIdAllocator { ulong Allocate(); }` (managed) implemented by `GameSession`; Burst systems use the `PersistentIdAllocator` singleton (SPEC-02 §4.1, SPEC-01 §5) | Runtime grids spawned by Galaxy |

### 4.2 IDs and enums (public contract)

Enums used by definitions live in `Game.Data` so `Game.Data` can reference them (architecture §3.2). Folder `Assets/Game/Data/Definitions/World/`, namespace `Game.Data.Definitions`.

```csharp
namespace Game.Data.Definitions
{
    /// <summary>Role of a body inside a star system. Implements SPEC-30 §4.2. Add values at the end only.</summary>
    public enum BodyRole : byte { None = 0, Star = 1, Planet = 2, Moon = 3, Station = 4, Belt = 5, Asteroid = 6, LaneGate = 7, PointOfInterest = 8, StorySpawn = 9 }

    public enum OrbitMode : byte { Fixed = 0, Circular = 1, Elliptic = 2 }                 // add at end only
    public enum LaneKind : byte { Hyperlane = 0, AncientGate = 1 }                         // add at end only
    public enum StarClass : byte { M = 0, K = 1, G = 2, F = 3, A = 4, B = 5, O = 6, WhiteDwarf = 7, NeutronStar = 8, BlackHole = 9 }
    public enum InteractionKind : byte { None = 0, Dock = 1, Inspect = 2, LaneGate = 3, Hail = 4, Salvage = 5 } // add at end only
    public enum StructureSource : byte { None = 0, Blueprint = 1, ProceduralShape = 2 }
    public enum ShapeKind : byte { Ring = 0, Cylinder = 1, Box = 2, Sphere = 3 }
}
```

Folder `Assets/Game/Galaxy/Data/`, namespace `Game.Galaxy.Data`:

```csharp
/// <summary>Star-system identity (architecture §6.5). Implements SPEC-30 §4.2.</summary>
public readonly struct StarSystemId : System.IEquatable<StarSystemId>, System.IComparable<StarSystemId>
{
    public readonly uint Value;                                   // 0 = None
    public const uint AuthoredBit = 0x80000000u;
    public StarSystemId(uint value) { Value = value; }
    public static StarSystemId None => default;
    public bool IsValid => Value != 0;
    public bool IsAuthored => (Value & AuthoredBit) != 0;
    public static StarSystemId FromProceduralIndex(int index) => new StarSystemId((uint)index + 1u);  // index 0 .. 0x7FFFFFFD
    public static StarSystemId FromAuthored(Game.Core.ContentHash hash)
    { uint low = (uint)(hash.Value & 0x7FFFFFFFul); return new StarSystemId(AuthoredBit | (low == 0u ? 1u : low)); }
    public bool Equals(StarSystemId o) => Value == o.Value;
    public override bool Equals(object o) => o is StarSystemId s && s.Value == Value;
    public override int GetHashCode() => (int)Value;              // dictionary use only; never persisted
    public int CompareTo(StarSystemId o) => Value.CompareTo(o.Value);
    public override string ToString() => "ss:" + Value.ToString("X8");
}

/// <summary>Structural body identity (architecture §6.5). LocalKey is unique inside one star system.</summary>
public readonly struct BodyId : System.IEquatable<BodyId>
{
    public readonly StarSystemId System;
    public readonly Game.Data.Definitions.BodyRole Role;
    public readonly ushort Slot, SubSlot;
    public BodyId(StarSystemId system, Game.Data.Definitions.BodyRole role, ushort slot, ushort subSlot)
    { System = system; Role = role; Slot = slot; SubSlot = subSlot; }
    public bool IsValid => System.IsValid && Role != Game.Data.Definitions.BodyRole.None;
    /// <summary>(Role &lt;&lt; 32) | (Slot &lt;&lt; 16) | SubSlot. Saved in deltas. Sort order = generation order.</summary>
    public ulong LocalKey => ((ulong)Role << 32) | ((ulong)Slot << 16) | SubSlot;
    public static BodyId FromLocalKey(StarSystemId system, ulong key)
        => new BodyId(system, (Game.Data.Definitions.BodyRole)(byte)(key >> 32), (ushort)(key >> 16), (ushort)key);
    public ulong StableKey => Game.Core.StableHash.Combine(System.Value, LocalKey);  // seeds and hashing
    public bool Equals(BodyId o) => System.Value == o.System.Value && LocalKey == o.LocalKey;
    public override bool Equals(object o) => o is BodyId b && Equals(b);
    public override int GetHashCode() => (int)(System.Value * 31u) ^ (int)LocalKey;    // dictionary use only
    public override string ToString() => System + "/" + Role + ":" + Slot + "." + SubSlot;
}

[System.Flags] public enum StarSystemFlags : ushort { None = 0, Authored = 1, Start = 2, Story = 4, HiddenUntilKnown = 8, NoEncounters = 16, Hub = 32 }
[System.Flags] public enum LaneFlags : byte { None = 0, Authored = 1, HiddenUntilKnown = 2 }
[System.Flags] public enum BodyStateFlags : byte { None = 0, Destroyed = 1, Interacted = 2, Replaced = 4, StorySpawn = 8, HasStructure = 16 }

public static class GalaxyConstants
{
    public const float OriginRebaseThresholdM = 2000f;   // architecture §9 (binding, not tunable)
    public const int CurrentGeneratorVersion = 1;
    public const ushort NoIndex = 0xFFFF;                 // "no template / no lane" in ushort index fields
    public const int MaxAuthoredStarSystems = 256;
    public const int MaxProceduralStarSystems = 100000;
    public const int MaxBodiesPerStarSystem = 512;
}
```

`StarSystemId` and `BodyId` are unmanaged and may appear in components, buffers, jobs and ECS events.

### 4.3 World content definitions (public contract)

All in `Assets/Game/Data/Definitions/World/`, namespace `Game.Data.Definitions`, one public type per file. **Property rule:** every `[SerializeField] private` field below has a read-only public property with the PascalCase name (`radiusMinM` → `RadiusMinM`); lists are exposed as `IReadOnlyList<T>`. `Validate` overrides call `base.Validate` first and report the codes of §5.9. Lane conditions are SPEC-40's `Game.Data.Logic.CompiledCondition` (§10 Q5). `GalaxyGenerationDefinition` implements `Game.Data.Logic.ILogicSourceOwner`; its `CompileLogic`/`BindLogic` bodies are written by SPEC-32 TRV-003 (SPEC-32 §5.1).

```csharp
[System.Serializable] public struct WeightedBodyTemplate
{ public BodyTemplateDefinition template; public float weight; public int maxCount; }   // maxCount 0 = unlimited

[System.Serializable] public struct WeightedStarSystemTemplate
{ public StarSystemTemplateDefinition template; public float weight; }

[System.Serializable] public struct ProceduralShapeParams
{
    public ShapeKind kind;
    public UnityEngine.Vector3Int sizeCells;   // Ring: x = outer diameter, y = height, z = inner diameter; Cylinder: x = diameter, y = height; Box: x,y,z; Sphere: x = diameter
    public int wallThicknessCells;             // ≥ 1; 0 = solid
    public BlockDefinition hullBlock;          // structural block filling the shell
    public BlockDefinition coreBlock;          // functional Core block placed once (required when destructible)
    public byte colorIndex;                    // palette colour (SPEC-10 PaletteEntry.Color)
}

[System.Serializable] public sealed class AuthoredLane
{
    public AuthoredStarSystemDefinition a, b;
    public LaneKind kind = LaneKind.Hyperlane;
    public Game.Data.Logic.CompiledCondition condition = new Game.Data.Logic.CompiledCondition();  // empty source = always open
    [UnityEngine.TextArea(1, 3)] public string lockedMessage;   // shown on the map while closed
    public BodyTemplateDefinition gateTemplateA, gateTemplateB; // null = galaxy default for the kind
    public bool hiddenUntilKnown;
}

[System.Serializable] public sealed class AuthoredBody
{
    public string key;                      // snake_case, unique in the star system, e.g. "ark_station"
    public BodyRole role;                   // Planet, Moon, Station, Belt or PointOfInterest
    public ushort slot;                     // unique per role in the star system
    public string parentKey;                // Moon: key of its planet; others: ""
    public BodyTemplateDefinition template;
    public OrbitMode mode = OrbitMode.Circular;   // Station/PointOfInterest/Belt must be Fixed
    public float orbitRadiusM;              // distance from parent (Belt: centre radius of the ring)
    public float phaseDegrees;              // angle at UniversalTime 0, measured from +X toward +Z
    public float eccentricity;              // Elliptic only, 0 .. 0.3
    public float periodOverrideS;           // 0 = Kepler-lite from the template (§5.5)
    public float radiusOverrideM;           // 0 = rolled from the body template range
    public float yawDegrees;                // Fixed bodies only
    public string displayNameOverride;      // "" = template display name
}

[ContentIdPrefix("galaxy_")]
[UnityEngine.CreateAssetMenu(menuName = "Game/Content/World/Galaxy Generation", fileName = "galaxy_new")]
public sealed class GalaxyGenerationDefinition : ContentDefinition
{
    [SerializeField] private int generatorVersion = 1;            // must equal GalaxyConstants.CurrentGeneratorVersion
    [SerializeField] private bool useFixedSeed = true;            // M3: every new game uses fixedSeed
    [SerializeField] private ulong fixedSeed = 0x5EED0001ul;
    [SerializeField] private float galaxyRadiusLy = 60f;
    [SerializeField] private int proceduralStarSystemCount = 0;   // M3 content: 0. Range 0..100000
    [SerializeField] private float minStarSystemSpacingLy = 3f;
    [SerializeField] private int placementAttemptsPerStarSystem = 30;
    [SerializeField] private int laneCandidateNeighbours = 6;
    [SerializeField] private int maxLaneDegree = 4;
    [SerializeField] private float maxLaneLengthLy = 12f;
    [SerializeField, UnityEngine.Range(0f, 1f)] private float extraLaneChance = 0.35f;
    [SerializeField] private System.Collections.Generic.List<WeightedStarSystemTemplate> templatePool = new();
    [SerializeField] private System.Collections.Generic.List<AuthoredStarSystemDefinition> authoredStarSystems = new();
    [SerializeField] private AuthoredStarSystemDefinition startStarSystem;
    [SerializeField] private System.Collections.Generic.List<AuthoredLane> authoredLanes = new();
    [SerializeField] private BodyTemplateDefinition defaultHyperlaneGate;
    [SerializeField] private BodyTemplateDefinition defaultAncientGate;
}

[ContentIdPrefix("systpl_")]
[UnityEngine.CreateAssetMenu(menuName = "Game/Content/World/Star System Template", fileName = "systpl_new")]
public sealed class StarSystemTemplateDefinition : ContentDefinition
{
    [SerializeField] private string namePrefix = "Uncharted";      // procedural display name prefix (§5.7)
    [SerializeField] private System.Collections.Generic.List<WeightedBodyTemplate> starPool = new();
    [SerializeField] private int minPlanets = 2, maxPlanets = 4;
    [SerializeField] private System.Collections.Generic.List<WeightedBodyTemplate> planetPool = new();
    [SerializeField] private int maxMoonsPerPlanet = 2;
    [SerializeField, UnityEngine.Range(0f, 1f)] private float moonChance = 0.5f;
    [SerializeField] private System.Collections.Generic.List<WeightedBodyTemplate> moonPool = new();
    [SerializeField] private int minBelts = 0, maxBelts = 1;
    [SerializeField] private System.Collections.Generic.List<WeightedBodyTemplate> beltPool = new();
    [SerializeField] private int minStations = 0, maxStations = 1;
    [SerializeField] private System.Collections.Generic.List<WeightedBodyTemplate> stationPool = new();
    [SerializeField] private int minPointsOfInterest = 1, maxPointsOfInterest = 3;
    [SerializeField] private System.Collections.Generic.List<WeightedBodyTemplate> pointOfInterestPool = new();
    [SerializeField] private float firstOrbitRadiusM = 3000f;
    [SerializeField] private float orbitSpacingMinM = 2000f, orbitSpacingMaxM = 3000f;
    [SerializeField] private double orbitPeriodAtReferenceS = 3600.0;   // Kepler-lite: T = Tref·(a/aref)^1.5
    [SerializeField] private float orbitReferenceRadiusM = 4000f;
    [SerializeField] private float moonOrbitMinM = 600f, moonOrbitMaxM = 1500f;   // added to the planet radius
    [SerializeField] private double moonPeriodAtReferenceS = 600.0;
    [SerializeField] private float moonReferenceRadiusM = 1000f;
    [SerializeField] private float stationRadiusMinM = 2500f, stationRadiusMaxM = 9000f;
    [SerializeField] private float pointOfInterestRadiusMinM = 2000f, pointOfInterestRadiusMaxM = 12000f;
    [SerializeField] private float minBodySeparationM = 1500f;
    [SerializeField] private float laneRingMarginM = 3000f;
    [SerializeField] private float minLaneRingRadiusM = 8000f;
    [SerializeField] private float extentMarginM = 2000f;              // ExtentM = lane ring radius + this
    [SerializeField] private int dangerMin = 0, dangerMax = 1;          // 0..3
}

[ContentIdPrefix("starsys_")]
[UnityEngine.CreateAssetMenu(menuName = "Game/Content/World/Authored Star System", fileName = "starsys_new")]
public sealed class AuthoredStarSystemDefinition : ContentDefinition
{
    [SerializeField] private string displayName;
    [SerializeField] private UnityEngine.Vector2 positionLy;           // galaxy frame
    [SerializeField] private StarSystemTemplateDefinition template;    // supplies orbit/period/lane-ring parameters
    [SerializeField] private BodyTemplateDefinition star;
    [SerializeField] private System.Collections.Generic.List<AuthoredBody> bodies = new();
    [SerializeField] private bool hiddenUntilKnown, isStory, isHub, noEncounters;
    [SerializeField] private bool connectProcedurally;                  // joins the procedural lane graph
    [SerializeField, UnityEngine.Range(0, 3)] private int danger;
    [SerializeField] private string startBodyKey;                       // start star system only: spawn near this body
    [SerializeField] private UnityEngine.Vector3 startOffsetM = new UnityEngine.Vector3(0f, 0f, -400f); // from that body at UniversalTime 0
    [SerializeField] private float startYawDegrees;
}

[ContentIdPrefix("body_")]
[UnityEngine.CreateAssetMenu(menuName = "Game/Content/World/Body Template", fileName = "body_new")]
public sealed class BodyTemplateDefinition : ContentDefinition
{
    [SerializeField] private string displayName;
    [SerializeField, UnityEngine.TextArea(2, 5)] private string description;
    [SerializeField] private BodyRole role;
    [SerializeField] private float radiusMinM = 100f, radiusMaxM = 100f;  // visual/collider radius (celestials) or bounding radius before a grid exists
    [SerializeField] private BodyVisualDefinition visual;
    [SerializeField] private StarClass starClass;                          // Star only
    [SerializeField] private bool hasCollider = true;                      // Star/Planet/Moon: static sphere collider
    [SerializeField] private bool canHaveMoons;                            // Planet only
    [SerializeField] private InteractionKind interaction;
    [SerializeField] private float interactionDistanceM = 150f;           // measured to the collider surface (SPEC-31 R10)
    [SerializeField] private string interactPrompt;                        // e.g. "Dock"; "" = none
    [SerializeField] private string storyTag;                              // optional narrative location tag, snake_case
    [SerializeField] private StructureSource structureSource;
    [SerializeField] private ShipBlueprintDefinition blueprint;             // structureSource == Blueprint
    [SerializeField] private ProceduralShapeParams shape;                   // structureSource == ProceduralShape
    [SerializeField] private bool destructible = true;                     // false: SPEC-13 DamageImmune (§10 Q15)
    [SerializeField] private float t2LoadDistanceM = 6000f;                // SPEC-31 tier T2, measured to the bounding sphere
    // Belt only
    [SerializeField] private float beltWidthM = 2000f;
    [SerializeField] private float sectorSizeM = 500f;
    [SerializeField] private int asteroidsPerSectorMin = 2, asteroidsPerSectorMax = 6;   // max ≤ 8
    [SerializeField] private float asteroidRadiusMinM = 4f, asteroidRadiusMaxM = 18f;
    [SerializeField] private BlockDefinition asteroidBlock;
    [SerializeField] private float asteroidNoiseAmplitude = 0.25f, asteroidNoiseFrequency = 0.15f;
}

[ContentIdPrefix("bodyvis_")]
[UnityEngine.CreateAssetMenu(menuName = "Game/Content/World/Body Visual", fileName = "bodyvis_new")]
public sealed class BodyVisualDefinition : ContentDefinition
{
    [SerializeField] private UnityEngine.Mesh mesh;                // placeholder; centred at the origin
    [SerializeField] private float meshRadius = 0.5f;              // radius of `mesh` in mesh units (Unity sphere = 0.5)
    [SerializeField] private UnityEngine.Material material;        // shared placeholder material (never instanced)
    [SerializeField] private UnityEngine.Color baseColor = UnityEngine.Color.gray;  // per-instance material property
    [SerializeField] private UnityEngine.Color emissionColor = UnityEngine.Color.black;
    [SerializeField] private bool castShadows;
    [SerializeField] private bool hideWhenGridPresent = true;      // impostor of a voxel body
    [SerializeField] private UnityEngine.Sprite mapIcon;
    [SerializeField] private string mapColorToken = "--color-map-body";   // USS token for map UI (SPEC-61)
}
```

`baseColor`/`emissionColor` are art data for placeholder materials (per-instance material properties), not UI colours; UI uses `mapColorToken` only (conventions §4.2).

Code blocks above omit `using UnityEngine;` and `using Game.Data.Definitions;`; the files include them.

### 4.4 Galaxy definition (public contract, regenerated, never saved)

Folder `Assets/Game/Galaxy/Data/`, namespace `Game.Galaxy.Data`.

```csharp
/// <summary>One star system in the galaxy frame. Blittable. Implements SPEC-30 §4.4.</summary>
public struct StarSystemDef
{
    public StarSystemId Id;
    public Unity.Mathematics.double2 PositionLy;
    public ulong Seed;                 // StableHash.Combine(galaxySeed, Id.Value)
    public ushort TemplateIndex;       // index into GalaxyDef.Templates; NoIndex for authored
    public ushort AuthoredIndex;       // 0 = procedural; else 1 + index into GalaxyDef.Authored
    public ushort StarTemplateIndex;   // index into GalaxyDef.BodyTemplates (rolled or authored star)
    public StarSystemFlags Flags;
    public byte Danger;                // 0..3
    public int FirstAdjacency;         // first index into GalaxyDef.Adjacency
    public ushort LaneCount;           // number of adjacency entries
}

/// <summary>A lane between two star systems. A.Value &lt; B.Value always.</summary>
public struct LaneDef
{
    public StarSystemId A, B;
    public float LengthLy;
    public Game.Data.Definitions.LaneKind Kind;
    public LaneFlags Flags;
    public ushort GateIndex;           // 0 = ungated; else 1 + index into GalaxyDef.LaneGates
    public ushort GateTemplateA;       // body template index of the gate in A (GalaxyDef.BodyTemplates)
    public ushort GateTemplateB;       // body template index of the gate in B
}

/// <summary>Managed lane gate condition (only authored lanes are gated in v1).</summary>
public sealed class LaneGateSpec
{
    public Game.Data.Logic.CompiledCondition Condition;   // the authored lane's object (not copied); bound at boot by SPEC-40
    public string LockedMessage;
}

/// <summary>The generated galaxy. Owns native memory (Allocator.Persistent); disposed by GalaxyService.</summary>
public sealed class GalaxyDef : System.IDisposable
{
    public ulong Seed { get; }
    public int GeneratorVersion { get; }
    public ulong Hash { get; }                                       // GalaxyHash.Compute, §5.4
    public Unity.Collections.NativeArray<StarSystemDef> StarSystems; // sorted by Id.Value ascending
    public Unity.Collections.NativeArray<LaneDef> Lanes;             // sorted by (A.Value, B.Value)
    public Unity.Collections.NativeArray<int> Adjacency;             // lane indices; per star system sorted by neighbour Id.Value
    public Unity.Collections.NativeHashMap<uint, int> IndexById;     // StarSystemId.Value → index in StarSystems
    public Unity.Collections.NativeArray<BodyTemplateParams> BodyTemplateParams; // index = body template index
    public Unity.Collections.NativeArray<AuthoredBodyParams> AuthoredBodies;     // all authored bodies, grouped
    public Unity.Collections.NativeArray<int2> AuthoredBodyRanges;   // per authored star system: (start, count) into AuthoredBodies
    public Unity.Collections.NativeArray<StarSystemTemplateParams> TemplateParams; // index = TemplateIndex; authored systems use AuthoredTemplateParams
    public Unity.Collections.NativeArray<StarSystemTemplateParams> AuthoredTemplateParams; // index = AuthoredIndex - 1
    public System.Collections.Generic.IReadOnlyList<StarSystemTemplateDefinition> Templates { get; }   // templatePool order
    public System.Collections.Generic.IReadOnlyList<AuthoredStarSystemDefinition> Authored { get; }     // sorted by ID (ordinal)
    public System.Collections.Generic.IReadOnlyList<LaneGateSpec> LaneGates { get; }
    public System.Collections.Generic.IReadOnlyList<BodyTemplateDefinition> BodyTemplates { get; }      // catalog.All<BodyTemplateDefinition>() order
    public StarSystemId StartStarSystem { get; }

    public bool TryGetIndex(StarSystemId id, out int index);
    public ref readonly StarSystemDef Get(StarSystemId id);          // KeyNotFoundException if absent
    public int LaneCountOf(StarSystemId id);
    public int LaneIndexOf(StarSystemId id, int adjacencySlot);      // 0 ≤ adjacencySlot < LaneCount
    public static StarSystemId Neighbour(in LaneDef lane, StarSystemId self); // the other end
    public bool TryFindLane(StarSystemId a, StarSystemId b, out int laneIndex);
    public ushort BodyTemplateIndexOf(BodyTemplateDefinition def);  // NoIndex if not in BodyTemplates
    public void Dispose();                                           // safe to call twice
}
```

Blittable parameter structs (copied from definitions by `GalaxyDefBuilder`, §5.1; same folder):

```csharp
public struct WeightedIndex { public ushort Index; public float Weight; public int MaxCount; }   // Index into BodyTemplates

public struct StarSystemTemplateParams
{
    public int MinPlanets, MaxPlanets, MaxMoonsPerPlanet, MinBelts, MaxBelts, MinStations, MaxStations, MinPois, MaxPois, DangerMin, DangerMax;
    public float MoonChance, FirstOrbitRadiusM, OrbitSpacingMinM, OrbitSpacingMaxM, OrbitReferenceRadiusM,
                 MoonOrbitMinM, MoonOrbitMaxM, MoonReferenceRadiusM, StationRadiusMinM, StationRadiusMaxM,
                 PoiRadiusMinM, PoiRadiusMaxM, MinBodySeparationM, LaneRingMarginM, MinLaneRingRadiusM, ExtentMarginM;
    public double OrbitPeriodAtReferenceS, MoonPeriodAtReferenceS;
    public int2 StarPool, PlanetPool, MoonPool, BeltPool, StationPool, PoiPool;  // (start, count) into GalaxyDef.PoolEntries
}
// GalaxyDef also has: public NativeArray<WeightedIndex> PoolEntries;  (all pools of all templates, concatenated)

public struct BodyTemplateParams
{
    public Game.Data.Definitions.BodyRole Role;
    public float RadiusMinM, RadiusMaxM, BeltWidthM;
    public byte CanHaveMoons;              // 0/1
}

public struct AuthoredBodyParams
{
    public ulong LocalKey;                 // BodyId(role, slot, subSlot) of this entry; Moon SubSlot = index among the planet's moons in list order
    public ulong ParentKey;                // 0 = star
    public ushort TemplateIndex;
    public Game.Data.Definitions.OrbitMode Mode;
    public float OrbitRadiusM, PhaseRad, Eccentricity, PeriodOverrideS, RadiusOverrideM, YawRad;
}
```

### 4.5 Star-system content (public contract, regenerated, never saved)

```csharp
public struct OrbitElements
{
    public Game.Data.Definitions.OrbitMode Mode;
    public double RadiusM;          // Fixed: distance from parent; Circular/Elliptic: semi-major axis
    public double Eccentricity;     // Elliptic only, 0 .. 0.3
    public double PeriodS;          // Circular/Elliptic, > 0
    public double PhaseRad;         // angle (Fixed, Circular) or mean anomaly (Elliptic) at UniversalTime 0
    public double ArgPeriapsisRad;  // Elliptic only
}

public struct BodySpec
{
    public BodyId Id;
    public ulong ParentKey;         // 0 = star-system root; Moon: LocalKey of its planet; StorySpawn: anchor key or 0
    public ushort TemplateIndex;    // GalaxyDef.BodyTemplates (after replacements)
    public ushort LaneIndex;        // LaneGate: index into GalaxyDef.Lanes; else NoIndex
    public OrbitElements Orbit;
    public OrbitElements ParentOrbit; // copy of the parent's orbit (Moon/StorySpawn with parent), Mode Fixed + Radius 0 otherwise
    public float RadiusM;           // celestial radius or voxel-body bounding radius
    public float YawRad;
    public float BeltInnerRadiusM, BeltOuterRadiusM;  // Belt only (ring around the star), else 0
    public ulong Seed;              // StableHash.Combine(StarSystemDef.Seed, Id.LocalKey)
    public BodyStateFlags State;
}

/// <summary>Bodies of one star system: generated (§5.5), then deltas applied (§5.6). Owns native memory.</summary>
public sealed class StarSystemContent : System.IDisposable
{
    public StarSystemId Id { get; }
    public Unity.Collections.NativeList<BodySpec> Bodies;   // Allocator.Persistent, sorted by Id.LocalKey ascending
    public double LaneRingRadiusM { get; internal set; }
    public double ExtentM { get; internal set; }
    public ulong GeneratedHash { get; internal set; }        // StarSystemContentHash over Bodies before deltas
    public bool TryFind(ulong localKey, out int index);       // binary search
    public void Dispose();
}

public static class OrbitMath   // Burst-compatible static methods
{
    public static double3 LocalPosition(in OrbitElements o, double t);   // relative to parent, Y = 0
    public static double3 LocalVelocity(in OrbitElements o, double t);
    public static double3 Position(in BodySpec b, double t);             // star-system frame: parent + local
    public static double3 Velocity(in BodySpec b, double t);
}
```

### 4.6 Deltas and galaxy state (public contract, saved)

```csharp
public struct StorySpawnRecord                 // managed (ContentId); never in ECS data
{
    public Game.Core.ContentId Template;       // body template ID
    public ulong AnchorBodyKey;                // 0 = position relative to the star
    public Unity.Mathematics.double3 OffsetM;  // from the anchor's position (XZ used; Y ignored)
    public float YawRad;
    public bool Removed;                       // removal keeps the index (slot stability)
}
public struct ReplacementRecord { public ulong BodyKey; public Game.Core.ContentId Template; }

public sealed class StructureDiffRecord : System.IDisposable
{
    public ulong BodyKey;
    public uint Revision;                      // VoxelGrid.Revision when captured
    public Game.Voxel.VoxelStructure Structure; // owned; full structure in M3 (brick diffs: GAL-016, M4)
    public void Dispose();
}

/// <summary>Saved changes to one generated star system. Implements SPEC-30 §4.6.</summary>
public sealed class StarSystemDelta : System.IDisposable
{
    public StarSystemDelta(StarSystemId id);
    public StarSystemId StarSystem { get; }
    public uint VisitCount { get; }
    public System.Collections.Generic.IReadOnlyList<ulong> Destroyed { get; }          // sorted ascending, unique
    public System.Collections.Generic.IReadOnlyList<ulong> Interacted { get; }         // sorted ascending, unique
    public System.Collections.Generic.IReadOnlyList<StorySpawnRecord> StorySpawns { get; } // append-only; index = slot
    public System.Collections.Generic.IReadOnlyList<ReplacementRecord> Replacements { get; } // sorted by BodyKey, unique
    public System.Collections.Generic.IReadOnlyList<StructureDiffRecord> StructureDiffs { get; } // sorted by BodyKey, unique

    public void IncrementVisit();
    public bool MarkDestroyed(ulong bodyKey);              // false if already present
    public bool MarkInteracted(ulong bodyKey);
    public int AddStorySpawn(in StorySpawnRecord r);       // returns slot; throws when Count == 65535
    public bool RemoveStorySpawn(int slot);                // sets Removed; false if already removed or out of range
    public void SetReplacement(ulong bodyKey, Game.Core.ContentId template);   // insert or overwrite
    public void SetStructure(ulong bodyKey, uint revision, Game.Voxel.VoxelStructure s); // takes ownership; disposes the previous one
    public bool RemoveStructure(ulong bodyKey);            // disposes it
    public bool TryGetStructure(ulong bodyKey, out StructureDiffRecord r);
    public bool IsEmpty { get; }                           // VisitCount == 0 and all lists empty
    public void Dispose();                                 // disposes structures
}

public struct PlayerLocation
{
    public StarSystemId StarSystem;
    public Unity.Mathematics.double3 PositionM;            // star-system frame (UniversePosition)
    public float YawRad;
}

/// <summary>Everything the galaxy saves. Held by GameSession (SPEC-01); restored in place.</summary>
public sealed class GalaxyState : System.IDisposable
{
    public ulong Seed;
    public int GeneratorVersion;
    public StarSystemId CurrentStarSystem;
    public bool HasPlayerLocation;
    public PlayerLocation PlayerLocation;
    public StarSystemId RouteDestination { get; set; }    // written by SPEC-32; the setter increments Revision when the value changes
    public uint Revision { get; }                         // +1 on every AddKnown/AddVisited that returns true, on a RouteDestination change
                                                          // and on Clear (never reset; consumers compare stamps)
    public System.Collections.Generic.IReadOnlyList<uint> Known { get; }    // sorted ascending
    public System.Collections.Generic.IReadOnlyList<uint> Visited { get; }  // sorted ascending
    public bool AddKnown(StarSystemId id); public bool AddVisited(StarSystemId id);
    public bool IsKnown(StarSystemId id); public bool IsVisited(StarSystemId id);
    public StarSystemDelta GetOrCreateDelta(StarSystemId id);
    public bool TryGetDelta(StarSystemId id, out StarSystemDelta d);
    public System.Collections.Generic.IEnumerable<StarSystemDelta> DeltasSorted();   // ascending StarSystemId, skips IsEmpty
    public void Clear();                                  // disposes deltas, resets every field to default
    public void Dispose();
}
```

`Game.Galaxy` may reference `Game.Voxel` (architecture §3), so `VoxelStructure` is allowed in `StructureDiffRecord`; it is managed and never enters ECS data.

### 4.7 ECS components and events (public contract)

Folder `Assets/Game/Galaxy/Components/` (namespace `Game.Galaxy.Components`) and `Assets/Game/Galaxy/Events/` (namespace `Game.Galaxy.Events`; a new folder name, reason: event structs consumed by Presentation and App that are neither components nor services).

```csharp
/// <summary>Star-system-frame position (architecture §6.5). On every persistent root entity. Synced by OriginRebaseSystem.</summary>
public struct UniversePosition : IComponentData { public Unity.Mathematics.double3 Value; }

/// <summary>Singleton. Written only by OriginRebaseSystem.</summary>
public struct FrameOrigin : IComponentData
{
    public Unity.Mathematics.double3 OriginInStarSystem;   // binding (architecture §6.5)
    public StarSystemId StarSystem;                        // star system this frame belongs to; None before the first load
    public uint ShiftSequence;                             // +1 on every rebase (threshold or request)
    public Unity.Mathematics.float3 LastShiftDelta;        // newLocal = oldLocal + delta for the last rebase; zero when it changed star system
    public byte LastShiftChangedStarSystem;                // 1 when the last rebase came from a request with a different star system
    public uint AppliedRequestSerial;                      // Serial of the last OriginRebaseRequest applied
}

/// <summary>Enableable, on the FrameOrigin entity. Written only by GalaxyService.RequestOriginReset; consumed (disabled) by OriginRebaseSystem.</summary>
public struct OriginRebaseRequest : IComponentData, IEnableableComponent
{
    public Unity.Mathematics.double3 NewOrigin;
    public StarSystemId StarSystem;
    public uint Serial;                                    // monotonically increasing, starts at 1
    public byte PlacePlayer;                               // 1: ShiftJob places the PlayerShip root (SPEC-31 Q2, SPEC-32)
    public PlayerPlacement Placement;                      // star-system frame; read only when PlacePlayer == 1
}

/// <summary>Where the player ship is put by a placement request (star-system frame, yaw only, ADR-0003).</summary>
public struct PlayerPlacement { public Unity.Mathematics.double3 PositionM; public float YawRad; }

/// <summary>Shared, low cardinality (≤ 2 values). On every star-system-owned entity: grid roots, bricks, functional entities, body anchors. Never on the player ship or its children.</summary>
public struct StarSystemMember : ISharedComponentData { public StarSystemId Id; }

```

```csharp
// Assets/Game/Core/Events/OriginShifted.cs  (namespace Game.Core.Events; SPEC-02 §5.6 payload rules)
/// <summary>Raised on every rebase (architecture §6.5). Delivered next frame through ManagedEventQueue&lt;OriginShifted&gt;;
/// same-frame consumers read FrameOrigin.ShiftSequence/LastShiftDelta instead (§5.10).</summary>
public readonly struct OriginShifted
{
    public readonly Unity.Mathematics.float3 Delta;    // binding; zero when StarSystemChanged == 1 (same-star-system requests carry their delta)
    public readonly byte StarSystemChanged;            // 1: everything local was replaced; consumers reset (camera cut, clear trails)
    public readonly uint StarSystemValue;              // StarSystemId.Value of the frame after the rebase
    public readonly uint Sequence;                     // FrameOrigin.ShiftSequence after the rebase
    public OriginShifted(Unity.Mathematics.float3 delta, byte changed, uint starSystemValue, uint seq) { Delta = delta; StarSystemChanged = changed; StarSystemValue = starSystemValue; Sequence = seq; }
}
```

The Core singleton `OriginShiftRecord` (SPEC-02 §4, `Game.Core.Components`) mirrors the last rebase for `Game.Simulation`, which cannot read `FrameOrigin` (SPEC-21 `ProjectileStore`, SPEC-23 AI anchors): `{ uint Sequence; float3 Delta; byte StarSystemChanged; }`. `OriginRebaseSystem.OnCreate` adds it to the `FrameOrigin` entity (all zero); `DecideJob` writes it together with `FrameOrigin` (`Sequence = ShiftSequence`, `Delta = LastShiftDelta`, `StarSystemChanged = LastShiftChangedStarSystem`). It holds only the last rebase; consumers that see `Sequence` jump by more than 1 treat it like a star-system change (SPEC-21, SPEC-23 already do).

`Game.Galaxy/AssemblyInfo.cs` adds `[assembly: RegisterGenericComponentType(typeof(Game.Data.Components.ManagedEventQueue<Game.Core.Events.OriginShifted>))]` and `ManagedEventChannels.RegisterAll` gets `system.RegisterChannel<OriginShifted>();` (SPEC-02 §5.6 rule 3).

### 4.8 Managed services and interfaces (public contract)

Folder `Assets/Game/Galaxy/Services/`, namespace `Game.Galaxy.Services`.

```csharp
public readonly struct GalaxyFrameContext
{
    public readonly Unity.Entities.EntityManager EntityManager;
    public readonly double UniversalTime;    // SimClock.UniversalTime
    public readonly uint Tick;               // SimClock.Tick
    public readonly float UnscaledDeltaTime; // UnityEngine.Time.unscaledDeltaTime, clamped to 0.1
    public readonly int FrameCount;          // UnityEngine.Time.frameCount
}

/// <summary>A Galaxy managed service that needs one call per frame. Called by GalaxyFrameTicker.</summary>
public interface IGalaxyFrameTick { int TickOrder { get; } void Tick(in GalaxyFrameContext ctx); }

/// <summary>Registry of IGalaxyFrameTick. Constructed by SPEC-01 before every Galaxy service; ticked by GalaxyServicesBridgeSystem.</summary>
public sealed class GalaxyFrameTicker
{
    public void Register(IGalaxyFrameTick t);     // idempotent
    public void Unregister(IGalaxyFrameTick t);
    public void Tick(in GalaxyFrameContext ctx);  // order: TickOrder ascending, then type full name (ordinal); per-service try/catch → GameLog.Error, others still run
}

/// <summary>Implemented by SPEC-32 (TravelService). Lane availability for the map and routes.</summary>
public interface ILaneAccess
{
    bool IsLaneOpen(int laneIndex);               // gate condition true (ungated lanes: always true)
    string GetLockedMessage(int laneIndex);       // "" when open
    int GetFuelCost(int laneIndex);
    uint Revision { get; }                        // +1 whenever any IsLaneOpen result may have changed (the "ILaneAccess change counter" of §5.12)
}

/// <summary>Implemented by SPEC-33 (TerritoryService). Owner faction of a star system as an Allegiance faction index;
/// 255 = no owner (0 is the player faction, SPEC-33 decision 1). GetOwnerColorToken returns "" for no owner.</summary>
public interface IStarSystemOwnership { byte GetOwnerFaction(StarSystemId id); string GetOwnerColorToken(StarSystemId id); }

/// <summary>Implemented by SPEC-31 (StarSystemLoader). Lets delta writes reach the loaded star system.</summary>
public interface IGalaxyLiveDeltaSink
{
    void OnBodyDeltaChanged(BodyId body);         // replacement / story spawn added or removed in a loaded star system
    void FlushStructures();                       // capture damaged body grids into deltas now (called before save)
}

/// <summary>Owner of GalaxyDef, star-system content cache and world commands. Implements SPEC-30 §5.8.</summary>
public sealed class GalaxyService : System.IDisposable, IGalaxyFrameTick
{
    public GalaxyService(Game.Data.Services.ContentCatalog catalog, GalaxyState state, Unity.Entities.World world, GalaxyFrameTicker ticker);
    public int TickOrder => 0;
    public GalaxyDef Galaxy { get; }                   // null until NewGame/OnSessionRestored
    public GalaxyState State { get; }
    public GalaxyGenerationDefinition Definition { get; }  // the single GalaxyGenerationDefinition in the catalog
    public int GenerationCount { get; }                // galaxy generations since construction (R9 test hook)
    public int ContentGenerationCount(StarSystemId id);  // content generations of `id` since the last NewGame/restore (R9)

    // Lifecycle (called by SPEC-01)
    public void NewGame(ulong seed);                   // seed is ignored when Definition.UseFixedSeed
    public void OnSessionRestored();                   // after GameSession restored GalaxyState in place

    // Content
    public JobHandle PrefetchContent(StarSystemId id); // schedules generation; no-op if cached or scheduled
    public StarSystemContent GetContent(StarSystemId id);   // completes prefetch or generates now; applies deltas; cached
    public void ReleaseContent(StarSystemId id);       // disposes the cache entry
    public void SetLiveDeltaSink(IGalaxyLiveDeltaSink sink);
    public uint RequestOriginReset(StarSystemId id, double3 newOrigin); // enables OriginRebaseRequest with Serial+1; returns the serial
    public uint RequestOriginReset(StarSystemId id, double3 newOrigin, in PlayerPlacement placement); // same, and places the player ship
    public uint LastOriginRequestSerial { get; }

    // Queries
    public bool TryResolveStarSystem(Game.Core.ContentId authoredId, out StarSystemId id);
    public bool TryResolveBody(Game.Core.ContentId authoredStarSystem, string bodyKey, out BodyId id);
    public BodyTemplateDefinition GetBodyTemplate(ushort templateIndex);
    public string GetStarSystemName(StarSystemId id);  // §5.7
    public string GetBodyName(BodyId id);              // authored override, else template display name + " " + roman/letter suffix (§5.7)
    public string GetLocationKey(BodyId id);           // "<starsys id>/<body key>" for authored bodies, else "<ss:XXXXXXXX>/<role>:<slot>.<subslot>"
    public bool CapturePlayerLocation();               // reads PlayerShip UniversePosition + yaw into State; false when no player ship

    // World commands (narrative effects reach these through ILogicContext in Game.App)
    public void ReplaceBody(BodyId body, Game.Core.ContentId template);
    public BodyId AddStorySpawn(StarSystemId sys, Game.Core.ContentId template, BodyId anchor, double3 offsetM, float yawRad);
    public bool RemoveStorySpawn(BodyId storySpawn);
    public bool MarkBodyDestroyed(BodyId body);
    public bool MarkBodyInteracted(BodyId body);
    public bool IsBodyDestroyed(BodyId body);
    public void Reveal(StarSystemId id);               // Known += id (also hidden ones)
    public void MarkVisited(StarSystemId id);          // Visited += id; Known += id and its non-hidden neighbours

    public void Tick(in GalaxyFrameContext ctx);       // completes finished content prefetches (§5.8 step T1); no event draining
    public void Dispose();                             // disposes GalaxyDef and cached content
}
```

`GalaxyMapModel` (§5.12) and `GalaxySaveParticipant` (§5.11) are in the same folder.

### 4.9 Internal types

```csharp
// Assets/Game/Galaxy/Components/UniversePositionIsAnalytic.cs (public: SPEC-31 adds it to bodies on rails)
/// <summary>Tag: UniversePosition of this root is computed analytically (orbits). OriginRebaseSystem never syncs it from LocalTransform.</summary>
public struct UniversePositionIsAnalytic : IComponentData { }

// internal, Assets/Game/Galaxy/Generation/
internal struct RebaseDecision { public byte Rebase; public byte FromRequest; public byte StarSystemChanged; public double3 NewOrigin; public float3 Delta; public byte PlacePlayer; public PlayerPlacement Placement; }
internal struct LaneCandidate { public int A, B; public double LengthLy; }        // indices into StarSystems, A < B
```

## 5. Systems, services and flow

All generation code lives in `Assets/Game/Galaxy/Generation/` (namespace `Game.Galaxy.Generation`). Jobs are `[BurstCompile]` `IJob`s that read only `NativeArray`s in `GalaxyDef` and their own inputs. Generation runs at new game, load and star-system load, never in the fixed step, so completing these jobs on the main thread is allowed (§5.8 states exactly where).

### 5.1 `GalaxyDefBuilder` (managed, main thread)

`static GalaxyDef GalaxyDefBuilder.Build(ContentCatalog catalog, ulong seed, int generatorVersion)`. Steps:
1. `gen` = the single `GalaxyGenerationDefinition` in `catalog.All<GalaxyGenerationDefinition>()`. The validator guarantees exactly one (GAL-E001). If `generatorVersion` is not a value this build supports (`1` only), throw `GalaxyGenerationException("unsupported generator version N")`.
2. `BodyTemplates` = `catalog.All<BodyTemplateDefinition>()` (ordinal ID order, SPEC-04 Decision 5). Fill `BodyTemplateParams[i]` from each definition.
3. `Templates` = `gen.TemplatePool` in list order (weights stay with the entries). `TemplateParams[i]` from each template. Pools of every template are appended to `PoolEntries` in the order star, planet, moon, belt, station, POI, and each pool's `(start, count)` is stored in the template params. Pool entries with `weight <= 0` are skipped (the validator reports them, GAL-E011).
4. `Authored` = `gen.AuthoredStarSystems` sorted by `string.CompareOrdinal(Id.Value)`. For each, in that order:
   - `AuthoredTemplateParams[k]` comes from its `template`;
   - its bodies become `AuthoredBodyParams`, sorted by `LocalKey` and appended to `AuthoredBodies`;
   - `AuthoredBodyRanges[k] = (start, count)`.
5. Moons in authored star systems: `SubSlot` = the moon's index among the moons with the same `parentKey`, in list order; `Slot` = the parent planet's slot. `ParentKey` = the planet's `LocalKey`. Other roles: `SubSlot = 0`, `ParentKey = 0`.
6. Run §5.2 and §5.3 (layout and lanes), §5.4 (hash). Return the `GalaxyDef` (all `NativeArray`s `Allocator.Persistent`).

Lane-gate specs: for every authored lane whose `condition.Source` is not empty, append `new LaneGateSpec { Condition, LockedMessage }` to `LaneGates` and set `LaneDef.GateIndex = 1 + index`.

### 5.2 Layout job `GalaxyLayoutJob`

Inputs: `galaxySeed`, `gen` scalars, the authored positions/flags/danger/star template indices (sorted as in §5.1), template weights. Output: `NativeList<StarSystemDef>`.

1. **Authored first.** For each authored star system `k`:
   - `Id = StarSystemId.FromAuthored(ContentHash of its ID)`;
   - `PositionLy` = `positionLy`;
   - `AuthoredIndex = k + 1`;
   - `TemplateIndex = NoIndex`;
   - `StarTemplateIndex` = index of its `star` in `BodyTemplates`;
   - `Danger` = `danger`;
   - `Flags = Authored | (Start if it is gen.startStarSystem) | (Story if isStory) | (HiddenUntilKnown) | (NoEncounters) | (Hub)`.

   If two authored IDs map to the same `StarSystemId`, throw `GalaxyGenerationException` (the validator reports it first as GAL-E021).
2. **Spatial grid.** Cell size `c = minStarSystemSpacingLy`. Key = `int2(floor(x / c), floor(y / c))`. Use a `NativeParallelMultiHashMap<int2, int>` (capacity `authored + proceduralCount`, `Allocator.Temp`). Insert every authored star system.
3. **Procedural placement.**
   - `rng = RngStreams.For(galaxySeed, RngPurpose.GalaxyLayout, 0)`.
   - For `i` in `0 .. proceduralStarSystemCount − 1`: for `attempt` in `0 .. placementAttemptsPerStarSystem − 1`:
     1. `u = rng.NextDouble()`, `t = rng.NextDouble()` (both always drawn);
     2. `r = galaxyRadiusLy * sqrt(u)`, `θ = 2π t`, `p = (r cos θ, r sin θ)`;
     3. check the 3×3 cells around `p`'s cell. If any accepted star system is closer than `c`, continue;
     4. otherwise accept: `Id = FromProceduralIndex(acceptedCount)`, insert into the grid, `acceptedCount++`, break.
   - If all attempts fail, star system `i` is skipped. This is deterministic, and `GalaxyDef` exposes `SkippedProceduralCount`, which GAL-007 asserts is 0 for the M3 definition and logs at Info otherwise.
4. **Per-star-system attributes.** For each procedural star system:
   - `Seed = StableHash.Combine(galaxySeed, Id.Value)`;
   - `a = RngStreams.For(Seed, RngPurpose.StarSystemGeneration, 0)`;
   - draw exactly three values in this order: `tRoll = a.NextFloat()`, `dRoll = a.NextFloat()`, `sRoll = a.NextFloat()`.
     - `TemplateIndex` = weighted pick over `Templates` with `tRoll` (§5.5.6);
     - `Danger = DangerMin + (int)floor(dRoll * (DangerMax − DangerMin + 1))`, clamped to `DangerMax`;
     - `StarTemplateIndex` = weighted pick over the template's star pool with `sRoll`.
   - `Flags = None`.

   Authored star systems also get `Seed = StableHash.Combine(galaxySeed, Id.Value)`.
5. Sort all star systems by `Id.Value` ascending. Build `IndexById`.

Complexity O(N × attempts); N = 1,000, attempts = 30 → ≤ 30,000 grid probes.

### 5.3 Lane job `GalaxyLaneJob`

1. **Authored lanes.** For each `AuthoredLane` in definition order:
   - `(A, B)` = the two `StarSystemId`s, ordered so `A.Value < B.Value`;
   - `LengthLy` = the Euclidean distance;
   - `Kind`;
   - `Flags = Authored | HiddenUntilKnown?`;
   - `GateIndex` per §5.1;
   - gate templates = the lane's override, else the galaxy default for the kind.

   Record the pair in a `NativeHashSet<ulong>` keyed `((ulong)A << 32) | B`.
2. **Participants.** `P` = procedural star systems plus authored ones with `connectProcedurally`, in `Id` order.
3. **Candidates.**
   - Grid cell size = `maxLaneLengthLy`.
   - For each `p ∈ P`, collect every `q ∈ P` in the 3×3 cells with `0 < dist ≤ maxLaneLengthLy`, sort by `(dist, q.Id)`, and keep the first `laneCandidateNeighbours`.
   - Add the pair `(min, max)` to the candidate list unless it is already present.
   - Sort the candidates by `(LengthLy, A.Id, B.Id)`.
4. **Spanning forest.** Kruskal over the candidates with a union-find (`NativeArray<int> parent`, path halving). An edge joining two sets is accepted even if it exceeds `maxLaneDegree`, because connectivity wins over degree.
5. **Extra lanes.**
   - `rng = RngStreams.For(galaxySeed, RngPurpose.GalaxyLanes, 0)`.
   - For each candidate **not** accepted in step 4, in sorted order: draw `x = rng.NextFloat()` (always drawn). Accept the candidate if `x < extraLaneChance` and both endpoint degrees `< maxLaneDegree`.
6. **Bridge components.** While more than one union-find set contains a member of `P`:
   1. take the set containing the smallest `Id` that is not in the start star system's set (if the start star system is not in `P`, use the set of the smallest `Id` in `P`);
   2. find the shortest pair `(u in that set, v outside it)` by brute force over `P`, breaking ties on `(u.Id, v.Id)`;
   3. add the lane and union the two sets.

   This is O(N² × sets). It only runs for disconnected leftovers and is capped at `N ≤ 100,000` by the validator. Bridges can exceed `maxLaneLengthLy`.
7. Drop procedural lanes whose pair already exists in the authored set.
8. Sort all lanes by `(A.Value, B.Value)`.
9. **Adjacency.** For each star system, list the lanes touching it, sorted by the neighbour's `Id.Value`, into `Adjacency`, and set `FirstAdjacency` and `LaneCount`.

### 5.4 Galaxy hash `GalaxyHash.Compute`

1. Serialize the following into a `NativeList<byte>` (little-endian, raw IEEE bits for floats and doubles):
   - **For each star system in order:** `Id.Value u32`, `PositionLy.x f64`, `PositionLy.y f64`, `Seed u64`, `TemplateIndex u16`, `AuthoredIndex u16`, `StarTemplateIndex u16`, `Flags u16`, `Danger u8`;
   - **then for each lane in order:** `A u32`, `B u32`, `LengthLy f32`, `Kind u8`, `Flags u8`, `GateIndex u16`, `GateTemplateA u16`, `GateTemplateB u16`.
2. `Hash = StableHash.Hash64(bytes, seed: (ulong)generatorVersion)`.

The same function with the same layout is used for `StarSystemContent.GeneratedHash`. It runs over `BodySpec` fields in declaration order, `Seed` included, `State` excluded.

**Golden values.** `Assets/Game/Tests/EditMode/Galaxy/Golden/galaxy_v1.txt` holds lines `<fixture name> <hash hex>`. The fixture definitions are `TestGalaxyFixtures.Small` (4 authored + 50 procedural, seed `0x1234`) and `.Large` (1,000 procedural, seed `0xABCD`). The first implementation writes the file; afterwards any mismatch fails `GalaxyHashTests.MatchesGolden`. Changing a golden value requires bumping `CurrentGeneratorVersion` (Decision 2) and keeping the v1 path.

### 5.5 Star-system content job `StarSystemContentJob`

Inputs:
- the `StarSystemDef`;
- template params: `TemplateParams[TemplateIndex]`, or `AuthoredTemplateParams[AuthoredIndex − 1]` when authored;
- `PoolEntries`, `BodyTemplateParams`;
- for authored star systems, the `AuthoredBodies` range;
- the adjacent lanes, plus each neighbour's `PositionLy`.

Output: `NativeList<BodySpec>`, `laneRingRadiusM`, `extentM`.

Body seeds: `BodySpec.Seed = StableHash.Combine(def.Seed, id.LocalKey)`. **Body stream:** `b = RngStreams.For(BodySpec.Seed, RngPurpose.BodyGeneration, 0)`, drawing exactly five floats in order `[tmpl, radius, phase, extraA, extraB]` whether or not each is used.

1. **Star.** `BodyId(Star, 0, 0)`; template = `StarTemplateIndex`; `Orbit = Fixed, RadiusM 0`; `RadiusM = lerp(RadiusMinM, RadiusMaxM, radius)`.
2. **Authored path** (`AuthoredIndex > 0`). For each `AuthoredBodyParams` in `LocalKey` order:
   - `Orbit.Mode = Mode`;
   - `Orbit.RadiusM = OrbitRadiusM`;
   - `PhaseRad`;
   - `Eccentricity`;
   - `PeriodS = PeriodOverrideS > 0 ? PeriodOverrideS : KeplerLite(...)` (§5.5.5);
   - `RadiusM = RadiusOverrideM > 0 ? RadiusOverrideM : lerp(min, max, radius)`;
   - `YawRad`;
   - `ParentKey`;
   - `ParentOrbit` = the parent's `Orbit` (the parent was emitted earlier because Planet role (2) < Moon role (3)).
   - Belt: `BeltInnerRadiusM = OrbitRadiusM − BeltWidthM/2`, `BeltOuterRadiusM = OrbitRadiusM + BeltWidthM/2`.

   Then go to step 6.
3. **Procedural counts.** `L = RngStreams.For(def.Seed, RngPurpose.StarSystemLayout, 0)`. Draw in this exact order:
   - `planets = L.NextInt(MinPlanets, MaxPlanets + 1)`;
   - `belts = L.NextInt(MinBelts, MaxBelts + 1)`;
   - `stations = L.NextInt(MinStations, MaxStations + 1)`;
   - `pois = L.NextInt(MinPois, MaxPois + 1)`;
   - then for `k` in `0 .. MaxPlanets + MaxBelts − 1`: `spacing[k] = L.NextFloat()`;
   - then for `p` in `0 .. MaxPlanets − 1`: `moonRoll[p] = L.NextFloat()`, `moonCount[p] = L.NextInt(0, MaxMoonsPerPlanet + 1)`.

   Every draw happens for the maximum counts, so the stream position never depends on rolled values.
4. **Orbit radii.**
   - `a[0] = FirstOrbitRadiusM`;
   - `a[k+1] = a[k] + lerp(OrbitSpacingMinM, OrbitSpacingMaxM, spacing[k])`;
   - planets use `a[0 .. planets−1]`, belts use `a[planets .. planets+belts−1]`.
5. **Procedural bodies.**
   - **Planet `p`.** `BodyId(Planet, p, 0)`;
     - template = weighted pick of the planet pool with `tmpl`;
     - `Circular`, `RadiusM = a[p]`, `PhaseRad = 2π·phase`, `PeriodS = KeplerLite(a[p], OrbitPeriodAtReferenceS, OrbitReferenceRadiusM)`;
     - body `RadiusM = lerp(min, max, radius)`.
   - **Moons of planet `p`.** If the planet template has `CanHaveMoons` and `moonRoll[p] < MoonChance`, then for `m` in `0 .. moonCount[p] − 1`:
     - `BodyId(Moon, p, m)`;
     - template from the moon pool with the moon's `tmpl`;
     - `Circular`, `RadiusM = planetRadius + MoonOrbitMinM + (MoonOrbitMaxM − MoonOrbitMinM) * (m + extraA) / max(1, MaxMoonsPerPlanet)`;
     - `PeriodS = KeplerLite(r, MoonPeriodAtReferenceS, MoonReferenceRadiusM)`;
     - `ParentKey` = the planet's key; `ParentOrbit` = the planet orbit.
   - **Belt `k`.** `BodyId(Belt, k, 0)`; `Fixed`, `RadiusM = a[planets + k]`; width from the template; inner and outer radii as in step 2.
   - **Station `s` and POI `q`.** `BodyId(Station, s, 0)` / `BodyId(PointOfInterest, q, 0)`. `Fixed`.
     1. Up to 8 candidates `j = 0..7`: use the body stream's `phase`, `extraA` and `extraB` for `j = 0`; for `j > 0` use `RngStreams.For(Seed, BodyGeneration, (ulong)j)` and its first two floats.
     2. Radius = `lerp(StationRadiusMinM, StationRadiusMaxM, u)` (POI: the POI range); angle `2πv`.
     3. Accept the first candidate whose distance to every earlier Fixed body (stations, POIs, gates are placed later) is ≥ `MinBodySeparationM`; otherwise take candidate 7.
     4. `YawRad = 2π·extraB`.
   - Templates come from the station and POI pools (weighted pick with `tmpl`).
6. **Lane gates.** Compute the extent:
   - `maxExtent = max over bodies of (Orbit.RadiusM·(1 + Eccentricity) + RadiusM + (ParentOrbit.RadiusM if moon))`;
   - for belts use `BeltOuterRadiusM`.

   Then:
   - `LaneRingRadiusM = max(MinLaneRingRadiusM, maxExtent + LaneRingMarginM)`;
   - `ExtentM = LaneRingRadiusM + ExtentMarginM`.

   For adjacency slot `j` (in adjacency order):
   1. `BodyId(LaneGate, j, 0)`, `LaneIndex` = the lane index;
   2. direction `d = normalize(neighbour.PositionLy − self.PositionLy)` mapped to `(d.x, 0, d.y)`; angle `φ = atan2(d.y, d.x)`;
   3. if an earlier gate is within `MinBodySeparationM` along the ring (arc distance), add `MinBodySeparationM / LaneRingRadiusM` radians to `φ` and retry, up to 16 times;
   4. `Fixed`, `RadiusM = LaneRingRadiusM`, `PhaseRad = φ`, `YawRad = φ + π` (the gate faces the star);
   5. template = `GateTemplateA` if self is `A`, else `GateTemplateB`.
7. Sort `Bodies` by `LocalKey` (insertion sort; ≤ 512 elements). If the count exceeds `MaxBodiesPerStarSystem`, truncate the list to 512 and set the job's error flag; `GalaxyService` logs `GameLog.ErrorOnce`. The validator prevents this for shipped content (GAL-E014).

**5.5.5 Kepler-lite.**
- `KeplerLite(a, Tref, aref) = Tref * pow(a / aref, 1.5)`, clamped to ≥ 60 s.
- Elliptic orbits use mean anomaly `M = PhaseRad + 2π * frac(t / PeriodS)`.
  - Solve `E − e sin E = M` with exactly 6 Newton iterations from `E0 = M`. Fixed iteration count means the result is deterministic.
  - Position is `(a(cos E − e), 0, a√(1−e²) sin E)` rotated by `ArgPeriapsisRad` about Y.
- Circular: `θ = PhaseRad + 2π * frac(t / PeriodS)`; position `R(cos θ, 0, sin θ)`; velocity `(2πR/T)(−sin θ, 0, cos θ)`.
- Fixed: position `R(cos PhaseRad, 0, sin PhaseRad)`, velocity 0.
- `frac` uses `t / PeriodS − floor(t / PeriodS)` in double precision.
- `OrbitMath.Position(b, t)` = `LocalPosition(ParentOrbit, t) + LocalPosition(Orbit, t)` (moons and anchored story spawns; others have a zero `ParentOrbit`).

**5.5.6 Weighted pick.** `total = Σ weight` over entries with `maxCount == 0 || used < maxCount`. Pick the first entry with cumulative weight `> roll * total`. If `total == 0`, use `NoIndex`: the body is skipped and the job's warning flag is set. `used` counts per pool per star system.

### 5.6 `StarSystemDeltaApplier` (managed, main thread, after the content job)

`static void Apply(StarSystemContent c, StarSystemDelta d, GalaxyDef g, ContentCatalog catalog)`. Fixed order:
1. **Replacements** (sorted by key). Find the body by binary search; if it is absent, skip with `WarnOnce("GAL.Delta.MissingBody")`. Resolve `Template` via `catalog.TryResolve<BodyTemplateDefinition>`. If unresolved, keep the original template and record the unresolved ID (SPEC-04). Otherwise set `TemplateIndex` and `State |= Replaced`.
2. **Story spawns** (index order). For `slot i` with `!Removed`:
   - `BodyId(StorySpawn, i, 0)`;
   - template resolved as above (unresolved → skip the spawn);
   - `ParentKey = AnchorBodyKey`.
   - If the anchor is a `Moon`, use the moon's parent planet as the anchor and `WarnOnce("GAL.StorySpawn.MoonAnchor")`.
   - `ParentOrbit` = the anchor's `Orbit`, or zero when the anchor key is 0 or missing.
   - `Orbit = Fixed`, `RadiusM = length(OffsetM.xz)`, `PhaseRad = atan2(OffsetM.z, OffsetM.x)`; `YawRad`; `RadiusM` (body) = the template's `RadiusMaxM`; `State |= StorySpawn`.
   - Append. Role 9 sorts after all generated roles, and slots ascend, so `Bodies` stays sorted. Assert this in debug builds.
3. **Interacted.** `State |= Interacted` for each key found.
4. **Destroyed.** `State |= Destroyed`. Destroyed bodies stay in the list; SPEC-31 decides what to spawn for them.
5. **Structure diffs.** `State |= HasStructure`.

The applier never consumes RNG and never reorders generated bodies.

### 5.7 Names

- Authored star system: `displayName`.
- Procedural star system: `namePrefix + " " + (char)('A' + (int)(Seed % 26)) + "-" + Id.Value.ToString("D4")` (e.g. "Uncharted K-0042").
- Body:
  - an authored `displayNameOverride` if non-empty;
  - else Planet: `<star system name> <Roman(slot + 1)>` (I…XX);
  - Moon: `<planet name> <(char)('a' + subSlot)>`;
  - LaneGate: `"Gate to " + <neighbour star system name>`;
  - Star: `<star system name>`;
  - every other role: the template's `displayName`.
- Names are computed on demand and cached per `BodyId` in a `Dictionary<BodyId, string>` that is cleared on `ReleaseContent`.

### 5.8 `GalaxyService`, `GalaxyFrameTicker` and `GalaxyServicesBridgeSystem`

`GalaxyServicesBridgeSystem` (`Game.Presentation`, folder `Presentation/World/`):
- a `SystemBase` in `WorldGroup` with `OrderFirst = true` and `RequireForUpdate<GameplayActive>`;
- each frame it builds a `GalaxyFrameContext` from `SimClock` and `UnityEngine.Time` and calls `Services.Get<GalaxyFrameTicker>().Tick(ctx)`. If the ticker is missing, it does `WarnOnce` and returns.

`GalaxyService` behaviour:
- **`NewGame(seed)`**
  1. `Dispose` cached content and `Galaxy`;
  2. `State.Clear()`;
  3. `s = Definition.UseFixedSeed ? Definition.FixedSeed : seed`; if `s == 0`, throw `ArgumentException` (SPEC-01 generates non-zero seeds);
  4. `State.Seed = s`, `State.GeneratorVersion = CurrentGeneratorVersion`;
  5. `Galaxy = GalaxyDefBuilder.Build(...)` (jobs `Run()` synchronously); `GenerationCount++`;
  6. `State.CurrentStarSystem = Galaxy.StartStarSystem`;
  7. `MarkVisited(start)`;
  8. `State.HasPlayerLocation = false` (SPEC-31 computes the start location with `GetStartLocation`, §6).
- **`OnSessionRestored()`**
  1. dispose `Galaxy` and content;
  2. `Galaxy = Build(State.Seed, State.GeneratorVersion)`; `GenerationCount++`;
  3. reset `ContentGenerationCount`.
  - An unsupported version throws `GalaxyGenerationException`. SPEC-01 catches it and fails the load with "This save needs a newer game version."
- **`PrefetchContent(id)`**: if cached or already scheduled, return the existing handle. Otherwise allocate the output list (`Persistent`), schedule `StarSystemContentJob` and remember `(id → handle, list)`.
- **`GetContent(id)`**
  1. If cached, return it.
  2. If scheduled, call `handle.Complete()`; this is allowed because SPEC-31 calls it from the `WorldGroup` streaming step, outside the fixed step, and normally only after `IsCompleted`. Otherwise run the job synchronously.
  3. Wrap the result in `StarSystemContent`, compute `GeneratedHash`, apply the delta if `State.TryGetDelta`, cache it, and increment `ContentGenerationCount[id]`.
  - An unknown `id` throws `KeyNotFoundException`.
- **`ReleaseContent(id)`**: complete any scheduled job, dispose, and remove the cache entry and the cached names for that star system.
- **Delta writes** (`ReplaceBody`, `AddStorySpawn`, `RemoveStorySpawn`, `MarkBodyDestroyed`, `MarkBodyInteracted`):
  1. update `State.GetOrCreateDelta(sys)`;
  2. if that star system's content is cached, patch the cached `BodySpec`, in the same way §5.6 does for that record type;
  3. call `liveSink?.OnBodyDeltaChanged(body)`.
  - `AddStorySpawn` returns `BodyId(StorySpawn, slot, 0)`.
- **`MarkVisited(id)`**: `AddVisited(id)`, `AddKnown(id)`, and `AddKnown` for every neighbour whose star system is not `HiddenUntilKnown` and whose lane is not `HiddenUntilKnown`. `IncrementVisit` on the delta happens in SPEC-31, on star-system entry.
- **`RequestOriginReset(id, origin[, placement])`**: main thread; `serial = ++LastOriginRequestSerial`; set `OriginRebaseRequest { NewOrigin, StarSystem = id, Serial, PlacePlayer = placement given ? 1 : 0, Placement }` on the `FrameOrigin` entity and enable it; return `serial`. A second request before the first is applied overwrites it (the higher serial wins; callers wait for `FrameOrigin.AppliedRequestSerial ≥ serial`).
- **`Tick(ctx)`** (T1): for each scheduled prefetch whose handle `IsCompleted`, leave it in place; `GetContent` will finalize it. `Tick` does nothing else in v1. It exists so SPEC-31/32/33 services share one ordered per-frame entry point.

`GalaxyFrameTicker` order (`TickOrder`):
- `GalaxyService` 0;
- SPEC-31 `StarSystemLoader` 100;
- SPEC-32 `TravelService` 200;
- SPEC-33 `EncounterDirectorService` 300 and `TerritoryService` 310.

### 5.9 `GalaxyContentValidator` (`IContentDatabaseValidator`, `Assets/Game/Editor/Content/Validators/`)

Codes (`ValidationReport` severity in brackets):

| Code | Rule |
|---|---|
| GAL-E001 [E] | Exactly one `GalaxyGenerationDefinition` |
| GAL-E002 [E] | `generatorVersion == CurrentGeneratorVersion` |
| GAL-E003 [E] | `startStarSystem` set and contained in `authoredStarSystems` |
| GAL-E004 [E] | `authoredStarSystems.Count ≤ 256`, no nulls, no duplicates |
| GAL-E005 [E] | Authored bodies: `key` matches `^[a-z][a-z0-9_]*$` and is unique per star system; `(role, slot)` unique; for moons `(slot, subslot)` unique |
| GAL-E006 [E] | Moon `parentKey` names a body with role Planet in the same star system; non-moons have `parentKey == ""` |
| GAL-E007 [E] | Station, PointOfInterest and Belt use `OrbitMode.Fixed`; Planet and Moon use Circular or Elliptic |
| GAL-E008 [E] | `0 ≤ eccentricity ≤ 0.3`; `orbitRadiusM > 0` except Fixed at 0 is allowed for POIs |
| GAL-E009 [E] | Authored lanes: both ends set, different, pair unique |
| GAL-E010 [E] | Every pool entry's template role equals the pool's role (star pool → Star, …); every authored body's template role equals its `role` |
| GAL-E011 [E] | Pool weights > 0; a pool is non-empty when its minimum count > 0 |
| GAL-E012 [E] | `min ≤ max` for every count pair; counts ≥ 0 |
| GAL-E013 [E] | Range pairs `min ≤ max` and > 0 (radii, orbit spacing, station/POI radius, moon orbit) |
| GAL-E014 [E] | Body bound: `1 + MaxPlanets·(1 + MaxMoonsPerPlanet) + MaxBelts + MaxStations + MaxPois + maxLaneDegree + authoredLaneCount ≤ 512` per template; authored: `1 + bodies + lanes ≤ 512` |
| GAL-E015 [E] | Body templates: `Blueprint` source has `blueprint`; `ProceduralShape` has `sizeCells > 0`, `wallThicknessCells ≥ 0`, `hullBlock` set, and `coreBlock` set when `destructible` |
| GAL-E016 [E] | Belt templates: `asteroidBlock` set; `1 ≤ asteroidsPerSectorMin ≤ asteroidsPerSectorMax ≤ 8`; `sectorSizeM ≥ 100` |
| GAL-E017 [E] | `0 ≤ proceduralStarSystemCount ≤ 100000`; `placementAttemptsPerStarSystem` 1..100; `laneCandidateNeighbours` 1..16; `maxLaneDegree` 1..16 |
| GAL-E018 [E] | Start star system: `startBodyKey` names one of its bodies |
| GAL-E019 [E] | `defaultHyperlaneGate` and `defaultAncientGate` set with role LaneGate |
| GAL-E020 [E] | `BodyVisualDefinition.meshRadius > 0`; `mesh` and `material` set |
| GAL-E021 [E] | No two authored star systems produce the same `StarSystemId` |
| GAL-W001 [W] | Authored star systems closer than `minStarSystemSpacingLy` |
| GAL-W002 [W] | Authored lane longer than `maxLaneLengthLy` |
| GAL-W003 [W] | Authored position outside `galaxyRadiusLy` |

### 5.10 `OriginRebaseSystem`

- **Group:** `GameInitGroup`, `OrderFirst = true` (architecture §5: before `PlayerInputSampleSystem` and `GridSpawnSystem`). `[BurstCompile] ISystem`; `RequireForUpdate<GameplayActive>`, `RequireForUpdate<WorldSceneActive>` (SPEC-01 Decision 6: no galaxy in the Blueprint Lab), `RequireForUpdate<FrameOrigin>`.
- **`OnCreate`:** create the singleton entity with `FrameOrigin` (all zero) and `OriginRebaseRequest` (disabled). This happens once and is not per-frame structural work.
- **Reads/Writes:** writes `FrameOrigin`, `OriginRebaseRequest` (enable bit only), `UniversePosition` (non-analytic roots), `LocalTransform` of roots (`WithNone<Parent>`), `PhysicsGraphicalInterpolationBuffer.PreviousTransform` and the enqueue to `ManagedEventQueue<OriginShifted>`. Architecture §8 makes it the only writer of these during a rebase.

Per frame, entirely in jobs with no main-thread sync point (markers `OriginRebaseSystem.Decide`, `.Shift`, `.Sync`):

1. **`DecideJob` (`IJob`).** Inputs: `FrameOrigin` and `OriginRebaseRequest` via `ComponentLookup` on the singleton entity; the player root's `LocalTransform` via a `NativeArray` from `playerQuery.ToComponentDataListAsync` (`PlayerShip`, `WithNone<Parent>`, dependency combined); `RebaseDecision` as a `NativeReference` (`WorldUpdateAllocator`).
   1. If the request is enabled and `request.Serial > FrameOrigin.AppliedRequestSerial`:
      - `Rebase = 1`, `FromRequest = 1`, `NewOrigin = request.NewOrigin`;
      - `StarSystemChanged = request.StarSystem != FrameOrigin.StarSystem`;
      - `Delta = changed ? 0 : (float3)(FrameOrigin.OriginInStarSystem − NewOrigin)`;
      - `PlacePlayer = request.PlacePlayer`, `Placement = request.Placement`;
      - disable the request; set `AppliedRequestSerial`.
   2. Else, if a player root exists and `length(playerLocal) > 2000`:
      - `Rebase = 1`, `FromRequest = 0`;
      - `NewOrigin = FrameOrigin.OriginInStarSystem + (double3)playerLocal`;
      - `Delta = (float3)(old − NewOrigin)`, i.e. `−playerLocal`.
   3. If `Rebase`:
      - update `FrameOrigin` (`OriginInStarSystem = NewOrigin`, `StarSystem` from the request if `FromRequest`, `ShiftSequence++`, `LastShiftDelta = Delta` (already 0 when the star system changed), `LastShiftChangedStarSystem = StarSystemChanged`);
      - write `OriginShiftRecord { Sequence = ShiftSequence, Delta, StarSystemChanged }`;
      - enqueue `OriginShifted(Delta, changed, FrameOrigin.StarSystem.Value, ShiftSequence)` with `SortKey = ShiftSequence`.

   Same-star-system requests (SPEC-32 cruise, M4) therefore shift every consumer exactly like a threshold rebase: VFX, debris, trails, camera, projectiles and AI anchors move by `Delta`.
2. **`ShiftJob` (`IJobEntity`, `ScheduleParallel`)** over roots with `LocalTransform` (`WithNone<Parent>`). It reads `RebaseDecision` and returns immediately when `Rebase == 0`.
   - With `UniversePosition`: `Position = (float3)(UniversePosition.Value − NewOrigin)`.
   - Without it: `Position += Delta`; when `StarSystemChanged == 1`, skip (those entities belong to the star system being unloaded).
   - If the entity has `PhysicsGraphicalInterpolationBuffer`: `PreviousTransform.pos += Delta`, or set it to the new pose when the star system changed. This prevents a one-frame smear (03-B9c).
2b. **`PlaceJob` (`IJobEntity`, single-threaded `Schedule`, after `ShiftJob`)** over roots with `PlayerShip`, `LocalTransform`, `PhysicsVelocity` (`WithNone<Parent>`). It returns immediately unless `Rebase == 1 && PlacePlayer == 1`. Then:
   - `oldYaw = Planar.YawFromForward(math.mul(Rotation, new float3(0, 0, 1)))`;
   - `Position = (float3)(Placement.PositionM − NewOrigin)`; `Rotation = Planar.YawRotation(Placement.YawRad)`;
   - `PhysicsVelocity.Linear = math.mul(quaternion.RotateY(Placement.YawRad − oldYaw), Linear)`; `Angular` unchanged (the speed is kept in ship-local terms, SPEC-32 decision 6);
   - if it has `UniversePosition`: `Value = Placement.PositionM`;
   - if it has `PhysicsGraphicalInterpolationBuffer`: `PreviousTransform` = the new pose (no smear).

   This is the only velocity write outside Unity Physics and the impulse writers (architecture §8: placement re-expression only).
3. **`SyncJob` (`IJobEntity`, `ScheduleParallel`)** over roots with `UniversePosition` and `LocalTransform`, `WithNone<Parent, UniversePositionIsAnalytic>`: `UniversePosition = FrameOrigin.OriginInStarSystem + (double3)Position`. It reads the post-rebase origin from the `RebaseDecision`, or from `FrameOrigin` when there was no rebase.

Everything else that holds positions reacts to the shift; this spec does not implement those reactions.
- **Same frame:** read `FrameOrigin.ShiftSequence` and `LastShiftDelta` (Presentation and Galaxy) or `OriginShiftRecord` (Simulation) and compare with the last seen sequence. Presentation systems that keep their own world-space buffers do this: camera (SPEC-62), trails/VFX/debris buffers (SPEC-63), audio emitters (SPEC-64).
- **Next frame:** `EventBus<OriginShifted>`, for managed listeners that tolerate a one-frame delay.

### 5.11 `GalaxySaveParticipant` (`GLXY`, order 80, version 1)

Implements `ISaveParticipant` over the `GalaxyState` owned by `GameSession`.
- **Capture:**
  1. `WriteUInt64(Seed)`; `WriteInt32(GeneratorVersion)`; `WriteUInt32(RouteDestination.Value)`;
  2. `WriteVarUInt32(Known.Count)` + `WriteUInt32` each; the same for `Visited`;
  3. deltas: `DeltasSorted()` → `WriteVarUInt32(count)`. Per delta:
     1. `WriteUInt32(id)`; `WriteVarUInt32(VisitCount)`;
     2. `Destroyed` and `Interacted` lists (`VarUInt32` count + `UInt64` each);
     3. story spawns (count, then per record `WriteContentId(Template)`, `WriteUInt64(AnchorBodyKey)`, `WriteDouble3(OffsetM)`, `WriteSingle(YawRad)`, `WriteBool(Removed)`);
     4. replacements (count, then `UInt64` key + `WriteContentId`);
     5. structure diffs (count, then `UInt64` key, `UInt32` revision, `WriteDeferred(VoxsSnapshotBlock.Capture(source of Structure, names))`).
- **Not saved here:** `CurrentStarSystem`, `HasPlayerLocation` and `PlayerLocation` are written by SPEC-31's `PLOC` participant (SPEC-05 §4.8 table) over the same `GalaxyState` object.
- **Restore(reader, fromVersion):**
  - `fromVersion == 0` → `State.Clear()` and return.
  - Otherwise `State.Clear()` and read in the same order. Structures are decoded with `VoxsCodec.TryDecode` and converted to a `VoxelStructure` (SPEC-10 adapter). On failure, drop that record, log `GameLog.Warn`, and continue (SPEC-05 §7).
  - Unknown template `ContentId`s are kept as read; they are resolved (and reported) when applied (§5.6).
  - The participant does **not** regenerate the galaxy; SPEC-01 calls `GalaxyService.OnSessionRestored()` after every participant has restored.

### 5.12 `GalaxyMapModel`

A managed class in `Galaxy/Services/`, constructed by SPEC-01 with `(GalaxyService, ILaneAccess, IStarSystemOwnership)`. Either interface may be `null` (its spec's task is not done yet): a `null` `ILaneAccess` means every lane is open with fuel cost 0 and `Revision` 0; a `null` `IStarSystemOwnership` means no owner (`OwnerColorToken` = ""). Read-only for the UI (SPEC-61).

```csharp
public readonly struct StarSystemMapEntry { public readonly StarSystemId Id; public readonly double2 PositionLy; public readonly string Name; public readonly bool Known, Visited, Current, IsStory, IsRouteTarget; public readonly byte Danger; public readonly string OwnerColorToken; }
public readonly struct LaneMapEntry { public readonly int LaneIndex; public readonly StarSystemId A, B; public readonly bool Open; public readonly string LockedMessage; public readonly int FuelCost; }
public sealed class GalaxyMapModel
{
    public uint Revision { get; }                       // increments when any visible entry changes
    public IReadOnlyList<StarSystemMapEntry> StarSystems { get; }
    public IReadOnlyList<LaneMapEntry> Lanes { get; }
    public void Refresh();                              // rebuilds when GalaxyService/GalaxyState changed; O(N + L)
}
```

Visibility:
- A star system is listed if it is `Known`.
- A lane is listed if both ends are `Known` and the lane is not `HiddenUntilKnown`, or if both ends are `Visited`.
- Unknown star systems are not listed at all, so the map never leaks hidden content.

`Refresh` compares a version stamp (`GalaxyState.Revision`, `ILaneAccess.Revision`, the current star system) and rebuilds only when it changed. Entries are reused lists with no per-frame allocation when nothing changed. SPEC-61's view model calls `Refresh()` when the map screen opens and on `StarSystemEntered`/`FactChanged`.

## 6. Public API

| API | Preconditions | Postconditions / errors |
|---|---|---|
| `GalaxyService.NewGame(seed)` | Catalog valid (GAL-E*) | `Galaxy != null`, `GenerationCount` +1, start star system Known + Visited. `ArgumentException` for seed 0 when not fixed |
| `GalaxyService.OnSessionRestored()` | All participants restored | Galaxy rebuilt from saved seed/version, `GenerationCount` +1. `GalaxyGenerationException` for unsupported version |
| `GalaxyService.GetStartLocation()` → `PlayerLocation` | `Galaxy != null` | Start body position at UniversalTime `State`'s current time + `startOffsetM` rotated by the body yaw; yaw = `startYawDegrees` |
| `PrefetchContent` / `GetContent` / `ReleaseContent` | `id` exists in `Galaxy` | See §5.8. `GetContent` returns the same instance until released |
| `ReplaceBody`, `AddStorySpawn`, `RemoveStorySpawn`, `MarkBodyDestroyed`, `MarkBodyInteracted` | `Galaxy != null`; body's star system exists | Delta updated; cached content patched; sink notified. Unknown template → `false`/`default(BodyId)` + `GameLog.Warn` |
| `TryResolveStarSystem`, `TryResolveBody` | — | `false` when unknown (never throws) |
| `RequestOriginReset(id, origin)` | Main thread; `FrameOrigin` exists | Rebase applied by `OriginRebaseSystem` next `GameInitGroup` update |
| `OrbitMath.*` | `PeriodS > 0` for moving orbits | Pure; Burst-compatible |
| `GalaxyMapModel.Refresh()` | Main thread | Entries current; `Revision` +1 only when content changed |

## 7. Edge cases and failure behaviour

| Situation | Required behaviour |
|---|---|
| Load a save whose `GeneratorVersion` > supported | `GalaxyGenerationException`; SPEC-01 fails the load with a message; nothing half-loaded |
| Authored star system removed from content after a save | Its deltas remain in `GalaxyState` (saved again unchanged); `TryGetIndex` false; SPEC-31 treats a current star system that no longer exists as "move to start star system" and logs an error |
| Body template removed after a save (replacement / story spawn) | Unresolved ID recorded and shown in the load warning; the original body stays (replacement) or the spawn is skipped |
| Procedural placement cannot place every star system | Fewer star systems; deterministic; Info log with the count |
| Disconnected procedural graph | Bridge lanes (§5.3 step 6); every star system reachable from the start |
| Two gates in the same direction | Rotated along the ring (§5.5 step 6) |
| Rebase requested twice in one frame | Only the highest serial applies (the request component holds the last values) |
| Rebase request and threshold in the same frame | Request wins; threshold is checked again next frame |
| Player ship absent (dead, in editor) | No threshold rebase; requests still apply |
| Player more than 2,000 m away after one tick at very high speed | One rebase per frame is enough: the new origin is the player position itself |
| `UniversalTime` very large (1e9 s) | `frac(t / T)` keeps orbit angles exact to < 1e-6 rad |
| `GetContent` for an unknown id | `KeyNotFoundException` (programming error, not content) |
| Story spawn count reaches 65,535 in one star system | `AddStorySpawn` throws `InvalidOperationException`; narrative effect logs the error |
| Save during a prefetch | Prefetches are not saved; nothing to complete |
| A `StarSystemTemplateDefinition` or pool used by procedural star systems is edited after release | Procedural star systems using it regenerate differently and old deltas may point at different bodies. Rule: after the first public release, never edit a shipped template's numbers or pools. Create a new template ID and keep the old one (content review checklist, SPEC-72/SPEC-81). Authored star systems are unaffected because their bodies are keyed by authored slots |

## 8. Performance budget

| Gate | Target | Measured by |
|---|---|---|
| Galaxy generation (1,000 procedural + 4 authored) | ≤ 50 ms, Burst (06 world gates) | `GalaxyPerfTests.Generate_1000` |
| One star-system content generation | ≤ 2 ms Burst, ≤ 0.5 ms main thread for delta apply | `GalaxyPerfTests.Content_Procedural` |
| Origin rebase with 5,000 roots | ≤ 0.5 ms total job time; 0 main-thread sync points | `OriginRebasePerfTests.Rebase_5000Roots` |
| Galaxy map refresh, 1,000 star systems | ≤ 1 ms, 0 B when unchanged | `GalaxyMapModelTests.Refresh_NoChange_NoAlloc` |

**Scale notes.**
- `StarSystemDef` is 48 B: 1,000 star systems are 48 KB, and 100,000 are 4.8 MB, the validator cap.
- Lanes average about 2.5 per star system.
- Content is generated per star system only when loaded (at most 2 cached, SPEC-31).
- The shift and sync jobs are O(roots). Bricks are children, so a 1M-cell station costs one root.
- No per-frame allocation: `RebaseDecision` uses `WorldUpdateAllocator`.

## 9. Tasks

### GAL-001 — Public contract types
- **Milestone:** M3
- **Tier:** standard — transcribes the exact code of §4
- **Depends on:** `CORE-001`, `DATA-001`, `VOX-001`, `NAR-001`
- **Do:** Create every public type of §4 (IDs, enums, definitions with fields and read-only properties, `GalaxyDef`, params structs, `StarSystemContent`, `OrbitElements`, `BodySpec`, deltas, `GalaxyState`, components, `OriginShifted` in `Game.Core/Events`, services and interfaces) with method bodies `throw new System.NotImplementedException()`. Register the `OriginShifted` channel (SPEC-02 §5.6 rule 3). Add `StarSystemMember` here (architecture §6.5).
- **Files:** `Assets/Game/Data/Definitions/World/*.cs`; `Assets/Game/Galaxy/{Data,Components,Services}/*.cs`; `Assets/Game/Core/Events/OriginShifted.cs`; `Assets/Game/Galaxy/AssemblyInfo.cs`; `Assets/Game/Presentation/Systems/ManagedEventChannels.cs` (one line)
- **Acceptance criteria:**
  - [ ] `tools/compile.sh` clean; every name in §4 exists with the stated namespace and file.
  - [ ] `StarSystemId`, `BodyId`, `BodySpec`, `StarSystemDef`, `LaneDef` are unmanaged (`UnsafeUtility.IsUnmanaged<T>()`).
  - [ ] `StarSystemMember` is an `ISharedComponentData`.
- **Tests:** `GalaxyContractTests.IdTypes_AreUnmanaged`, `.BodyId_LocalKey_RoundTrips`, `.StarSystemId_Authored_SetsHighBit`.

### GAL-002 — Orbit math
- **Milestone:** M3
- **Tier:** standard — closed-form formulas fully given in §5.5.5
- **Depends on:** `GAL-001`
- **Do:** Implement `OrbitMath` (Fixed, Circular, Elliptic with 6 Newton iterations, `frac`-based angles, analytic velocity, parent composition).
- **Files:** `Assets/Game/Galaxy/Data/OrbitMath.cs`; `Assets/Game/Tests/EditMode/Galaxy/OrbitMathTests.cs`
- **Acceptance criteria:**
  - [ ] Circular: position at `t = 0` equals `R(cos φ, 0, sin φ)`; after one period equals the start within 1e-6 m for R = 1e5 m.
  - [ ] Velocity equals the central finite difference `(P(t+h) − P(t−h)) / 2h` with h = 0.01 s within 1e-3 m/s for all modes.
  - [ ] Elliptic with e = 0.3: distance to the parent stays in `[a(1−e), a(1+e)]` over 100 samples.
  - [ ] At t = 1e9 s positions are finite and the circular error vs. a reference computed with `t mod T` is < 1e-3 m.
- **Tests:** `OrbitMathTests.Circular_PeriodReturnsToStart`, `.Velocity_MatchesFiniteDifference`, `.Elliptic_DistanceWithinApsides`, `.LargeTime_Stable`, `.Moon_ComposesParent`.

### GAL-003 — Definitions, properties and validator
- **Milestone:** M3
- **Tier:** standard — rule table GAL-E001…W003 is explicit
- **Depends on:** `GAL-001`
- **Do:** Implement the definitions' properties, `Validate` overrides and `GalaxyContentValidator` with every code of §5.9. Add `TestGalaxyFixtures` (in-memory definitions: `Small`, `Large`, `Disconnected`, `AuthoredOnly`).
- **Files:** `Assets/Game/Data/Definitions/World/*.cs`; `Assets/Game/Editor/Content/Validators/GalaxyContentValidator.cs`; `Assets/Game/Tests/EditMode/Galaxy/{TestGalaxyFixtures,GalaxyValidatorTests}.cs`
- **Acceptance criteria:**
  - [ ] Each code has one failing fixture producing exactly that code and one passing fixture.
  - [ ] The four fixtures validate with zero errors.
- **Tests:** `GalaxyValidatorTests.<Code>_Fails` and `.<Code>_Passes` for every code.

### GAL-004 — GalaxyDefBuilder and layout job
- **Milestone:** M3
- **Tier:** frontier — F3/F1: deterministic layout and structural IDs every save delta relies on
- **Depends on:** `GAL-003`
- **Do:** Implement `GalaxyDefBuilder` steps 1–5 and `GalaxyLayoutJob` (§5.2) with the spatial grid, fixed draw counts and weighted picks (§5.5.6). Implement `GalaxyDef` queries and `Dispose`.
- **Files:** `Assets/Game/Galaxy/Generation/{GalaxyDefBuilder,GalaxyLayoutJob,WeightedPick}.cs`; `Assets/Game/Galaxy/Data/GalaxyDef.cs`; `Assets/Game/Tests/EditMode/Galaxy/GalaxyLayoutTests.cs`
- **Acceptance criteria:**
  - [ ] Same seed → identical `StarSystems` array (bytewise) across two runs and across worker-count 1 vs max.
  - [ ] No two star systems closer than `minStarSystemSpacingLy`; all within `galaxyRadiusLy`.
  - [ ] Authored IDs have the high bit; procedural IDs are `1..accepted` without gaps.
  - [ ] Adding an extra weighted template entry with weight 0 changes nothing; changing an authored star system's danger changes no procedural star system.
  - [ ] All native memory freed by `Dispose` (leak detection on).
- **Tests:** `GalaxyLayoutTests.SameSeed_Identical`, `.Spacing_Respected`, `.Ids_Structural`, `.AuthoredEdit_DoesNotMoveProcedural`, `.Dispose_NoLeaks`.

### GAL-005 — Lane graph and galaxy hash
- **Milestone:** M3
- **Tier:** frontier — F3: graph algorithm whose ordering bugs silently change every procedural save
- **Depends on:** `GAL-004`
- **Do:** Implement `GalaxyLaneJob` (§5.3) and `GalaxyHash.Compute` (§5.4) including the golden file.
- **Files:** `Assets/Game/Galaxy/Generation/{GalaxyLaneJob,UnionFind,GalaxyHash}.cs`; `Assets/Game/Tests/EditMode/Galaxy/{GalaxyLaneTests,GalaxyHashTests}.cs`; `Assets/Game/Tests/EditMode/Galaxy/Golden/galaxy_v1.txt`
- **Acceptance criteria:**
  - [ ] Every star system is reachable from the start star system (BFS over `Adjacency`) for `Small`, `Large` and `Disconnected`.
  - [ ] Lanes sorted by `(A, B)`, `A < B`, no duplicates, authored lanes present exactly once with their flags.
  - [ ] Non-bridge procedural lanes ≤ `maxLaneLengthLy`; degree ≤ `maxLaneDegree` except for spanning-forest/bridge lanes (count reported).
  - [ ] `Hash` identical across runs and matches the golden file.
- **Tests:** `GalaxyLaneTests.AllReachable`, `.Sorted_NoDuplicates`, `.AuthoredLanes_Kept`, `.LengthAndDegree_Bounds`; `GalaxyHashTests.Deterministic`, `.MatchesGolden`.

### GAL-006 — Star-system content job
- **Milestone:** M3
- **Tier:** frontier — F3: draw-order discipline and structural BodyIds
- **Depends on:** `GAL-002`, `GAL-005`
- **Do:** Implement `StarSystemContentJob` (§5.5 steps 1–7), `StarSystemContent` (binary search, `GeneratedHash`, `Dispose`) and the content hash.
- **Files:** `Assets/Game/Galaxy/Generation/{StarSystemContentJob,StarSystemContentHash}.cs`; `Assets/Game/Galaxy/Data/StarSystemContent.cs`; `Assets/Game/Tests/EditMode/Galaxy/StarSystemContentTests.cs`
- **Acceptance criteria:**
  - [ ] Same inputs → identical `Bodies` and `GeneratedHash`.
  - [ ] Bodies sorted by `LocalKey`; every planet orbit radius strictly increases with slot; moons orbit their planet.
  - [ ] Stations/POIs keep ≥ `MinBodySeparationM` from each other when the 8 candidates allow it (fixture with ample space).
  - [ ] One gate per adjacent lane at `LaneRingRadiusM`, pairwise arc distance ≥ `MinBodySeparationM` for a fixture with 6 lanes in a 30° cone.
  - [ ] Authored star system output equals the authored data (radii, phases, keys).
  - [ ] Releasing and regenerating the same star system 100 times gives the same `GeneratedHash` (no hidden state).
- **Tests:** `StarSystemContentTests.Deterministic`, `.Sorted_ByLocalKey`, `.Orbits_Increasing`, `.Gates_OnRing_Separated`, `.Authored_MatchesData`, `.Regenerate_NoHiddenState`.

### GAL-007 — StarSystemDelta, GalaxyState and the applier
- **Milestone:** M3
- **Tier:** frontier — F3: delta ordering is what makes save → load → save byte-identical (R9)
- **Depends on:** `GAL-006`
- **Do:** Implement `StarSystemDelta` (sorted inserts with binary search, append-only story spawns), `GalaxyState`, `StarSystemDeltaApplier` (§5.6).
- **Files:** `Assets/Game/Galaxy/Data/{StarSystemDelta,GalaxyState,StructureDiffRecord}.cs`; `Assets/Game/Galaxy/Generation/StarSystemDeltaApplier.cs`; `Assets/Game/Tests/EditMode/Galaxy/{StarSystemDeltaTests,DeltaApplierTests}.cs`
- **Acceptance criteria:**
  - [ ] Lists stay sorted and unique under random insert order (100 seeded permutations).
  - [ ] Applying deltas never changes `GeneratedHash` and never changes a generated body's `Id`, orbit or seed except `TemplateIndex` for replacements.
  - [ ] Destroying body X does not change any other body's `BodySpec` (R9 / 03-D3).
  - [ ] Story spawn slots are stable after a removal.
- **Tests:** `StarSystemDeltaTests.Sorted_UnderRandomOrder`, `.StorySpawn_RemovalKeepsSlots`; `DeltaApplierTests.Generated_Unchanged`, `.Destroy_IsolatesBody`, `.Replacement_SetsTemplate`, `.UnresolvedTemplate_Reported`.

### GAL-008 — GalaxyService, ticker and bridge system
- **Milestone:** M3
- **Tier:** frontier — F1/F5: lifecycle used by new game, load, streaming and travel
- **Depends on:** `GAL-007`, `FND-001`
- **Do:** Implement `GalaxyService` (§5.8, §6 including `GetStartLocation`), names (§5.7), `GalaxyFrameTicker`, `GalaxyServicesBridgeSystem`.
- **Files:** `Assets/Game/Galaxy/Services/{GalaxyService,GalaxyFrameTicker,GalaxyNames}.cs`; `Assets/Game/Presentation/World/GalaxyServicesBridgeSystem.cs`; `Assets/Game/Tests/EditMode/Galaxy/GalaxyServiceTests.cs`
- **Acceptance criteria:**
  - [ ] `NewGame` then `OnSessionRestored` with the same state → identical `Galaxy.Hash`; `GenerationCount == 2`.
  - [ ] `GetContent` twice returns the same instance; `ContentGenerationCount == 1` (R9 EditMode part).
  - [ ] Prefetch + `GetContent` equals synchronous `GetContent` byte for byte.
  - [ ] Delta writes on a cached star system update the cached `BodySpec` and call the sink exactly once.
  - [ ] Ticker calls services in `TickOrder`; an exception in one does not stop the others.
- **Tests:** `GalaxyServiceTests.Restore_SameHash`, `.Content_GeneratedOnce`, `.Prefetch_EqualsSync`, `.DeltaWrite_PatchesCacheAndNotifies`, `.Names_Format`, `.Ticker_OrderAndIsolation`.

### GAL-009 — OriginRebaseSystem
- **Milestone:** M3
- **Tier:** frontier — F1/F2/F5: single floating-origin owner, parallel jobs, ordering with streaming, camera and VFX
- **Depends on:** `GAL-001`, `SPEC-02`
- **Do:** Implement `OriginRebaseSystem` (§5.10, including `PlaceJob` and the `OriginShiftRecord` write) and both `GalaxyService.RequestOriginReset` overloads.
- **Files:** `Assets/Game/Galaxy/Systems/OriginRebaseSystem.cs`; `Assets/Game/Tests/EditMode/Galaxy/OriginRebaseSystemTests.cs`; `Assets/Game/Tests/PlayMode/Galaxy/OriginRebasePerfTests.cs`
- **Acceptance criteria:**
  - [ ] Player at local x = 2,001 → after one update local ≈ 0, `FrameOrigin.OriginInStarSystem.x` increased by 2,001, `ShiftSequence` +1, one `OriginShifted` with `Delta = (−2001, 0, 0)`.
  - [ ] Roots with `UniversePosition` keep `UniversePosition` unchanged (exact double equality) across 1,000 rebases while moving 1,500 m per rebase.
  - [ ] Child entities (bricks) are not modified.
  - [ ] Request with a different star system: all `UniversePosition` roots placed at `UP − NewOrigin`, `StarSystemChanged = 1`, `Delta = 0`.
  - [ ] `UniversePositionIsAnalytic` roots are not synced.
  - [ ] Request with placement (different star system): the player root ends at `Placement.PositionM − NewOrigin` with yaw `Placement.YawRad`; its linear velocity keeps its magnitude and its angle relative to the ship's forward axis; its angular velocity is unchanged.
  - [ ] Same-star-system request with `Delta = d`: `LastShiftDelta == d`, `OriginShifted.Delta == d`, `OriginShiftRecord == { ShiftSequence, d, 0 }`, and non-`UniversePosition` roots move by `d`.
  - [ ] `RequestOriginReset` returns increasing serials; `FrameOrigin.AppliedRequestSerial` equals the returned serial after the next update.
  - [ ] No main-thread `Complete()` (verified by the test running the system with a pending writer job on `LocalTransform` and asserting no `InvalidOperationException`).
  - [ ] Perf: 5,000 roots ≤ 0.5 ms.
- **Tests:** `OriginRebaseSystemTests.Placement_PoseAndVelocity`, `.SameStarSystemRequest_ShiftsConsumers`, `.Record_MirrorsFrameOrigin`, `.Serials_Returned`, `OriginRebaseSystemTests.Threshold_Rebases`, `.UniversePosition_ExactAcrossRebases`, `.Children_Untouched`, `.Request_ChangesStarSystem`, `.Analytic_NotSynced`, `.Smoothing_HistoryShifted`; `OriginRebasePerfTests.Rebase_5000Roots`.

### GAL-010 — GLXY save participant
- **Milestone:** M3
- **Tier:** frontier — F3: binary format with versioning and round-trip guarantee
- **Depends on:** `GAL-007`, `PERS-001`
- **Do:** Implement `GalaxySaveParticipant` (§5.11); register it in `SaveChunkRegistry` row `GLXY` (SPEC-05 §4.8 already lists it). Write the v1 fixture with `SaveFixtureHarness.WriteFixture`.
- **Files:** `Assets/Game/Galaxy/Services/GalaxySaveParticipant.cs`; `Assets/Game/Tests/EditMode/Galaxy/GalaxySaveParticipantTests.cs`; fixture under `Assets/Game/Tests/EditMode/Galaxy/Fixtures/`
- **Acceptance criteria:**
  - [ ] Capture → Restore → Capture produces identical bytes for a state with 50 deltas, 10 structures, removed story spawns and unresolved IDs.
  - [ ] Restore into a non-empty state fully replaces it (no leftovers); structures from the previous state are disposed.
  - [ ] A corrupted VOXS block drops only that record and logs one warning.
- **Tests:** `GalaxySaveParticipantTests.RoundTrip_ByteIdentical`, `.Restore_ReplacesState`, `.CorruptStructure_DropsRecord`, `.Restore_FromV1_Fixture`.

### GAL-011 — Galaxy map model
- **Milestone:** M3
- **Tier:** standard — visibility rules and change stamp fully given in §5.12
- **Depends on:** `GAL-008`
- **Do:** Implement `GalaxyMapModel` (§5.12).
- **Files:** `Assets/Game/Galaxy/Services/GalaxyMapModel.cs`; `Assets/Game/Tests/EditMode/Galaxy/GalaxyMapModelTests.cs`
- **Acceptance criteria:**
  - [ ] Unknown star systems never appear; hidden lanes appear only when both ends are visited.
  - [ ] `Refresh` without changes allocates 0 bytes (`GC.GetAllocatedBytesForCurrentThread`) and keeps `Revision`.
- **Tests:** `GalaxyMapModelTests.UnknownHidden`, `.HiddenLane_RevealedByVisits`, `.Refresh_NoChange_NoAlloc`, `.Revision_ChangesOnVisit`.

### GAL-012 — M3 galaxy content
- **Milestone:** M3
- **Tier:** standard — content authoring from the vision's star-system list with given definitions
- **Depends on:** `GAL-003`, `SPEC-72`
- **Do:**
  1. Create `galaxy_main` (fixed seed, `proceduralStarSystemCount = 0`, start star system `starsys_ark_start`, `startBodyKey = "ark_station"`) and the default gate templates (`defaultHyperlaneGate` = `body_jump_point`, `defaultAncientGate` = `body_gate_active`, both role `LaneGate`).
  2. Create exactly the three authored star systems, their bodies and the two lanes of SPEC-42 §4.12 (IDs with the `starsys_` prefix: `starsys_ark_start`, `starsys_drift`, `starsys_whisper`; positions and danger as listed there). The `ark_start ↔ whisper` lane has `condition` source `$gate_ark_start_active`, `lockedMessage` "The gate is dormant." and `gateTemplateA`/`gateTemplateB` = `body_gate_dormant`.
  3. Give every body template the `storyTag` of SPEC-42 §4.10 (for example `body_resource_cache` → `resource_cache`, `body_derelict_ship` → `derelict`). Templates that SPEC-72 MIG-014 already created as stubs are completed here, not duplicated.
- **Files:** `Assets/Game/Content/Galaxy/*.asset`; `Assets/Game/Content/Bodies/*.asset`
- **Acceptance criteria:**
  - [ ] Content validates with zero GAL errors; `GalaxyService.NewGame` succeeds in a PlayMode smoke test.
  - [ ] The lane `ark_start ↔ whisper` is closed on a new game and open after `set_fact gate_ark_start_active true` (through SPEC-32's `ILaneAccess` once TRV-003 exists).
  - [ ] `TryResolveBody(starsys_ark_start, "ark_station")` and `TryResolveBody(starsys_drift, "drift_cache")` succeed.
- **Tests:** `GalaxyContentSmokeTests.MainGalaxy_GeneratesAndValidates`, `.OpeningChapter_BodiesResolve`, `.WhisperLane_GatedByFact` (PlayMode).

### GAL-013 — Performance gates
- **Milestone:** M3
- **Tier:** standard — measurement of already-specified code with the SPEC-71 perf helpers
- **Depends on:** `GAL-005`, `GAL-006`, `GAL-009`, `GAL-011`, `TST-001`
- **Do:** Add the perf tests of §8 and record results in the ledger *Measurements*.
- **Files:** `Assets/Game/Tests/PlayMode/Galaxy/GalaxyPerfTests.cs`
- **Acceptance criteria:**
  - [ ] All §8 targets met on the reference machine with safety checks off.
- **Tests:** `GalaxyPerfTests.Generate_1000`, `.Content_Procedural`.

### GAL-014 — Brick-level structure diffs
- **Milestone:** M4
- **Tier:** frontier — F3: partial VOXS encoding against a regenerated base structure
- **Depends on:** `GAL-010`
- **Do:** Replace full structures in `StructureDiffRecord` with Partial VOXS (`VoxsSource.Partial = true`): only bricks whose cells differ from the regenerated base structure plus `RemovedBricks`. Bump `GLXY` to version 2 with a v1 reader kept.
- **Files:** `Assets/Game/Galaxy/Data/StructureDiffRecord.cs`; `Assets/Game/Galaxy/Services/GalaxySaveParticipant.cs`; tests
- **Acceptance criteria:**
  - [ ] A 1M-cell station with a 2,000-cell cut saves ≤ 40 KB of GLXY payload before container compression.
  - [ ] v1 fixture still restores.
- **Tests:** `GalaxySaveParticipantTests.PartialDiff_Small`, `.Restore_FromV1_Fixture`.

## 10. Open questions

| # | Question | Default if unanswered | Decider |
|---|---|---|---|
| 1 | Should procedural star systems exist in v1 at all? | Generator built and tested at 1,000; M3 content uses 0 | Owner |
| 2 | Names for procedural star systems | `"<prefix> <letter>-<NNNN>"` (§5.7) | Owner |
| 3 | Do destroyed celestial bodies leave wrecks? | No: SPEC-31 spawns nothing for `Destroyed` | Owner |
| 4 | Galaxy size for M4 | `galaxyRadiusLy` 60, 200 star systems | Owner |
| 5 | Condition shape for gated lanes | **Answered:** SPEC-40 `CompiledCondition` with the SPEC-40 text syntax (`$gate_ark_start_active`) | SPEC-40 |
| 15 | Non-destructible bodies | `destructible = false` → SPEC-13 `DamageImmune` tag on the grid | SPEC-13 |
| 16 | §4.6 spells the structure type `Game.Voxel.VoxelStructure`, but SPEC-10 decision 17 / §4.3 and the existing implementation place it in `Game.Voxel.Model`. | Use `Game.Voxel.Model.VoxelStructure` in `StructureDiffRecord.Structure` and `StarSystemDelta.SetStructure`; preserve the existing owning type. | Tech lead — resolved by GAL-001 transcription against the owning contract; central mirror pending. |
| 17 | §4.8's readonly frame context (and §5.12's readonly map entries referenced there) omit constructors, so callers cannot supply non-default values. | Add constructors assigning every stated readonly field, in declaration order, without changing fields or adding behavior. | Tech lead — resolved by GAL-001; central mirror pending. |
| 18 | §4.8 omits `GalaxyService.GetStartLocation`, although §6 and SPEC-01's new-game flow require it. | Include the stated `PlayerLocation GetStartLocation()` contract, with a GAL-008 throwing stub; no start-position computation in GAL-001. | Tech lead — resolved by GAL-001 against the existing public API; central mirror pending. |

## 11. References

- Audit: `docs/reference/audit-2026-10/03-galaxy-world.md` (03-B1, 03-B9a–d, 03-D3), `04` (04-B1, 04-B11), `LEDGER.md` R9.
- Architecture §6.5, §9; conventions §2.10; traps T5.1–T5.2.
- Unity Physics `PhysicsGraphicalInterpolationBuffer` (graphical smoothing).
- Kruskal's algorithm; Kepler's equation (Newton iteration).
