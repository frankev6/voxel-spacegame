# SPEC-33 — Encounters, factions and territory

| | |
|---|---|
| **Status** | Draft |
| **Area code** | ENC (task IDs are `ENC-001`, `ENC-002`, …) |
| **Module(s)** | `Game.Simulation` (folder `Assets/Game/Simulation/Factions/`: faction ECS state and its system, `EncounterTag`); `Game.Galaxy` (folder `Assets/Game/Galaxy/Encounters/` and `Assets/Game/Galaxy/Services/`: director, faction and territory services, save participants); `Game.Data` (`Assets/Game/Data/Definitions/Factions/`, `Assets/Game/Data/Definitions/Encounters/`); `Game.Core` (event payloads); `Game.App` (narrative wiring, dev commands); `Game.Editor` (legacy converter, validators) |
| **Depends on specs** | SPEC-21 (`Allegiance`, `FactionRelations`, `FactionStance`); SPEC-23 (`AiSetup`, `AiProfileDefinition`, `AiBrain`, `PlayerCombatStatus`, `FleetMember`/`FormationDefinition` in M4); SPEC-13 (`Hulk`, `GridDamageStats`); SPEC-30 (`GalaxyDef`, `StarSystemDef.Danger`/`Seed`/`Flags`, `StarSystemContent`, `OrbitMath`, `IStarSystemOwnership`, `GalaxyFrameTicker`, `RngPurpose` values); SPEC-31 (`IShipSpawner`, `GalaxyShipSpawnArgs`, `StarSystemEntered`, `StarSystemLoader.IsReady`, `Interactable`, `DockingService`); SPEC-32 (`TravelService.Phase`); SPEC-40 (`CompiledCondition`, `LogicUsage.EncounterCondition`, `ILogicSourceOwner`, `ILogicContext`, `LogicWorldAdapter`); SPEC-50 (`LootTableDefinition`, `ILogicEconomy.GiveLoot`); SPEC-02 (`RngStreams`, `StableHash`, `EventBus<T>`, `ManagedEventQueue<T>`, `ISaveParticipant`, `INewGameInitializer`); SPEC-05 (`FACN`, `ENCT` chunks); SPEC-72 (legacy hand-off) |
| **Depended on by** | SPEC-21 (fills `FactionRelations`); SPEC-23 (encounter ships and profiles); SPEC-30 (`IStarSystemOwnership` for the map); SPEC-31 (station allegiance); SPEC-40 (relations, `SpawnEncounter`, `StartCombat`, `EncounterDefeated`, `FactionRelationChanged`); SPEC-42 (story encounters, hails); SPEC-61 (ally/enemy colours, hail prompt); SPEC-72 (`FactionDefinition` for MIG-012); SPEC-80 (`factionId` for liveries); SPEC-70 (dev commands) |
| **Milestone** | M3 (fleet formations and procedural territory: M4) |

> Read [01-architecture.md](../01-architecture.md) (§5, §6.4–§6.6, §8, §11), [02-conventions.md](../02-conventions.md), [SPEC-21](SPEC-21-weapons.md) §4.3, [SPEC-23](SPEC-23-ai.md) §3–§5 and [SPEC-31](SPEC-31-star-system-streaming.md) §4.4 before this spec. This spec may not contradict them.

## 1. Purpose

This spec decides who is in space with the player and how they feel about each other. **Factions** are content (`FactionDefinition`): each gets a stable runtime index used by `Allegiance.Faction`, a static stance toward the other factions, and a saved relation with the player from −100 to 100. The relation turns into Hostile, Neutral or Friendly through thresholds and is written into SPEC-21's `FactionRelations` matrix by one fixed-step system, so weapons, targeting and AI see hostility tick-exactly. Shooting a neutral faction's ships or stations provokes it. **Encounters** are content (`EncounterDefinition`): a faction, a group of ships with AI profiles, placement and lifetime rules. The **encounter director** spawns them near the player on a seeded schedule driven by the star system's danger, and narrative spawns them on demand (`spawn_encounter`, `start_combat`). It tracks every instance until it is defeated or despawned and raises `EncounterDefeated` for the narrative. **Territory** says which faction owns a star system (authored in M3, flood-filled over procedural star systems in M4), which gives stations their allegiance and the map its colours. The old prototype had encounters as scattered NPC fleets with chase flags, factions that only coloured names, and no defeat tracking (audit 05 §encounters). Here everything goes through the director, and every hostile is hostile through one matrix.

## 2. Scope

**In scope:**
- `FactionDefinition`, `EncounterDefinition`, `EncounterConfigDefinition` and their validators.
- Faction indices, the faction ECS singleton (`FactionStanding`, `FactionRelationRequest`), `FactionStandingSystem` (sole writer of `FactionRelations` after boot), provocation, `FactionRelationChanged`.
- `FactionService` (managed relations API, `FACN` save chunk, new-game initial relations).
- `EncounterDirectorService`:
  - director rolls;
  - narrative spawn and combat requests;
  - instance tracking, defeat, despawn;
  - pending story encounters across star systems;
  - the `ENCT` save chunk;
  - hails on encounter ships.
- `EncounterPlacement` (anchor resolution, positions, clearance) and fleet composition.
- `TerritoryService` (`IStarSystemOwnership`): authored ownership (M3), procedural territory and `TerritoryEntered` (M4).
- The legacy converter of fleets into encounters, and M3 faction/encounter content.
- Narrative wiring of SPEC-40's relation and encounter members; dev commands and probes.

**Out of scope (and who owns it instead):**
- Target choice, steering, firing: SPEC-23 and SPEC-21 (they read `FactionRelations`).
- Spawning grids: SPEC-01 `ShipSpawnService` through SPEC-31's `IShipSpawner`.
- Station grids: SPEC-31 (asks `IStarSystemOwnership` for their allegiance).
- Dialog on hail and story beats: SPEC-41/SPEC-42 (they look instances up through §6).
- `ShipDestroyed` and its bridge: SPEC-21 (`_INTEGRATION-TODO` item 68). This spec only tags encounter ships.
- Loot rolls and salvage: SPEC-50.
- Fleet steering in formation: SPEC-23 (M4).

## 3. Decisions (binding)

1. **Faction indices are runtime-only and deterministic.**
   - Index 0 is the player faction (exactly one definition with `isPlayerFaction`; content `faction_ark`). The other factions follow in `ContentCatalog.All<FactionDefinition>()` order (ordinal ID), as indices 1…N−1 with N ≤ 64.
   - `Allegiance.Faction` uses these indices; 255 = none (SPEC-21).
   - Indices are never saved. Saves key relations by `ContentId`.

   Reason: adding a faction must not corrupt saves.
2. **Two kinds of stance.**
   - **Faction↔faction** stances are static content: symmetric, default Neutral, diagonal Friendly.
   - **Player↔faction** stance comes from the saved relation: `relation ≤ hostileAtOrBelow` (−25) → Hostile; `relation ≥ friendlyAtOrAbove` (50) → Friendly; otherwise Neutral.
   - Factions with `relationLocked` keep their base relation forever (raiders and xeno are always Hostile).

   Reason: simple, readable diplomacy that the narrative can move.
3. **`FactionStandingSystem` is the only writer of `FactionRelations` after boot, and of the standings buffer.**
   - It runs in the fixed step (`GameplayPrePhysicsGroup`, before `ShipCommandGroup`).
   - It applies relation requests and provocation, and writes changed matrix entries in a job.
   - The job is ordered through a read-write `ComponentLookup<FactionRelations>`, so all earlier readers are in its dependency and later readers depend on it. There is no main-thread sync and no `GetSingletonRW`.
   - Managed code only appends `FactionRelationRequest`s, outside the fixed step; they apply next tick.

   Reason: architecture rule 3; hostility flips at a tick boundary every system agrees on.
4. **Provocation.**
   - When a grid of faction `f` (not none, not the player faction, not currently Hostile, not locked) records damage whose source was the player ship (`GridDamageStats.LastDamageInstigator`, item 69 and review X6: the player's craft count as the player), relation `f` drops by `provokePenalty` (10).
   - The drop happens at most once per `provokeIntervalTicks` (60) per faction.

   Reason: friendly fire is on (SPEC-21), so shooting a neutral station must have consequences. Sustained fire turns a neutral faction hostile in about 3 s.
5. **The director is seeded and game-time based.**
   - Rolls happen at `UniversalTime` thresholds: first roll after `firstRollDelaySeconds` (90), then every `rollIntervalSeconds` (120).
   - RNG is `RngStreams.For(StableHash.Combine(starSystemSeed, visitCount), RngPurpose.EncounterDirector, rollIndex)`.
   - Composition uses `EncounterFleet` and placement uses `EncounterPlacement`, both seeded per instance.

   Pause, dialogs and jumps never advance the schedule. The same visit with the same choices spawns the same encounters.
6. **Spawns are frame-granular.**
   - The director runs on the main thread in `GalaxyFrameTicker` (`TickOrder` 300) and spawns through `IShipSpawner`. SPEC-10 grids appear on the next `GridSpawnSystem` frame.
   - SPEC-71 replay tests disable the director (`Enabled = false`).

   Reason: all grid spawning is per frame (SPEC-10). Replays stay deterministic by containing no spawns.
7. **Instances are tracked by member `PersistentId`.**
   - Each frame the director sweeps its members (at most 40 ships) through `IShipSpawner.TryGetRoot` and `Hulk`.
   - An instance is **defeated** when every member is a hulk or gone. It is **despawned** when the director removes it.

   Reason: no dependency on event delivery order; O(members).
8. **Story encounters persist until defeated.**
   - Instances created by narrative (`SpawnEncounter`, `StartCombat`) or by definitions with `persistUntilDefeated` are never despawned for distance.
   - If the player leaves their star system or saves before defeating them, they become **pending**, keyed by star system, and respawn on the next entry. They are saved in `ENCT`.

   Reason: story progress never depends on the player staying in place. Combat itself cannot be saved (SPEC-23 `PlayerCombatStatus` blocker).
9. **Caps.**
   - At most `maxInstances` (16) live instances and `maxShipsAlive` (40) living encounter ships.
   - The director never exceeds them; narrative requests are clamped with a warning.

   Reason: the 06 combat gate is 30 ships at 60 FPS, plus margin for stations.
10. **Territory.**
    - In M3, ownership is authored (`FactionDefinition.ownedStarSystems`; at most one owner per star system).
    - In M4, procedural star systems are flood-filled once per galaxy build from seeded faction centres.
    - Stations spawn with the owner's allegiance (`Team` 0); a star system without an owner gives `Allegiance.None` (255).
11. **Events** (`Game.Core.Events`):
    - `FactionRelationChanged`: ECS-produced, unmanaged, registered channel.
    - `EncounterDefeated`: managed.
    - `TerritoryEntered`: managed, M4.
12. **Folders and namespaces:**
    - `Assets/Game/Simulation/Factions/` → `Game.Simulation.Factions`;
    - `Assets/Game/Galaxy/Encounters/` → `Game.Galaxy.Encounters`;
    - services in `Assets/Game/Galaxy/Services/` → `Game.Galaxy.Services`;
    - definitions in `Game.Data.Definitions`.

## 4. Data model

All types are **public contract** unless marked *internal*.

### 4.1 Definitions (`Game.Data.Definitions`)

Property rule as in SPEC-30 §4.3: each serialized field has a read-only PascalCase property; lists are `IReadOnlyList<T>`.

```csharp
// Assets/Game/Data/Definitions/Factions/
[System.Serializable] public struct FactionStanceEntry { public FactionDefinition other; public FactionStanceValue stance; }
public enum FactionStanceValue : byte { Neutral = 0, Friendly = 1, Hostile = 2 }   // mirror of Game.Simulation.FactionStance; values equal (test)

[System.Serializable] public struct DoctrineEntry
{
    public ShipBlueprintDefinition blueprint;
    public float weight;                         // > 0
    public AiProfileDefinition aiProfile;        // null = the faction's defaultAiProfile
}

[ContentIdPrefix("faction_")]
[UnityEngine.CreateAssetMenu(menuName = "Game/Content/Faction", fileName = "faction_new")]
public sealed class FactionDefinition : ContentDefinition
{
    [SerializeField] private string displayName;
    [SerializeField, UnityEngine.TextArea(2, 5)] private string description;
    [SerializeField] private bool isPlayerFaction;                 // exactly one (faction_ark)
    [SerializeField] private string shipNamePrefix;                // e.g. "VSS"; shown before blueprint names (SPEC-61)
    [SerializeField] private string mapColorToken = "--color-faction-neutral";  // USS token for map/HUD (SPEC-61)
    [SerializeField] private UnityEngine.Color liveryColor = UnityEngine.Color.gray;  // art data for SPEC-80 liveries
    [SerializeField, UnityEngine.Range(-100, 100)] private int basePlayerRelation;
    [SerializeField] private bool relationLocked;                  // relation never changes (raiders, xeno)
    [SerializeField] private System.Collections.Generic.List<FactionStanceEntry> stances = new();   // toward non-player factions
    [SerializeField] private System.Collections.Generic.List<DoctrineEntry> doctrine = new();
    [SerializeField] private AiProfileDefinition defaultAiProfile; // null = AiConfig default
    [SerializeField] private System.Collections.Generic.List<AuthoredStarSystemDefinition> ownedStarSystems = new(); // M3 territory
    // M4 procedural territory
    [SerializeField, UnityEngine.Range(0, 64)] private int territorySeeds;        // 0 = no procedural territory
    [SerializeField, UnityEngine.Range(0, 8)] private int territoryRadiusHops = 2;
}

// Assets/Game/Data/Definitions/Encounters/
public enum EncounterKind : byte { Patrol = 0, Ambush = 1, Merchant = 2, Story = 3 }   // add at end only

[System.Serializable] public struct EncounterShipEntry
{
    public ShipBlueprintDefinition blueprint;
    public AiProfileDefinition aiProfile;         // null = faction default
    public int countMin, countMax;               // 1..10, min ≤ max
}

[ContentIdPrefix("encounter_")]
[UnityEngine.CreateAssetMenu(menuName = "Game/Content/Encounter", fileName = "encounter_new")]
public sealed class EncounterDefinition : ContentDefinition, Game.Data.Logic.ILogicSourceOwner
{
    [SerializeField] private string displayName;
    [SerializeField] private FactionDefinition faction;
    [SerializeField] private EncounterKind kind;
    [SerializeField] private System.Collections.Generic.List<EncounterShipEntry> ships = new();
    [SerializeField] private bool useFactionDoctrine;             // ships come from the faction doctrine instead of `ships`
    [SerializeField] private int doctrineMinShips = 2, doctrineMaxShips = 3;   // 1..10
    [SerializeField] private FormationDefinition formation;       // M4; null = line abreast (§5.6)
    // Director
    [SerializeField, UnityEngine.Min(0)] private float directorWeight;          // 0 = never rolled by the director
    [SerializeField, UnityEngine.Range(0, 3)] private int minDanger, maxDanger = 3;
    [SerializeField] private bool uniquePerVisit;                 // at most once per star-system visit
    [SerializeField] private bool uniqueGlobal;                   // at most once per campaign (director only)
    [SerializeField] private Game.Data.Logic.CompiledCondition condition = new();   // LogicUsage.EncounterCondition; empty = always
    // Placement
    [SerializeField] private float spawnDistanceMinM = 1500f, spawnDistanceMaxM = 2500f;  // from the anchor (surface for body anchors)
    [SerializeField, UnityEngine.Min(0)] private float shipSpacingM;            // 0 = automatic (§5.6)
    // Lifetime and interaction
    [SerializeField] private bool persistUntilDefeated;           // director instances of this definition behave like story instances
    [SerializeField] private bool hailable;                       // leader gets Interactable(Hail) (§5.9)
    [SerializeField] private LootTableDefinition defeatLoot;      // SPEC-50; null = none
    // ILogicSourceOwner: compiles/binds `condition` with LogicUsage.EncounterCondition, field "condition".
}

// Assets/Game/Data/Definitions/Encounters/EncounterConfigDefinition.cs — single asset config_encounters
[ContentIdPrefix("config_")]
public sealed class EncounterConfigDefinition : ContentDefinition
{
    // Director
    [SerializeField] private float firstRollDelaySeconds = 90f;
    [SerializeField] private float rollIntervalSeconds = 120f;
    [SerializeField] private float[] dangerSpawnChance = { 0f, 0.25f, 0.45f, 0.65f };   // index = StarSystemDef.Danger
    [SerializeField] private int maxActiveDirectorInstances = 2;      // per star system
    [SerializeField] private float minPlayerDistanceM = 800f;         // director and narrative spawns, except StartCombat
    [SerializeField] private float combatMinDistanceM = 300f;         // StartCombat
    [SerializeField] private float arrivalGraceSeconds = 10f;         // after StarSystemEntered(Jump): no director roll
    [SerializeField] private float arrivalGraceDistanceM = 2000f;     // … and no director spawn closer than this to the arrival point
    [SerializeField] private int placementTries = 12;
    [SerializeField] private float placementStepDegrees = 30f;
    [SerializeField] private float bodyClearanceM = 150f;
    // Lifetime
    [SerializeField] private float despawnDistanceM = 6000f;
    [SerializeField] private float despawnDelaySeconds = 30f;
    [SerializeField] private int maxInstances = 16;
    [SerializeField] private int maxShipsAlive = 40;
    // Relations
    [SerializeField] private int hostileAtOrBelow = -25;
    [SerializeField] private int friendlyAtOrAbove = 50;
    [SerializeField] private int provokePenalty = 10;
    [SerializeField] private int provokeIntervalTicks = 60;
    // Hail
    [SerializeField] private float hailDistanceM = 300f;
}
```

Validation codes:

| Code | Rule |
|---|---|
| ENC-E001 [E] | Exactly one `FactionDefinition` with `isPlayerFaction`; its `relationLocked == true` |
| ENC-E002 [E] | At most 64 factions |
| ENC-E003 [E] | Stance entries: `other` set, not self, not the player faction; no pair with two different stances (A→B and B→A included) |
| ENC-E004 [E] | Doctrine entries: blueprint set, `weight > 0` |
| ENC-E005 [E] | Each `AuthoredStarSystemDefinition` is owned by at most one faction |
| ENC-E006 [E] | `mapColorToken` starts with `--color-` |
| ENC-E010 [E] | Encounter `faction` set |
| ENC-E011 [E] | `ships` non-empty, or `useFactionDoctrine` with a non-empty faction doctrine |
| ENC-E012 [E] | `1 ≤ countMin ≤ countMax ≤ 10` per entry; `1 ≤ doctrineMinShips ≤ doctrineMaxShips ≤ 10`; maximum total ships ≤ 10 |
| ENC-E013 [E] | `0 ≤ minDanger ≤ maxDanger ≤ 3`; `directorWeight ≥ 0` |
| ENC-E014 [E] | `100 ≤ spawnDistanceMinM ≤ spawnDistanceMaxM ≤ 10000` |
| ENC-E015 [E] | Kind `Merchant`: every resolved AI profile has role `Civilian` |
| ENC-E020 [E] | Exactly one `EncounterConfigDefinition` (`config_encounters`); `dangerSpawnChance.Length == 4`, each in [0, 1]; `hostileAtOrBelow < friendlyAtOrAbove`; positive intervals, distances and caps; `maxShipsAlive ≤ 60` |
| ENC-W001 [W] | Director encounter (weight > 0) of kind `Ambush` or `Patrol` whose faction is not Hostile at its base relation |
| ENC-W002 [W] | `StartCombat` target (any `Story` encounter) whose faction is not Hostile at base relation |

### 4.2 Faction ECS state (`Assets/Game/Simulation/Factions/`, namespace `Game.Simulation.Factions`)

```csharp
namespace Game.Simulation.Factions
{
    using Unity.Entities; using Game.Core;

    /// <summary>One element per faction index (0 = player faction). On the faction singleton. Only writer: FactionStandingSystem
    /// (and FactionContent at boot / FactionService during restore and new game, outside gameplay ticks).</summary>
    [InternalBufferCapacity(0)]
    public struct FactionStanding : IBufferElementData
    {
        public ContentHash Faction;
        public int Relation;               // −100..100 (player faction: 100)
        public byte Locked;                // 1 = relationLocked
        public FactionStance PlayerStance; // stance between the player faction and this faction (Friendly for index 0)
        public ushort Reserved;
        public uint LastProvokedTick;
    }

    /// <summary>Pending relation changes from managed code. Appended only on the main thread outside the fixed step (FactionService);
    /// consumed and cleared by FactionStandingSystem.</summary>
    [InternalBufferCapacity(0)]
    public struct FactionRelationRequest : IBufferElementData
    {
        public byte Faction;               // index
        public byte Mode;                  // 0 = add Value, 1 = set to Value
        public ushort Reserved;
        public int Value;
    }

    /// <summary>On the faction singleton. Thresholds from config_encounters. Revision +1 whenever any standing changes.</summary>
    public struct FactionState : IComponentData
    {
        public int HostileAtOrBelow, FriendlyAtOrAbove, ProvokePenalty, ProvokeIntervalTicks;
        public int Count;                  // number of factions (== FactionRelations.Count)
        public uint Revision;
    }

    /// <summary>On encounter ship roots (added by the director right after IShipSpawner.Spawn). Read by SPEC-21's ShipDestroyed bridge.</summary>
    public struct EncounterTag : IComponentData { public ContentHash Encounter; public uint Instance; }

    /// <summary>Boot factory (SPEC-01 BootSingletons, before WeaponContent): builds the index map, FactionRelations (Count = N),
    /// FactionStanding (base relations), FactionRelationRequest (empty), FactionState.</summary>
    public static class FactionContent
    {
        public static void CreateSingletons(EntityManager em, Game.Data.Services.ContentCatalog catalog,
                                            Game.Data.Definitions.EncounterConfigDefinition config, System.Collections.Generic.List<Entity> created);
    }

    /// <summary>Deterministic faction index order (decision 1). Built once from the catalog; immutable.</summary>
    public sealed class FactionIndexMap
    {
        public static FactionIndexMap Build(Game.Data.Services.ContentCatalog catalog);
        public int Count { get; }
        public byte IndexOf(Game.Data.Definitions.FactionDefinition def);       // 255 when null or not in the catalog
        public byte IndexOf(in ContentHash faction);                            // 255 when unknown
        public Game.Data.Definitions.FactionDefinition ByIndex(byte index);     // null when out of range
    }
}
```

`FactionStandingSystem` (§5.2) is in the same folder.

### 4.3 Event payloads (`Assets/Game/Core/Events/`, namespace `Game.Core.Events`)

```csharp
namespace Game.Core.Events
{
    /// <summary>ECS-produced (FactionStandingSystem). One per faction whose relation value changed in a tick.</summary>
    public readonly struct FactionRelationChanged
    {
        public readonly ContentHash Faction;
        public readonly int OldRelation, NewRelation;
        public readonly byte OldStance, NewStance;     // FactionStance values
        public FactionRelationChanged(ContentHash f, int oldR, int newR, byte oldS, byte newS)
        { Faction = f; OldRelation = oldR; NewRelation = newR; OldStance = oldS; NewStance = newS; }
    }

    /// <summary>Managed. Once per instance, when every member is a hulk or gone.</summary>
    public readonly struct EncounterDefeated
    {
        public readonly uint InstanceId; public readonly ContentHash Encounter; public readonly uint StarSystemValue;
        public EncounterDefeated(uint instance, ContentHash encounter, uint starSystem) { InstanceId = instance; Encounter = encounter; StarSystemValue = starSystem; }
    }

    /// <summary>Managed, M4. Entering a star system whose owner differs from the previous star system's owner (and is not none).</summary>
    public readonly struct TerritoryEntered
    {
        public readonly ContentHash Faction; public readonly uint StarSystemValue;
        public TerritoryEntered(ContentHash f, uint s) { Faction = f; StarSystemValue = s; }
    }
}
```

`Game.Simulation/AssemblyInfo.cs` registers `ManagedEventQueue<FactionRelationChanged>`, and `ManagedEventChannels.RegisterAll` gets `RegisterChannel<FactionRelationChanged>()` (SPEC-02 §5.6 rule 3).

### 4.4 Encounter runtime types (`Assets/Game/Galaxy/Encounters/`, namespace `Game.Galaxy.Encounters`)

```csharp
namespace Game.Galaxy.Encounters
{
    using Unity.Mathematics; using Game.Galaxy.Data;

    public enum EncounterSource : byte { Director = 0, Narrative = 1, Combat = 2, Dev = 3, Pending = 4 }
    public enum EncounterAnchorKind : byte { Player = 0, Body = 1, Tag = 2 }
    public enum InstanceState : byte { Active = 0, Defeated = 1, Despawned = 2 }

    public struct EncounterAnchor
    {
        public EncounterAnchorKind Kind;
        public BodyId Body;          // Kind == Body
        public string Tag;           // Kind == Tag: BodyTemplateDefinition.storyTag
        public static EncounterAnchor Player() => new EncounterAnchor { Kind = EncounterAnchorKind.Player };
    }

    /// <summary>Read-only view of an instance (for SPEC-42 hails, SPEC-70, tests).</summary>
    public readonly struct EncounterInstanceInfo
    {
        public readonly uint Id; public readonly Game.Data.Definitions.EncounterDefinition Definition; public readonly StarSystemId StarSystem;
        public readonly EncounterSource Source; public readonly InstanceState State; public readonly int Members, AliveMembers;
        public readonly bool Persistent;
        public EncounterInstanceInfo(uint id, Game.Data.Definitions.EncounterDefinition def, StarSystemId sys, EncounterSource src, InstanceState st, int members, int alive, bool persistent)
        { Id = id; Definition = def; StarSystem = sys; Source = src; State = st; Members = members; AliveMembers = alive; Persistent = persistent; }
    }

    /// <summary>One planned ship of an instance (output of EncounterComposer).</summary>
    public struct PlannedShip { public Game.Data.Definitions.ShipBlueprintDefinition Blueprint; public Game.Data.Definitions.AiProfileDefinition Profile; public float RadiusM; }

    public static class EncounterComposer   // §5.5, pure
    {
        /// Fills `into` (cleared first) deterministically from the definition and the instance seed.
        public static void Compose(Game.Data.Definitions.EncounterDefinition def, ulong instanceSeed, float defaultCellSize,
                                   System.Collections.Generic.List<PlannedShip> into);
        public static float BlueprintRadiusM(Game.Data.Definitions.ShipBlueprintDefinition bp);  // cellSize × length(max(|BoundsMin|, |BoundsMax + 1|))
    }

    public struct PlacementParams { public float DistanceMinM, DistanceMaxM, MinPlayerDistanceM, BodyClearanceM, StepDegrees; public int Tries; }

    public static class EncounterPlacement   // §5.6, pure
    {
        /// Centre of the instance in the star-system frame, or false when no clear point exists in `p.Tries` tries.
        public static bool TryPlaceCentre(StarSystemContent content, double3 anchorM, float anchorRadiusM, double3 playerM,
                                          double universalTime, ulong instanceSeed, in PlacementParams p, out double3 centreM);
        /// Ship positions and yaw: line abreast perpendicular to the direction centre → facePointM (§5.6); or formation slots (M4).
        public static void ShipPoses(double3 centreM, double3 facePointM, System.Collections.Generic.IReadOnlyList<PlannedShip> ships, float spacingM,
                                     Game.Data.Definitions.FormationDefinition formation, System.Collections.Generic.List<(double3 pos, float yaw)> into);
    }
}
```

*Internal:*

```csharp
internal sealed class EncounterInstance
{
    public uint Id;                                     // serial from 1, saved in ENCT
    public EncounterDefinition Definition;
    public StarSystemId StarSystem;
    public EncounterSource Source;
    public InstanceState State;
    public bool Persistent;                             // decision 8
    public ulong Seed;
    public EncounterAnchor Anchor;                      // for re-spawn of pending instances
    public readonly List<ulong> Members = new(10);      // PersistentIds in spawn order
    public int AliveMembers;                            // not hulk, root exists (updated by the sweep)
    public double FarSinceUniversalTime = -1;           // despawn timer (§5.8); -1 = player near
    public bool AnyEngaging;                            // any member AiBrain.State == Engage (sweep)
}

internal struct PendingEncounter { public ContentId Encounter; public uint InstanceId; public StarSystemId StarSystem; public EncounterAnchor Anchor; public bool Combat; }
```

### 4.5 Services (`Assets/Game/Galaxy/Services/`, namespace `Game.Galaxy.Services`)

```csharp
namespace Game.Galaxy.Services
{
    using Unity.Mathematics; using Game.Galaxy.Data; using Game.Galaxy.Encounters; using Game.Data.Definitions;

    /// <summary>Managed facade over the faction singleton. Implements SPEC-33 §5.3.</summary>
    public sealed class FactionService : Game.Core.Flow.INewGameInitializer, System.IDisposable
    {
        public FactionService(Unity.Entities.World world, Game.Data.Services.ContentCatalog catalog);
        public int NewGameOrder => 30;
        public Game.Simulation.Factions.FactionIndexMap Indices { get; }
        public FactionDefinition PlayerFaction { get; }
        public int GetRelation(FactionDefinition faction);                         // −100..100; 0 for null/unknown; includes requests not yet applied
        public Game.Simulation.FactionStance GetPlayerStance(FactionDefinition faction);
        public void AdjustRelation(FactionDefinition faction, int delta);          // queued; applied next fixed tick; locked factions ignore it
        public void SetRelation(FactionDefinition faction, int value);             // queued; dev and restore use
        public Game.Simulation.FactionStance GetStance(byte factionA, byte factionB);   // matrix read (main thread)
        public void InitializeNewGame(in Game.Core.Flow.NewGameArgs args);         // base relations
        public void Dispose();
    }

    /// <summary>FACN (SPEC-05 order 30, version 1). §5.3.</summary>
    public sealed class FactionSaveParticipant : Game.Core.Save.ISaveParticipant { public FactionSaveParticipant(FactionService factions, Game.Data.Services.ContentCatalog catalog); }

    /// <summary>Owner of encounter instances and the director. Implements SPEC-33 §5.4–§5.9.</summary>
    public sealed class EncounterDirectorService : IGalaxyFrameTick, Game.Core.Flow.ISceneExiting, System.IDisposable
    {
        public EncounterDirectorService(GalaxyService galaxy, StarSystemLoader loader, TravelService travel, DockingService docking,
                                        IShipSpawner spawner, FactionService factions, TerritoryService territory,
                                        Unity.Entities.World world, Game.Data.Services.ContentCatalog catalog);
        public int TickOrder => 300;
        public int SceneExitOrder => 95;
        public bool Enabled { get; set; }                       // false: no director rolls (SPEC-71 replays, dev); requests still work
        public EncounterConfigDefinition Config { get; }

        // Requests (§5.7). Return the number of instances created (0 on refusal; reason logged).
        public int SpawnEncounter(EncounterDefinition def, in EncounterAnchor anchor, int count, EncounterSource source);
        public int StartCombat(EncounterDefinition def, in EncounterAnchor anchor);

        // Queries
        public int ActiveInstanceCount { get; }
        public int AliveShipCount { get; }
        public bool TryGetInstance(uint id, out EncounterInstanceInfo info);
        public bool TryGetInstanceByMember(ulong persistentId, out EncounterInstanceInfo info);   // SPEC-42 hails
        public void GetActiveInstances(System.Collections.Generic.List<EncounterInstanceInfo> into);
        public bool IsGloballyUsed(EncounterDefinition def);    // uniqueGlobal already spawned

        public void Tick(in GalaxyFrameContext ctx);
        public void OnSceneExiting(in Game.Core.Flow.SceneExitContext context);
        public void Dispose();
    }

    /// <summary>ENCT (SPEC-05 order 100, version 1). §5.10.</summary>
    public sealed class EncounterSaveParticipant : Game.Core.Save.ISaveParticipant { public EncounterSaveParticipant(EncounterDirectorService director, Game.Data.Services.ContentCatalog catalog); }

    /// <summary>Star-system ownership. Implements SPEC-30's IStarSystemOwnership. §5.11.</summary>
    public sealed class TerritoryService : IStarSystemOwnership, IGalaxyFrameTick
    {
        public TerritoryService(GalaxyService galaxy, FactionService factions, Game.Data.Services.ContentCatalog catalog);
        public int TickOrder => 310;
        public byte GetOwnerFaction(StarSystemId id);           // faction index; 255 = none (item 70)
        public string GetOwnerColorToken(StarSystemId id);      // FactionDefinition.mapColorToken, "" when none
        public FactionDefinition GetOwner(StarSystemId id);     // null when none
        public void Tick(in GalaxyFrameContext ctx);            // rebuilds the ownership table when GalaxyService.GenerationCount changed
    }
}
```

## 5. Systems, services and flow

### 5.0 Placement

| Part | Where | Notes |
|---|---|---|
| `FactionStandingSystem` | fixed step, `GameplayPrePhysicsGroup`, `[UpdateBefore(typeof(ShipCommandGroup))]`, Burst `ISystem` | requires `GameplayActive`, `FactionState`, `SimClock`; jobs only |
| `FactionService` calls | main thread, outside the fixed step | request append and standing reads complete their own dependencies |
| `EncounterDirectorService.Tick` | `GalaxyFrameTicker`, `TickOrder` 300 (main thread, `WorldGroup`) | the architecture's `EncounterDirectorSystem` (§5 `WorldGroup`), realised inside `GalaxyServicesBridgeSystem` like SPEC-31's loader; after the loader (100) and travel (200); returns at once without `WorldSceneActive` |
| `TerritoryService.Tick` | `GalaxyFrameTicker`, `TickOrder` 310 | rebuild check only |
| Event handlers (`StarSystemEntered`, `BodyInteracted`) | `EventHub.DrainAll` | §5.8, §5.9 |

The director calls `IShipSpawner` from inside the ticker callback: on the main thread, outside the fixed step, with no `SystemAPI` iteration in progress. `_INTEGRATION-TODO` item 71 relaxes SPEC-01/31's "outside system updates" wording to exactly this.

### 5.1 Boot: `FactionContent.CreateSingletons`

1. `map = FactionIndexMap.Build(catalog)`:
   - `player` = the single `isPlayerFaction` definition. If it is missing, use none and `GameLog.Error` (the validator prevents this).
   - Then `catalog.All<FactionDefinition>()` without the player faction. `Count = 1 + rest`, capped at 64; extra factions get 255 and an error.
2. Create one entity with:
   - `FactionRelations { Stance = new NativeArray<byte>(N×N, Persistent), Count = N }`;
   - `FactionState { thresholds from config, Count = N, Revision = 0 }`;
   - the buffers `FactionStanding` (N elements) and `FactionRelationRequest` (empty).
3. **Standings:** `Faction = def.Id.Hash`, `Relation = clamp(def.basePlayerRelation, −100, 100)`, `Locked = def.relationLocked`. The player faction (index 0) has `Relation = 100`, `Locked = 1`. `PlayerStance = StanceOf(Relation)`.
4. **Matrix:**
   - all Neutral; diagonal Friendly;
   - for each faction `a` (index ≥ 1) and each stance entry `(b, s)`: `M[a,b] = M[b,a] = s`;
   - row and column 0: `M[0,f] = M[f,0] = PlayerStance(f)`, with `M[0,0] = Friendly`.
5. Append the entity to `created`.

SPEC-21's `WeaponContent.CreateSingletons` runs after this and skips its default matrix because one exists (SPEC-21 §5 "unless one exists"). `StanceOf(r)` returns `Hostile` if `r ≤ HostileAtOrBelow`, `Friendly` if `r ≥ FriendlyAtOrAbove`, and `Neutral` otherwise.

### 5.2 `FactionStandingSystem`

`OnCreate`: `RequireForUpdate<GameplayActive>`, `<FactionState>`, `<SimClock>`. Build `damagedQuery = WithAll<Allegiance, GridDamageStats>().WithNone<PlayerShip>()` with a change filter on `GridDamageStats`, and `playerQuery = WithAll<PlayerShip>().WithNone<Parent>()`.

Per tick (markers `FactionStandingSystem.Provoke`, `.Apply`):

1. `tick = SimClock.Tick`; `stateEntity = SystemAPI.GetSingletonEntity<FactionState>()`; `player` = the first entity of `playerQuery` (`Entity.Null` if none).
2. **`ProvokeJob`** (`IJobChunk`, `Schedule`, single-threaded, over `damagedQuery`).
   - Input: `NativeArray<byte> provoked` (64, `WorldUpdateAllocator`, zeroed).
   - For each entity with `GridDamageStats.LastDamageTick == tick − 1`, `LastDamageInstigator == player`, `player != Null` and `Allegiance.Faction` in `[1, Count)`: set `provoked[Faction] = 1`.

   This is O(chunks changed last tick).
3. **`ApplyJob`** (`IJob`, `Schedule` after `ProvokeJob`). It uses `BufferLookup<FactionStanding>(false)`, `BufferLookup<FactionRelationRequest>(false)`, `ComponentLookup<FactionState>(false)` and `ComponentLookup<FactionRelations>(false)` (decision 3: RW lookups order this job after every earlier reader without a sync point). It also gets the `ManagedEventQueue<FactionRelationChanged>` parallel writer, obtained with `SystemAPI.GetSingleton` (read-only) and `[NativeDisableContainerSafetyRestriction]` (item 34 pattern).
   1. Copy `old[f] = standing[f].Relation` and `oldStance[f]` for all `f`.
   2. **Requests**, in buffer order: skip if `Faction ≥ Count` or `standing.Locked`. Otherwise `Relation = clamp(Mode == 0 ? Relation + Value : Value, −100, 100)`. Then clear the request buffer.
   3. **Provocation:** for `f` with `provoked[f] == 1`, `!Locked`, `PlayerStance != Hostile` and `tick − LastProvokedTick ≥ ProvokeIntervalTicks` (or `LastProvokedTick == 0`): `Relation = max(−100, Relation − ProvokePenalty)`; `LastProvokedTick = tick`.
   4. For each `f` with `Relation != old[f]`:
      - `PlayerStance = StanceOf(Relation)`;
      - if the stance changed, write `Stance[0·N + f] = Stance[f·N + 0] = (byte)PlayerStance`;
      - enqueue `FactionRelationChanged(standing.Faction, old, new, oldStance, newStance)` with `SortKey = f`;
      - `FactionState.Revision++` (once per tick if anything changed).
4. `state.Dependency` = the apply job handle.

Cost per tick: one tiny job, plus work only for chunks whose `GridDamageStats` changed.

### 5.3 `FactionService` and `FACN`

All members run on the main thread, outside the fixed step.

- **`GetRelation(def)`**:
  1. `i = Indices.IndexOf(def)`; 255 → 0.
  2. `em.CompleteDependencyBeforeRO<FactionStanding>()`; `r = buffer[i].Relation`.
  3. Fold the pending requests for `i` (read after `CompleteDependencyBeforeRO<FactionRelationRequest>`) in order, so a narrative `adjust_relation` followed by `relation(...)` in the same frame reads the new value.
  4. Clamp to [−100, 100].
- **`AdjustRelation(def, delta)` / `SetRelation`**:
  1. `i = IndexOf`; 255 or locked → `GameLog.WarnOnce` and return.
  2. `em.CompleteDependencyBeforeRW<FactionRelationRequest>()`.
  3. Append `{ i, Mode, delta }`.
- **`GetStance(a, b)`**: `CompleteDependencyBeforeRO<FactionRelations>()` and read the matrix.
- **`InitializeNewGame`**: for every standing, `Relation = base`, `LastProvokedTick = 0`; clear requests. Then rebuild `PlayerStance` and the matrix row and column 0 exactly as §5.1 steps 3–4.

  This writes directly, which is allowed: new game runs before `GameplayActive` exists, so no system is running.
- **FACN capture:**
  - `WriteVarUInt32(count)`;
  - then, for each faction sorted by `ContentId` (ordinal), excluding the player faction: `WriteContentId(id)`, `WriteInt32(GetRelation(def))`;
  - then the preserved unresolved records from the last restore, merged into the same sorted order.
- **FACN restore(reader, fromVersion):**
  - `fromVersion == 0` → `InitializeNewGame` semantics.
  - Otherwise:
    1. read the pairs;
    2. resolve each id with `catalog.TryResolve<FactionDefinition>`;
    3. resolved and not locked → `Relation = value` (direct write; restore runs with gameplay torn down);
    4. unresolved → keep `(id, value)` in a preserved list so it is written again;
    5. factions missing from the save keep their base relation;
    6. then rebuild the stances and the matrix as in `InitializeNewGame`, and `Revision++`.

  The round trip is byte-identical.

### 5.4 Director roll (`EncounterDirectorService.Tick`, step D)

**Preconditions for a roll.** All must hold; otherwise skip to the next step without consuming the roll:
- `Enabled`;
- `loader.IsReady`;
- `travel.Phase == Idle`;
- the loaded star system's flags do not contain `NoEncounters`;
- a player root exists without `Hulk`;
- `!docking.IsDocked`;
- `PlayerCombatStatus.HostilesEngaging == 0`, read after `CompleteDependencyBeforeRO`;
- active `Director` instances in this star system `< maxActiveDirectorInstances`;
- `AliveShipCount < maxShipsAlive` and `ActiveInstanceCount < maxInstances`;
- `ctx.UniversalTime − arrivalTime ≥ arrivalGraceSeconds` when the visit started by a jump.

**Roll**, when `ctx.UniversalTime ≥ nextRollTime`:
1. `rng = RngStreams.For(StableHash.Combine(sysDef.Seed, visitCount), RngPurpose.EncounterDirector, rollIndex)`; `rollIndex++`; `nextRollTime += rollIntervalSeconds`.
   - If more than one interval was missed (a long dialog with the world unpaused cannot happen, but be safe), set `nextRollTime = UniversalTime + rollIntervalSeconds`.
2. Always draw `u0 = rng.NextFloat()` and `u1 = rng.NextFloat()`.
3. If `u0 ≥ dangerSpawnChance[sysDef.Danger]`: no spawn.
4. **Eligible** = `catalog.All<EncounterDefinition>()` in ID order where:
   - `directorWeight > 0`;
   - `minDanger ≤ Danger ≤ maxDanger`;
   - `!(uniquePerVisit && spawnedThisVisit.Contains(def))`;
   - `!(uniqueGlobal && globallyUsed.Contains(def.Id))`;
   - `def.condition.Evaluate(logicContext)` is true, when an `ILogicContext` exists (otherwise only empty conditions pass).

   This is one evaluation per roll, not polling.
5. Weighted pick with `u1` (cumulative weight `> u1 × total`, in list order). If nothing is eligible, no spawn.
6. `Spawn(def, EncounterAnchor.Player(), EncounterSource.Director)` (§5.7).

### 5.5 Composition (`EncounterComposer.Compose`)

`rng = RngStreams.For(instanceSeed, RngPurpose.EncounterFleet, 0)`. Draws always happen for the maximum counts, so the stream position never depends on outcomes.

1. **Explicit ships** (`!useFactionDoctrine`): for each entry in list order:
   - draw `n = rng.NextInt(countMin, countMax + 1)`;
   - append `n` copies of `{ blueprint, profile = entry.aiProfile ?? faction.defaultAiProfile, RadiusM = BlueprintRadiusM(blueprint) }`.
2. **Doctrine**:
   - `n = rng.NextInt(doctrineMinShips, doctrineMaxShips + 1)`;
   - for `k` in `0..doctrineMaxShips−1`: draw `u = rng.NextFloat()`; if `k < n`, pick a doctrine entry by weight with `u` and append it with `profile = entry.aiProfile ?? faction.defaultAiProfile`.
3. Truncate to `min(count, maxShipsAlive − AliveShipCount)`. The caller handles 0.

`instanceSeed = StableHash.Combine(StableHash.Combine(sysDef.Seed, visitCount), StableHash.Combine(instanceId, def.Id.Hash.Value))`.

### 5.6 Placement (`EncounterPlacement`)

**Anchor resolution** (in the director; star-system frame, `t = UniversalTime`):
- `Player`: anchor = the player's `UniversePosition`, radius 0.
- `Body`:
  - if `Body.System == loader.Loaded` and the body exists in the loaded content and is not destroyed: anchor = `OrbitMath.Position(spec, t)`, radius = `spec.RadiusM`;
  - otherwise fall back to Player and `GameLog.WarnOnce("ENC.Anchor.BodyMissing", …)`.
- `Tag`: the first body of the loaded content (LocalKey order) whose template's `storyTag == Tag`, not destroyed. If none, fall back to Player with a WarnOnce.

**`TryPlaceCentre`**:
1. Draw from `rng = RngStreams.For(instanceSeed, RngPurpose.EncounterPlacement, 0)`, always in this order: `u = NextFloat()`, `v = NextFloat()`.
2. `d = anchorRadiusM + lerp(DistanceMinM, DistanceMaxM, u)`; `θ0 = 2πv`.
3. For `k` in `0..Tries−1`:
   - `θ = θ0 + k × StepDegrees` (radians);
   - `c = anchor + d × (cos θ, 0, sin θ)`.

   `c` is **clear** if both hold:
   - `length((c − playerM).xz) ≥ MinPlayerDistanceM`;
   - for every non-destroyed body `b`:
     - belts: `|c.xz|` lies outside `[BeltInner − BodyClearanceM, BeltOuter + BodyClearanceM]`;
     - others: `length((c − pos_b).xz) ≥ b.RadiusM + BodyClearanceM`.

   Return the first clear `c`.
4. Return false if none is clear.

The `PlacementParams` for each request:
- **Director:** `DistanceMin/Max` from the definition; `MinPlayerDistanceM = max(minPlayerDistanceM, inGrace ? arrivalGraceDistanceM : 0)`.
- **Narrative `SpawnEncounter`:** definition distances; `minPlayerDistanceM`.
- **`StartCombat`:** `DistanceMinM = DistanceMaxM = max(combatMinDistanceM, definition.spawnDistanceMinM × 0.5)`; `MinPlayerDistanceM = combatMinDistanceM`.

**`ShipPoses`** (line abreast, M3):
- `n` = ship count; `fwd = normalize((facePoint − centre).xz)` (zero → `+Z`); `right = (fwd.z, 0, −fwd.x)`;
- `spacing = spacingM > 0 ? spacingM : 2 × max(RadiusM) + 20`;
- ship `k` is at `centre + right × ((k − (n − 1) / 2) × spacing)`, with `yaw = atan2(fwd.x, fwd.z)` (facing the face point).

The face point is the player position for every source. With a `FormationDefinition` (M4, ENC-013), ship 0 is at `centre` and ship `k ≥ 1` at `centre + rotateY((slot.x, 0, slot.y), yaw)`, using `slots[k − 1]`.

### 5.7 Spawning an instance (`SpawnEncounter`, `StartCombat`, director)

`Spawn(def, anchor, source, combat = false)`:
1. **Refusals**, each logged with `GameLog.Warn("Encounters", …)` and returning 0:
   - no loaded star system or not Ready → or, for `Narrative`/`Combat`, **queue as pending** (step 8) and return 0;
   - `ActiveInstanceCount ≥ maxInstances`;
   - no player root (director only).
2. `id = nextInstanceSerial++`; `seed` per §5.5. Compose (§5.5). If 0 ships: refusal, but `Narrative`/`Combat` become pending.
3. Resolve the anchor (§5.6). `TryPlaceCentre`. On failure:
   - director: abandon, and do not count the roll as a spawn;
   - narrative and combat: use `anchor + (d, 0, 0)` with one warning.
4. `ShipPoses(centre, playerUP, ships, def.shipSpacingM, def.formation, poses)`.
5. `faction = factions.Indices.IndexOf(def.faction)`; `team = (byte)(1 + id % 254)`.
6. For each ship `k`:
   - **Spawn:**
     ```csharp
     pid = spawner.Spawn(new GalaxyShipSpawnArgs {
         Blueprint = ship.Blueprint.Id,
         Position = (float3)(pose.pos − FrameOrigin.OriginInStarSystem),
         Rotation = quaternion.RotateY(pose.yaw),
         LinearVelocity = 0,
         Allegiance = { Faction = faction, Team = team },
         WithAi = true,
         AiProfile = ship.Profile?.Id ?? ContentId.None,
         StarSystem = loader.Loaded,
         Encounter = def.Id.Hash });
     ```
   - If `pid.Value == 0`, `WarnOnce` and skip this ship.
   - `spawner.TryGetRoot(pid, out root)`; `em.AddComponentData(root, new EncounterTag { Encounter = def.Id.Hash, Instance = id })`.
   - If `def.hailable && k == 0`, also `em.AddComponentData(root, new Interactable { Kind = InteractionKind.Hail, TemplateIndex = 0xFFFF, DistanceM = hailDistanceM, Body = default })`.
   - M4 with a formation and `k ≥ 1`: `em.AddComponentData(root, new FleetMember { Leader = root0, Slot = (byte)(k − 1), FormationIndex = catalog.IndexOf(def.formation) })`.
   - Add `pid` to `Members`.
7. **Record the instance:**
   - `Persistent = source != Director || def.persistUntilDefeated`;
   - mark `spawnedThisVisit` and, for `uniqueGlobal`, `globallyUsed`;
   - `AliveMembers = Members.Count`.

   **AI behaviour by kind** (anchor = the spawn position, set by SPEC-23's first AI update):
   - `Ambush` and `Combat` source: after spawning, set each member's `AiBrain.Anchor` and `GoalPosition` to the player's local position. This is a main-thread `SetComponentData` on new entities, allowed: the director runs outside the fixed step. The ships head for the player and engage within their sensor range.
   - `Patrol` and `Merchant`: keep the spawn anchor.
8. **Pending** (decision 8): append `PendingEncounter { def.Id, id, star system = the anchor's star system or loader.Loaded, anchor, combat }`. Pending entries for the loaded star system are spawned by Tick step P once possible.

`SpawnEncounter(def, anchor, count, source)` clamps `count` to 1..10 and calls `Spawn` `count` times; it returns how many succeeded. `StartCombat(def, anchor)` calls `Spawn(def, anchor, EncounterSource.Combat, combat: true)`.

### 5.8 Instance lifecycle (`Tick` steps S, P, X and the handlers)

`Tick(ctx)` runs, in order:
1. **S. Sweep**, every frame, over active instances of the loaded star system. For each member:
   - `alive` = `TryGetRoot` succeeds, the entity exists and has no `Hulk`;
   - count `AliveMembers`;
   - `AnyEngaging` = any alive member with `AiBrain.State == Engage`.

   If an instance with `Members.Count > 0` reaches `AliveMembers == 0`, call `OnDefeated(instance)`:
   1. `State = Defeated`;
   2. `EventBus<EncounterDefeated>.Publish(new EncounterDefeated(id, def.Id.Hash, star system))`;
   3. if `defeatLoot` and an economy exist, `economy.GiveLoot(catalog.IndexOf(defeatLoot), −1, StableHash.Combine(seed, 0xDEFEA7))`;
   4. remove the instance from the active list.
2. **X. Despawn**, director instances that are not `Persistent`:
   - `far = every alive member farther than despawnDistanceM from the player (XZ) && !AnyEngaging`;
   - if `far` and `FarSinceUniversalTime < 0`, set it to `UniversalTime`;
   - if not `far`, reset it to −1;
   - if `far` and `UniversalTime − FarSince ≥ despawnDelaySeconds`: `spawner.Despawn(pid)` for every alive member; `State = Despawned`; remove.

   Hulks stay; they are wrecks for SPEC-50 salvage.
3. **P. Pending**: if `loader.IsReady`, a player root exists, `travel.Phase == Idle`, and pending entries match `loader.Loaded`, spawn them in list order, at most 2 instances per frame. Each spawn reuses its stored `InstanceId` and `Combat` flag. Remove an entry only when its spawn succeeded.
4. **D. Director roll** (§5.4).

**`StarSystemEntered(e)` handler:**
1. For every active instance of a different star system: its ships were destroyed by the unload (SPEC-31). Then:
   - `Persistent && State == Active` → move it to pending (`PendingEncounter` with its id, anchor and source);
   - otherwise drop it.
2. Set:
   - `visitCount = e.VisitCount`, `rollIndex = 0`;
   - `nextRollTime = UniversalTime + firstRollDelaySeconds`;
   - `arrivalTime = UniversalTime`, `arrivedByJump = (e.Reason == Jump)`;
   - clear `spawnedThisVisit`.

`OnSceneExiting`: move active persistent instances to pending (same rule), drop the rest, clear the per-visit state. Ships are destroyed by SceneFlow teardown.

### 5.9 Hails

`BodyInteracted` handler:
- if `e.Kind == (byte)InteractionKind.Hail` and `TryGetInstanceByMember(e.GridPersistentId, out info)`, the director does nothing itself;
- SPEC-42 subscribes to the same event and looks up `TryGetInstanceByMember` to choose the encounter's beat or dialog (item 65 area).

The director only guarantees the `Interactable` on the leader of `hailable` encounters, and the lookup API (O(1) through a `Dictionary<ulong, uint>` member → instance, maintained on spawn and removal).

### 5.10 `ENCT` participant (order 100, version 1)

**Capture**, in this order:
1. `WriteUInt32(nextInstanceSerial)`.
2. `globallyUsed`: `WriteVarUInt32(count)`, then `WriteContentId` each, sorted ordinal.
3. Pending entries, including active persistent instances converted to pending at capture time: `WriteVarUInt32(count)`. Per entry, sorted by `InstanceId`:
   - `WriteContentId(encounter)`;
   - `WriteUInt32(instanceId)`;
   - `WriteUInt32(starSystem.Value)`;
   - `WriteByte(anchor.Kind)`;
   - `WriteUInt64(anchor.Body.LocalKey)`;
   - `WriteString(anchor.Tag ?? "")`;
   - `WriteBool(combat)`.

**Restore:**
- `fromVersion == 0` → empty state, `nextInstanceSerial = 1`.
- Otherwise read in the same order. An unresolved encounter id keeps the entry as raw data, written back unchanged on the next save. It is reported once and never spawned.
- Active instances are cleared: the World reload respawns persistent ones through pending.

Director timing and roll indices are not saved (a load starts a new visit, decision 5).

### 5.11 Territory (`TerritoryService`)

**Ownership table** (`byte[]` per `StarSystems` index, 255 = none). It is rebuilt when `galaxy.GenerationCount` changed, checked in `Tick` and at the start of every query:
1. Fill with 255.
2. **Authored:** for each faction `f` (index order) and each `AuthoredStarSystemDefinition s` in `ownedStarSystems`: `galaxy.TryResolveStarSystem(s.Id, out id)` → `owner[index(id)] = f`.
3. **Procedural (M4, ENC-014):**
   1. For each faction `f` with `territorySeeds > 0`, in index order: `rng = RngStreams.For(galaxy.Seed, RngPurpose.Territory, f)`. Draw `territorySeeds` indices into the list of procedural star systems (uniform, `rng.NextInt(0, P)`), skipping owned or already chosen ones by advancing to the next free index (wrapping). These are the seeds.
   2. Run a multi-source BFS over `Adjacency`, across procedural star systems only. Seeds start at hop 0 with their faction. A star system takes the first faction that reaches it at the lowest hop, ties broken by faction index, up to `territoryRadiusHops`.
   3. Authored ownership is never overwritten.

**Queries:**
- `GetOwnerFaction(id)`: 255 for unknown ids.
- `GetOwner(id)`: `factions.Indices.ByIndex(owner)`.
- `GetOwnerColorToken(id)`: the owner's `mapColorToken`, or "" for none.

**`TerritoryEntered` (M4):** the `StarSystemEntered` handler compares the new owner with the previous star system's owner. If they differ and the new owner is not 255, it publishes the event.

## 6. Public API

| Call | Pre-conditions | Post-conditions / errors |
|---|---|---|
| `FactionService.GetRelation(def)` | Main thread, outside the fixed step | Value including queued requests; 0 for unknown |
| `FactionService.AdjustRelation/SetRelation` | Main thread, outside the fixed step | Applied at the next fixed tick; locked or unknown → one warning, no change |
| `FactionService.GetStance(a, b)` | Main thread | Current matrix value |
| `EncounterDirectorService.SpawnEncounter` | Main thread (narrative, dev) | Instances created now or pending (narrative); returns the count created now |
| `EncounterDirectorService.StartCombat` | Main thread; dialog already ended (SPEC-41 decision 8) | One instance close to the anchor, heading for the player |
| `TryGetInstanceByMember(pid)` | — | O(1); false for non-members |
| `TerritoryService.GetOwnerFaction(id)` | — | Faction index or 255 |
| `FactionContent.CreateSingletons` | Boot, before SPEC-21's `WeaponContent` | Singleton with N factions |
| `EncounterComposer.Compose`, `EncounterPlacement.*` | — | Pure and deterministic |

## 7. Edge cases and failure behaviour

| Situation | Required behaviour |
|---|---|
| Player shoots a neutral station for 3 s | Relation −10 per second; Hostile after crossing −25; the station's turrets and that faction's ships engage from the next tick; one `FactionRelationChanged` per change |
| Player shoots a locked faction (raiders) | No change |
| Ram damage to a neutral (source = player root) | Counts as provocation (same rule) |
| Narrative `adjust_relation` and `relation()` in the same frame | `GetRelation` includes the queued request |
| Relation changes while ships are mid-fight | The matrix flips at the next tick; SPEC-23 re-targets on its next think |
| Story encounter requested while travelling or before the star system is loaded | Pending; spawned once the star system is Ready and travel is Idle |
| Player jumps away from an undefeated story encounter | Pending for that star system; respawns on the next entry with the same instance id |
| Save with an undefeated (not engaged) story encounter | Saved as pending in `ENCT`; respawns after load |
| Save while engaged | Blocked by SPEC-23 `PlayerCombatStatus` (`InCombat`) |
| Encounter ship becomes a hulk | Counts as defeated for the instance; the hulk stays |
| All members despawned by unload | Director instance dropped; persistent instance → pending |
| `IShipSpawner.Spawn` returns 0 for one ship | That ship is skipped; the instance exists with the others |
| No clear placement for a director spawn | No spawn; the roll is spent |
| Encounter definition removed from content after a save | Pending entry kept raw and re-saved; reported once; never spawned |
| Faction removed after a save | Its relation is preserved in `FACN` and written again; not used |
| `maxShipsAlive` reached | Director skips; narrative spawns are clamped (fewer ships) with a warning |
| Dev sandbox (no galaxy) | Director idle; narrative requests go pending and never spawn (warning once) |
| Two hostile encounters of different factions meet | They fight each other if their factions are Hostile in the matrix |
| `NoEncounters` star system (story calm) | No director rolls; narrative spawns still work |

## 8. Performance budget

| Gate / item | Target | Test (task) |
|---|---|---|
| `FactionStandingSystem` | ≤ 0.02 ms per tick with 200 grids, 10 of them damaged | `FactionStandingPerfTests.Tick_200Grids` (ENC-003) |
| `EncounterDirectorService.Tick` steady state | ≤ 0.05 ms with 16 instances / 40 members, 0 B | `EncounterDirectorTests.Tick_Steady_NoAlloc` (ENC-007) |
| One instance spawn (10 ships) | ≤ 1.5 ms main thread (composition, placement, 10 `Spawn` calls) excluding SPEC-10 grid creation | `EncounterDirectorTests.Spawn_TenShips_Time` (ENC-007) |
| Territory rebuild (100,000 star systems, M4) | ≤ 30 ms once per galaxy build | `TerritoryTests.Rebuild_100k` (ENC-014) |
| 30-ship battle (06 combat gate) | Counts the director's ships; budget owned by SPEC-21/23 gates | ENC-011 runs it with director-spawned raiders |

**Scale notes.**
- Faction state is O(N²) bytes with N ≤ 64, so at most 4 KB.
- Provocation is O(chunks changed).
- Director work is O(members) ≤ 40 per frame.
- Placement is O(bodies × tries) ≤ 512 × 12 per spawn.
- Territory is O(V + E) once per galaxy build, with a byte per star system.
- Rejected:
  - per-frame condition polling (conditions are evaluated once per roll);
  - per-entity managed loops over all ships (only members are swept);
  - main-thread `GetSingletonRW` in the fixed step (decision 3).

## 9. Tasks

### ENC-001 — Public contract types
- **Milestone:** M3
- **Tier:** standard — transcribes the exact code of §4 (bodies throw `NotImplementedException`)
- **Depends on:** `CORE-001`, `DATA-001`, `GAL-001`, `SYS-001`, `TRV-001`, `WPN-001`, `AI-001`, `NAR-001`, `ECO-001`
- **Do:** Create every type of §4 in the folders of decision 12, and register the `FactionRelationChanged` channel (§4.3).
- **Files:**
  - `Assets/Game/Data/Definitions/Factions/{FactionDefinition,FactionStanceEntry,FactionStanceValue,DoctrineEntry}.cs`;
  - `Assets/Game/Data/Definitions/Encounters/{EncounterKind,EncounterShipEntry,EncounterDefinition,EncounterConfigDefinition}.cs`;
  - `Assets/Game/Simulation/Factions/{FactionStanding,FactionRelationRequest,FactionState,EncounterTag,FactionContent,FactionIndexMap}.cs`;
  - `Assets/Game/Core/Events/{FactionRelationChanged,EncounterDefeated,TerritoryEntered}.cs`;
  - `Assets/Game/Galaxy/Encounters/{EncounterSource,EncounterAnchorKind,InstanceState,EncounterAnchor,EncounterInstanceInfo,PlannedShip,EncounterComposer,PlacementParams,EncounterPlacement}.cs`;
  - `Assets/Game/Galaxy/Services/{FactionService,FactionSaveParticipant,EncounterDirectorService,EncounterSaveParticipant,TerritoryService}.cs`;
  - `Assets/Game/Simulation/AssemblyInfo.cs`;
  - `Assets/Game/Presentation/Systems/ManagedEventChannels.cs` (one line).
- **Acceptance criteria:**
  - [ ] Compiles; ECS types are unmanaged; `FactionStanceValue` values equal `FactionStance` values.
- **Tests:** `EncounterContractTests.EcsTypes_Unmanaged`, `.StanceMirror_ValuesEqual`.

### ENC-002 — Definitions, config and validators
- **Milestone:** M3
- **Tier:** standard — fields and rule table given in §4.1
- **Depends on:** `ENC-001`, `SPEC-04`
- **Do:**
  1. Implement the properties and `Validate` methods, and `EncounterContentValidator` (ENC-E001…E020, W001, W002).
  2. Implement `EncounterDefinition : ILogicSourceOwner`: compile with `LogicUsage.EncounterCondition`, field "condition", and bind.
  3. Create `config_encounters.asset` with the §4.1 defaults.
- **Files:** `Assets/Game/Data/Definitions/{Factions,Encounters}/*.cs`; `Assets/Game/Editor/Content/Validators/EncounterContentValidator.cs`; `Assets/Game/Content/Config/config_encounters.asset`; `Assets/Game/Tests/EditMode/Galaxy/Encounters/EncounterValidationTests.cs`
- **Acceptance criteria:**
  - [ ] Each code has a failing and a passing fixture.
  - [ ] The defaults match §4.1.
- **Tests:** `EncounterValidationTests.<Code>_Fails`, `.<Code>_Passes`; `EncounterConfigTests.Defaults`.

### ENC-003 — Faction singleton, index map and FactionStandingSystem
- **Milestone:** M3
- **Tier:** frontier — F1/F2: sole writer of a matrix read by weapon and AI jobs every tick; ordering through RW lookups without sync points
- **Depends on:** `ENC-002`, `WPN-012`, `DMG-015`, `AI-010`
- **Do:** Implement `FactionIndexMap`, `FactionContent.CreateSingletons` (§5.1) and `FactionStandingSystem` (§5.2).
- **Files:** `Assets/Game/Simulation/Factions/{FactionIndexMap,FactionContent,FactionStandingSystem,ProvokeJob,FactionApplyJob}.cs`; `Assets/Game/Tests/EditMode/Simulation/Factions/{FactionStandingTests,FactionStandingPerfTests}.cs`
- **Acceptance criteria:**
  - [ ] Index 0 is `faction_ark`; the others follow ID order; adding a faction whose ID sorts last changes no existing index.
  - [ ] The matrix after boot equals §5.1 (symmetric, diagonal Friendly, row 0 from base relations).
  - [ ] A request queued before tick T is applied in tick T; stance flips write both matrix cells; one `FactionRelationChanged` per changed faction.
  - [ ] Provocation: player damage to a neutral faction's grid at tick T lowers the relation by 10 at T+1, and again only after 60 ticks. Locked factions and hostile factions are unaffected. Damage from other sources is ignored.
  - [ ] With a weapon job reading `FactionRelations` scheduled before the system, no safety error occurs and the main thread never waits (no `Complete` calls in the system).
  - [ ] The perf test meets §8.
- **Tests:** `FactionStandingTests.IndexMap_Order`, `.Boot_Matrix`, `.Request_AppliedNextTick`, `.StanceFlip_WritesBothCells`, `.Provocation_RateLimited`, `.Locked_Ignored`, `.OtherSources_Ignored`, `.NoSyncPoint`; `FactionStandingPerfTests.Tick_200Grids`.

### ENC-004 — FactionService and FACN
- **Milestone:** M3
- **Tier:** frontier — F3: binary save format with preserved unknown IDs
- **Depends on:** `ENC-003`, `PERS-001`, `FND-001`
- **Do:** Implement `FactionService` (§5.3, including `INewGameInitializer`) and `FactionSaveParticipant`, with the SPEC-05 registry row `FACN`.
- **Files:** `Assets/Game/Galaxy/Services/{FactionService,FactionSaveParticipant}.cs`; `Assets/Game/Tests/EditMode/Galaxy/Encounters/FactionServiceTests.cs`
- **Acceptance criteria:**
  - [ ] `AdjustRelation` then `GetRelation` in the same frame returns the new value.
  - [ ] Capture → restore → capture is byte-identical, including an unresolved faction ID.
  - [ ] Restoring into a world that already holds other relations replaces them and rebuilds the matrix.
  - [ ] New game resets to base relations.
- **Tests:** `FactionServiceTests.GetRelation_IncludesQueued`, `.Facn_RoundTrip`, `.Facn_UnknownPreserved`, `.Restore_RebuildsMatrix`, `.NewGame_BaseRelations`.

### ENC-005 — TerritoryService (authored ownership)
- **Milestone:** M3
- **Tier:** standard — table rules in §5.11
- **Depends on:** `ENC-002`, `ENC-003`, `ENC-004`, `GAL-008`
- **Do:** Implement `TerritoryService` steps 1–2 of §5.11, the queries, and the rebuild check.
- **Files:** `Assets/Game/Galaxy/Services/TerritoryService.cs`; `Assets/Game/Tests/EditMode/Galaxy/Encounters/TerritoryTests.cs`
- **Acceptance criteria:**
  - [ ] Authored ownership is resolved; unowned star systems and unknown ids return 255 and "".
  - [ ] A new galaxy generation rebuilds the table.
- **Tests:** `TerritoryTests.Authored_Owner`, `.Unowned_255`, `.Rebuild_OnGeneration`.

### ENC-006 — Composition and placement
- **Milestone:** M3
- **Tier:** standard — pure functions with fixed draw orders (§5.5, §5.6)
- **Depends on:** `ENC-002`, `GAL-006`, `GAL-008`
- **Do:** Implement `EncounterComposer` and `EncounterPlacement`.
- **Files:** `Assets/Game/Galaxy/Encounters/{EncounterComposer,EncounterPlacement}.cs`; `Assets/Game/Tests/EditMode/Galaxy/Encounters/{ComposerTests,PlacementTests}.cs`
- **Acceptance criteria:**
  - [ ] The same seed gives identical compositions and poses. Changing one entry's `countMax` does not change the RNG position of later entries (draws for the maximum).
  - [ ] Placement:
    - never closer to the player than `MinPlayerDistanceM`;
    - never inside a body plus clearance;
    - never inside a belt annulus;
    - false when all tries are blocked.
  - [ ] Line abreast is centred, faces the face point, and keeps a spacing ≥ 2 × the largest radius + 20 m.
- **Tests:** `ComposerTests.Deterministic`, `.DrawsForMaximum`, `.Doctrine_Weighted`, `.CapTruncates`; `PlacementTests.PlayerDistance`, `.BodyClearance`, `.BeltAvoided`, `.AllBlocked_False`, `.LineAbreast_Geometry`.

### ENC-007 — EncounterDirectorService
- **Milestone:** M3
- **Tier:** frontier — F5: coordinates spawning, AI anchors, travel and loader state, story persistence and caps across frames
- **Depends on:** `ENC-004`, `ENC-005`, `ENC-006`, `SYS-008`, `SYS-013`, `FND-012`, `TRV-006`, `AI-008`
- **Sequencing:** ADR-0099 uses completed runtime owners; independent scenario/performance tasks retain their acceptance.
- **Do:**
  1. Implement §5.4 (roll), §5.7 (spawn), §5.8 (sweep, defeat, despawn, pending, handlers) and §5.9 (member lookup, hail `Interactable`).
  2. If SPEC-21's `ShipDestroyed` bridge exists, fill its `Encounter`/`EncounterInstance` fields from `EncounterTag` (item 68).
  3. Add the construction lines to SPEC-01 §5.2 row 8.2:
     ```csharp
     factions = new FactionService(world, content.Catalog);
     territory = new TerritoryService(galaxy, factions, content.Catalog);
     encounters = new EncounterDirectorService(galaxy, loader, travel, docking, spawner, factions, territory, world, content.Catalog);
     ticker.Register(encounters);
     ticker.Register(territory);
     flow.RegisterExiting(encounters);
     session.RegisterNewGameInitializer(factions);
     ```
     Register them in Services as `FactionService`, `TerritoryService`, `IStarSystemOwnership` and `EncounterDirectorService`. `IShipSpawner` comes from row 8.3, so these lines go after it.
- **Files:** `Assets/Game/Galaxy/Services/EncounterDirectorService.cs`; `Assets/Game/Galaxy/Encounters/EncounterInstance.cs`; `Assets/Game/App/Boot/GameComposition.cs`; `Assets/Game/Tests/PlayMode/Galaxy/EncounterDirectorTests.cs`
- **Acceptance criteria:**
  - [ ] **Director:** in a danger-2 star system with the director's interval shortened by a test config:
    - rolls happen at the scheduled `UniversalTime` only;
    - the same visit seed produces the same encounter IDs and ship counts in two runs;
    - nothing is rolled while travel is not Idle, while docked, or while hostiles engage.
  - [ ] **Instances:**
    - a spawned instance has `EncounterTag` on every member and the encounter's allegiance;
    - its ships are at least 800 m from the player;
    - an `Ambush` heads for the player's position.
  - [ ] **Defeat:** turning all members into hulks publishes `EncounterDefeated` once and gives `defeatLoot` once.
  - [ ] **Despawn:** a director patrol left 6 km behind for 30 s is despawned; a story instance is not.
  - [ ] **Pending:**
    - jumping away from an undefeated story encounter and back respawns it with the same instance id;
    - a narrative request during Transit spawns after arrival.
  - [ ] **Caps:** 40 ships and 16 instances are never exceeded.
  - [ ] `Tick_Steady_NoAlloc` and `Spawn_TenShips_Time` meet §8.
- **Tests:** `EncounterDirectorTests.Roll_Schedule`, `.Roll_Deterministic`, `.Roll_Preconditions`, `.Spawn_TagsAndAllegiance`, `.Ambush_HeadsForPlayer`, `.Defeat_OnceWithLoot`, `.Despawn_DirectorOnly`, `.Pending_AcrossJump`, `.Pending_DuringTransit`, `.Caps`, `EncounterDirectorPerfTests.Tick_Steady_NoAlloc`, `.Spawn_TenShips_Time` (separate opt-in fixture so the live API can select functional cases without benchmarks).

### ENC-008 — ENCT participant
- **Milestone:** M3
- **Tier:** frontier — F3: binary save format with raw preservation of unknown content
- **Depends on:** `ENC-007`, `PERS-001`
- **Do:** Implement `EncounterSaveParticipant` (§5.10) and the SPEC-05 registry row `ENCT`.
- **Files:** `Assets/Game/Galaxy/Services/EncounterSaveParticipant.cs`; `Assets/Game/Tests/EditMode/Galaxy/Encounters/EnctTests.cs`
- **Acceptance criteria:**
  - [ ] Round trip byte-identical with pending entries, globally used IDs and one unknown encounter ID.
  - [ ] After restore, the active list is empty and the pending entries are present.
- **Tests:** `EnctTests.RoundTrip_ByteIdentical`, `.UnknownEncounter_Preserved`, `.Restore_ActiveCleared`.

### ENC-009 — Narrative wiring
- **Milestone:** M3
- **Tier:** standard — routing of existing APIs per §6
- **Depends on:** `ENC-007`, `NAR-011`, `NAR-012`, `NAR-020`
- **Sequencing:** ADR-0100 uses the completed runtime owners.
- **Do:** In `LogicWorldAdapter` (Game.App), wire these members and remove them from `UnwiredMembers`:
  - `GetRelation(i)`: faction = `catalog.GetByIndex<FactionDefinition>(i)`, or the current owner when `i == −1`; returns `factions.GetRelation`.
  - `AdjustRelation`.
  - `CurrentStarSystemOwnerFactionIndex`: `catalog.IndexOf(territory.GetOwner(loaded))`, 0 = none.
  - `SpawnEncounter(i, anchorTag, count, scope)`. The anchor:
    - `"here"`: `scope.HasBody` → `Body` (`BodyId` from the `LogicBodyHandle` fields); otherwise `Player`;
    - `"player"`: `Player`;
    - anything else: `Tag`.

    Then call `director.SpawnEncounter(def, anchor, count, Narrative)`.
  - `StartCombat(i, scope)`.

  In `NarrativeWorldEventBridge`, map `FactionRelationChanged` to `NotifyDependency(Relation, catalog.IndexOf(faction))`, and `EncounterDefeated` to `add defeated_<encounter> 1` (SPEC-40 §5.6).
- **Files:** `Assets/Game/App/Services/LogicWorldAdapter.cs`; `Assets/Game/App/Services/NarrativeWorldEventBridge.cs` (SPEC-40 NAR-012); `Assets/Game/Tests/EditMode/App/LogicWorldAdapterEncounterTests.cs`
- **Acceptance criteria:**
  - [ ] Each wired member calls its owner once with the mapped arguments (fakes).
  - [ ] `spawn_encounter encounter_raiders here 2` with a body scope anchors on that body.
  - [ ] A relation change re-evaluates a `relation("faction_vestari") < 0` watcher.
- **Tests:** `LogicWorldAdapterEncounterTests.Relation_Routes`, `.SpawnEncounter_AnchorMapping`, `.StartCombat_Routes`, `.OwnerIndex`, `.RelationChange_NotifiesDependency`.

### ENC-010 — Legacy converter and M3 content
- **Milestone:** M3
- **Tier:** standard — explicit tables (below) and SPEC-72 hand-off
- **Depends on:** `ENC-002`, `SPEC-72`
- **Do:**
  1. **`FleetEncounterConverter`** (`ILegacyConverter`, `Order` 600, `Name` "Encounters") over `LegacyHandOff.Fleets`. Map:
     - `npc_enemy_1` → `encounter_raiders`;
     - `npc_probe_tutorial` → `encounter_xeno_probe_tutorial`;
     - `npc_story_hunter` → `encounter_whisper_hunter`;
     - `npc_traveling_merchant` → `encounter_traveling_merchant`.

     Ships come from the **first** formation, grouped by blueprint into entries with `countMin = countMax = count`. Other formations are reported as `ENC-I001` (dropped). Allegiance "Enemy" maps to the hostile factions below and "Neutral" to `faction_vestari`.
  2. **Author** the factions `faction_ark` and `faction_raiders`, plus `faction_xeno` (hostile, locked, −100, map token `--color-faction-xeno`). MIG-012 writes the three legacy factions with the §4.1 fields:
     - `faction_ark` ("ARK Expedition"): player, locked, owns `starsys_ark_start` (SPEC-42 §4.12).
     - `faction_raiders` ("Raiders"):
       - −100, locked, Hostile to every other non-player faction;
       - doctrine `ship_fighter` 1.0 and `ship_striker` 0.6;
       - `defaultAiProfile ai_default`.
     - `faction_vestari` owns `starsys_whisper`.
  3. **Author the encounters below** (all `faction_raiders` unless stated). `starsys_drift` has danger 2 (SPEC-42 §4.12).

  | ID | Kind | Ships (count) | Director | Notes |
  |---|---|---|---|---|
  | `encounter_raiders` | Ambush | `ship_fighter` 2–3 | weight 1, danger 1–3 | distance 1,600–2,200 m |
  | `encounter_xeno_probe_tutorial` | Story | `ship_frigate` 3 | — | `faction_xeno` |
  | `encounter_whisper_hunter` | Story | `ship_red_elite` 1 | — | |
  | `encounter_traveling_merchant` | Merchant | `ship_freighter` 1, `ai_civilian` | weight 0.5, danger 0–2 | `faction_vestari`, hailable, `uniquePerVisit` |
  | `encounter_glass_worms` | Story | `ship_beetle` 3 | — | `faction_xeno` |
  | `encounter_doppelganger` | Story | `ship_crescent` 1 | — | `faction_xeno` |
  | `encounter_scavenger_king` | Story | `ship_bastion` 1, `ship_fighter` 2 | — | |
  | `encounter_jungle_beasts` | Story | `ship_beetle` 4 | — | `faction_xeno` |
  | `encounter_solar_parasite` | Story | `ship_butterfly` 2 | — | `faction_xeno` |
  | `encounter_extraction_guardians` | Story | `ship_striker` 2, `ship_frigate` 1 | — | |
- **Files:** `Assets/Game/Editor/Migration/Encounters/FleetEncounterConverter.cs`; `Assets/Game/Content/Factions/*.asset`; `Assets/Game/Content/Encounters/*.asset`; `Assets/Game/Tests/EditMode/Editor/Migration/FleetEncounterConverterTests.cs`
- **Acceptance criteria:**
  - [ ] The content validates with zero ENC errors.
  - [ ] Every encounter ID referenced by SPEC-40 §4.13 exists.
  - [ ] The converter output for the four fleets matches the table above.
- **Tests:** `FleetEncounterConverterTests.RealExport_FourEncounters`; `EncounterContentSmokeTests.AllReferencedIdsExist`.

### ENC-011 — Integration and regression scenarios
- **Milestone:** M3
- **Tier:** standard — PlayMode tests of specified behaviour
- **Depends on:** `ENC-008`, `ENC-009`, `ENC-010`, `TST-001`
- **Do:** PlayMode suite on the M3 content. Each scenario records its frame time in the ledger *Measurements*.
  - **Raiders:** in `starsys_drift` with a forced roll, raiders spawn and engage the player.
  - **Station defence:** `encounter spawn=encounter_raiders` near `whisper_outpost` (Vestari, SPEC-42 §4.12): the outpost's turrets shoot them.
  - **Provocation:** firing at `whisper_outpost` turns Vestari hostile and its turrets fire back.
  - **Narrative:** `start_combat encounter_whisper_hunter` from a test dialog spawns one ship within 300–600 m; destroying its core publishes `EncounterDefeated` and sets `defeated_encounter_whisper_hunter`.
  - **Persistence:** a save with a pending story encounter loads and respawns it.
  - **Battle:** 30 director-spawned and story ships fighting meet SPEC-21/23's combat gate on the reference machine.
- **Files:** `Assets/Game/Tests/PlayMode/Galaxy/EncounterIntegrationTests.cs`
- **Acceptance criteria:**
  - [ ] All six scenarios pass.
- **Tests:** `EncounterIntegrationTests.Raiders_Engage`, `.StationDefends`, `.Provocation_StationRetaliates`, `.StartCombat_DefeatSetsFact`, `.PendingStory_SurvivesSaveLoad`, `.ThirtyShips_CombatGate`.

### ENC-012 — Dev commands and probes
- **Milestone:** M3
- **Tier:** standard — harness wiring per SPEC-70 conventions
- **Depends on:** `ENC-007`, `ENC-004`, `DEV-003`, `DEV-005`, `DEV-006`
- **Sequencing:** ADR-0100: direct service commands do not wait for narrative routing or overlays.
- **Do:**
  1. Add commands:
     - `encounter spawn=<id> [count=1] [anchor=player|<tag>]` → `SpawnEncounter(…, Dev)`;
     - `encounter combat=<id>`;
     - `encounter director=on|off`;
     - `encounter list`;
     - `relation faction=<id> [set=<v>|add=<d>]`.
  2. Add probes: `encounters.active`, `encounters.ships`, `relation("faction_id")`, `stance("faction_id")` (string), `encounter_defeated("encounter_id")` (count since run start, from `EncounterDefeated`).
  3. Add CLI wrappers.
- **Files:** `Assets/Game/App/DevRuntime/Commands/{EncounterDevCommand,RelationDevCommand}.cs`; `Assets/Game/App/DevRuntime/Scenarios/EncounterScenarioProbes.cs`; `Assets/Game/DevTools/Editor/Commands/EncounterCliCommands.cs`
- **Acceptance criteria:**
  - [ ] `encounter spawn=encounter_raiders` then `encounters.ships >= 2`.
  - [ ] `relation faction=faction_vestari set=-50` then `stance("faction_vestari") == "Hostile"` after one tick.
- **Tests:** `EncounterDevCommandTests.Spawn_Probe`, `.Relation_SetAndStance`.

### ENC-013 — Encounter formations
- **Milestone:** M4
- **Tier:** standard — slot placement and component given in §5.6/§5.7
- **Depends on:** `ENC-006`, `ENC-007`, `AI-013`, `AI-014`
- **Sequencing:** ADR-0100: formation implementation precedes combined encounter integration scenarios.
- **Do:** Implement formation poses (§5.6) and `FleetMember` assignment at spawn (§5.7 step 6). Promote encounters with 3+ ships to use `formation_wedge` (new `FormationDefinition` asset: slots (−40, −40), (40, −40), (−80, −80), (80, −80), …).
- **Files:** `Assets/Game/Galaxy/Encounters/EncounterPlacement.cs`; `Assets/Game/Galaxy/Services/EncounterDirectorService.cs`; `Assets/Game/Content/Formations/formation_wedge.asset`; tests
- **Acceptance criteria:**
  - [ ] Members spawn at their slots and carry `FleetMember` pointing at the leader.
- **Tests:** `PlacementTests.Formation_Slots`, `EncounterDirectorTests.Formation_FleetMember`.

### ENC-014 — Procedural territory and TerritoryEntered
- **Milestone:** M4
- **Tier:** standard — seeded selection and multi-source BFS fully specified in §5.11
- **Depends on:** `ENC-005`, `GAL-008`, `CORE-003`
- **Do:** Implement §5.11 step 3 and `TerritoryEntered`.
- **Files:** `Assets/Game/Galaxy/Services/TerritoryService.cs`; `Assets/Game/Tests/EditMode/Galaxy/Encounters/TerritoryTests.cs`
- **Acceptance criteria:**
  - [ ] Deterministic for the same galaxy seed; authored ownership is unchanged.
  - [ ] Every owned procedural star system is within `territoryRadiusHops` of a seed of its owner; ties go to the lower faction index.
  - [ ] `Rebuild_100k` meets §8.
  - [ ] Entering a differently owned star system publishes `TerritoryEntered` once.
- **Tests:** `TerritoryTests.Procedural_Deterministic`, `.Procedural_HopBound`, `.Procedural_TieBreak`, `.Rebuild_100k`, `.TerritoryEntered_OnOwnerChange`.

## 10. Open questions

| # | Question | Default if unanswered | Decider |
|---|---|---|---|
| 1 | Relation thresholds and provocation strength | Hostile ≤ −25, Friendly ≥ 50, −10 per second of sustained fire | Owner |
| 2 | Should faction↔faction stances change at runtime (wars)? | No in v1 (static content) | Owner |
| 3 | Director pacing | First roll after 90 s, then every 120 s; chance 0 / 25 / 45 / 65 % by danger | Owner |
| 4 | Do encounter defeats drop loot directly? | Only when the definition names `defeatLoot`; otherwise salvage the hulks (SPEC-50) | Owner |
| 5 | Should the player faction own star systems? | Yes (ARK home system); its stations are friendly | Owner |
| 6 | Xeno as a faction | `faction_xeno` (hostile, locked) for the xeno story encounters | Narrative lead |
| 7 | Encounter IDs for converted legacy fleets | SPEC-40 §4.13 IDs (`encounter_raiders`, …); SPEC-72 §4.4 suggestions are superseded (item 72) | Tech lead |
| 8 | Requested changes to other specs | `_INTEGRATION-TODO.md` items 66 and 68–75 | Tech lead |
| 20 | ENC-001's body-staging instruction and file list omit interface members, metadata/value semantics and the two internal §4.4 DTOs. | **ENC-001 default:** transcribe all 31 public types plus EncounterInstance/PendingEncounter. Definition getters expose authored data with read-only lists; literal enum values, payload/view constructors, EncounterAnchor.Player and constant lifecycle/save metadata retain their specified value semantics. Runtime service constructors/queries/operations, validation and logic compilation/binding throw NotImplementedException naming their owning ENC task. Complete ISaveParticipant, INewGameInitializer, IGalaxyFrameTick, ISceneExiting and ILogicSourceOwner signatures using existing canonical types. Register only the required faction event queue/channel; later producer, boot/service wiring, saves, composition and encounter behavior stay with ENC-002…014. No assets, new packages or alternate owners. | Tech lead — transcription/staging defaults; central mirror pending. |
| 21 | ENC-E001 needs a player faction before dependent ENC-010 authors faction_ark; config generation alone would leave the content database invalid | Bring forward only the canonical player definition in the ENC-002 lead-run job: faction_ark, ARK Expedition, isPlayerFaction=true, relationLocked=true, retaining stated definition defaults for unspecified fields. Link starsys_ark_start only when the real authored definition exists; never create a substitute star system. If absent, retain the empty ownership list and an explicit ENC-010 follow-up. Preserve existing assets/GUIDs; all other faction/encounter content and complete ARK ownership remain ENC-010. | Tech lead |
| 22 | Finite values, player stance declarations, inactive count ranges, merchant AI fallback and malformed active blueprint rows are abbreviated | Reject nonfinite authored numbers under their stated codes; ENC-E014 includes finite nonnegative shipSpacingM. Reject player-owned static stances as ENC-E003 (player relations come from thresholds), plus invalid enum values; equal duplicate declarations are allowed. Check every declared ship-count range and doctrine range, but the total bound uses only the active source (long sum of explicit maxima or doctrine maximum). Require active blueprints via ENC-E011/004. Merchant profiles resolve entry -> faction default -> the single AiConfig default, matching the spawner's fallback; unresolved/non-Civilian profiles fail ENC-E015. Null standalone contexts cannot resolve the final fallback. | Tech lead |
| 23 | ValidationScope has no report accessor and the canonical logic compiler/wrapper are still NAR-003 stubs | Compile the owned condition with LogicUsage.EncounterCondition, scope.AssetPath and field condition into a temporary report, forwarding each diagnostic through the scope. Bind through CompiledCondition.Bind. No new scope API, compiler implementation or NAR completion claim. Local definition checks own field diagnostics; EncounterContentValidator Order 330 owns counts, reverse-pair conflicts, territory conflicts and warnings using config thresholds (the player is always friendly). All stated fields/defaults/getters remain unchanged. | Tech lead |

| 24 | ENC-010's first-formation import rule disagrees with its authored final table: enemy/hunter first formations contain one Shuttle, while the merchant has no combat formation | **Resolved (2026-10-13, ledger Q334):** group and resolve the exact first formation in memory, report later formations as ENC-I001, and report the three authored composition replacements as ENC-I002. The final ENC-010 table wins: Raiders use 2–3 Fighters, Hunter one Red Elite, Merchant one Freighter with ai_civilian; Probe retains three Frigates. All four use explicit ship entries rather than the legacy doctrine switch. Existing imported ships/profiles are mandatory, never substitutes. Story entries persist; unspecified placement ranges retain definition defaults and unspecified conditions/formation/loot/spacing remain intact. Preserve ARK/legacy faction fields; add real GAL-012 ownership only through empty-link fills. Generation preflights real Ark Start, Whisper and danger-2 Drift, and validates ENC content separately from unfinished GAL content. | Tech lead |

| 25 | Pure encounter composition/placement waits for whole SPEC-30 including later map/performance/M4 work | Use completed ENC-002 definitions and GAL-006/008 generated content and service queries. Preserve specified RNG streams, obstruction geometry and all required tests; director, spawning and live encounter acceptance remain ENC-007. | Tech lead |

| 26 | ENC-006 acceptance abbreviates pure API boundaries: no live cap inputs, a retained default cell-size parameter, maximum draws and an automatic-spacing minimum | **ENC-006 default:** follow §5.5/5.6 literally. Compose clears and returns the full ordered fleet; ENC-007 truncates by its actual available cap. CapTruncates checks that the composed prefix can be truncated by the caller, without claiming director coverage. Explicit entries consume one count draw each; doctrine consumes its count draw plus doctrineMaxShips weight draws, including unused slots. Blueprint radii use the blueprint cell size, not defaultCellSize. Positive authored spacing is honored; the radius-based minimum applies to automatic spacing. M3 poses are line abreast; formation slots remain ENC-013. | Tech lead — resolved; ledger Q352. |

| 27 | ENC-005 empty-galaxy lifetime and cache rebuild details | Follow authored step 2 using the accepted FactionService.Indices map and the current GalaxyService generation. | Tech lead — resolved; ledger Q448. Keep one managed byte table, initialized to255 and indexed by the current generated star-system indices; reuse its storage when the size is unchanged. Rebuild at construction, on frame ticks and before every query whenever GenerationCount changes. Clear the cache stamp/table when Galaxy is absent, including session clear without a generation increment. Null or unresolved authored ownership references are skipped; faction iteration remains canonical index order. Unknown/unowned/procedural M3 systems return255/null/empty token. No M4 procedural allocation, event subscriptions, persistence, API changes or shared composition edits. Exactly Authored_Owner, Unowned_255 and Rebuild_OnGeneration use the real FactionContent singleton, FactionService and a four-system generated galaxy with direct queries/reset events/ticker calls; transient ownership edits distinguish query-triggered rebuilds from Tick-triggered caching without reflection/test-only production APIs. Lead registers TerritoryService and IStarSystemOwnership, adds it to GalaxyFrameTicker and passes it to GalaxyMapModel; central mirroring and Unity acceptance remain lead-owned. |

| 33 | ENC-012 command output, zero-return combat requests and run-relative defeat counters | Commands and CLI use the real director/faction services through the shared default registries. Count means 1–10 encounter instances; combat is a single player-anchored StartCombat request. List shows available definitions plus actual active instance IDs, source, system and member counts. A zero Dev return is refusal; StartCombat returns zero for both queued and refused requests and exposes no pending readback, so report created 0, queued-or-refused and missing-galaxy/readiness/cap guidance as Failed (no invented Pending operation or spawn success). Relation writes accept one Int32 set/add, leave clamping/queueing to FactionService, reject locked factions and label returned relation/stance as including pending requests. Typed EncounterDefeated counts live with the dev host; each scenario snapshots its own per-definition and aggregate baselines, which survive session resets within that run. Unknown content/probe argument types fail explicitly. Author exactly Spawn_Probe and Relation_SetAndStance over production boot/director/fixed-step owners; lead owns Unity compile/acceptance. | Tech lead — resolved; ledger Q470. No production owner API changes. |
| 36 | ENC-009 needs concrete owner bindings while preserving the NAR-011 adapter constructor; narrative bridge handlers already exist in NAR-012 | Capture the registered FactionService, TerritoryService, StarSystemLoader and EncounterDirectorService in the adapter constructor, matching production boot order. Smaller contexts may omit them; requested faction/director operations report an unavailable owner once instead of pretending success. Owner queries use Loader.Loaded, return catalog index zero when unloaded/unowned, and resolve relation index -1 to that same owner. StartCombat uses the same here body/player mapping as SpawnEncounter. Remove only the five implemented routes from UnwiredMembers; preserve editor launching and unrelated routes. Reuse the existing bridge factory's canonical faction-hash resolver and generated defeat-counter writer without duplicate subscriptions or facts. Exactly five required tests compile real commands/conditions and use real faction/origin fixed-step systems, galaxy, territory, loader, director, narrative and watcher owners with a passive recording IShipSpawner boundary; no scenes, wall-clock combat waits, extra tests, tuning or worker Unity runs. Live bridge registration/callbacks remain QST-008 composition and ENCT remains ENC-008. | Tech lead — resolved; ledger Q473 |

## 11. References

- Audit (background only): `docs/reference/audit-2026-10/05-narrative-quests.md` (encounters unreachable, flags), `03-galaxy-world.md` (NPC fleets), `00-synthesis.md` §5.
- SPEC-21 §4.3 (`Allegiance`, `FactionRelations`); SPEC-23 §3–§5 (AI states, anchors, `PlayerCombatStatus`); SPEC-40 §4.7 (`spawn_encounter`, `start_combat`, `relation`, `adjust_relation`), §4.13 (IDs); SPEC-72 §4.6–§4.7 (faction values, fleet hand-off).

| 18 | ENC-003 faction runtime waits for whole-spec performance/balance work | Under ADR-0068 use accepted ENC-002, WPN-012, DMG-015 and AI-010 runtime/integration prerequisites. Advisory timing/balance and unrelated later follow-ups do not gate the standing writer. | Tech lead — resolved; ledger Q434 |

| 19 | ENC-003 job writer retains a read-only nested matrix handle after a scheduled reader; group bucket and world allocator need explicit APIs | Keep RW FactionRelations lookup for ECS ordering, and pass its Stance NativeArray explicitly as the apply job write container so native safety tracks the writer. Use CollectionHelper.CreateNativeArray with WorldUpdateAllocator. FactionStandingSystem uses OrderFirst with UpdateBefore ShipCommandGroup, which is also OrderFirst. No Complete calls in the production system; the required reader test passes. Intermediate request arithmetic uses long before clamping. | Tech lead — resolved; ledger Q437 |

| 20 | ENC-004 pending-read saturation and sandbox/reset lifetime | Fold each queued operation with the same clamp as the fixed-step writer. Register a session-model adapter as well as the new-game initializer, because sandbox reset skips campaign initializers. Parse FACN fully before mutation; reject duplicate or malformed records and preserve unknown IDs until session reset. | Tech lead — resolved; ledger Q441 |

| 21 | ENC-005 concrete prerequisites hidden by whole-spec dependency | Territory reads the canonical ENC-003 index map via ENC-004 faction service and GAL-008 galaxy generation; use these concrete completed prerequisites with ENC-002. | Tech lead — resolved; ledger Q442 |

| 28 | Procedural territory waits on unrelated encounter integration scenarios | Use completed authored territory, galaxy generation and deterministic RNG under ADR-0068. No encounter director or spawning dependency exists in the specified BFS/event algorithm. | Tech lead — resolved; ledger Q458 |

| 30 | Director integration needs concrete runtime prerequisites and asynchronous spawn readiness | ADR-0099 selects completed runtime owners; no acceptance is waived. Transit requests target TravelService.Target; pre-load requests use current galaxy system. Spawn readiness includes GameplayActive because the real spawner requires it. Sweeps retain unconverted requests and use their requested pose until LocalTransform exists; never defeat members during loader unload. Ambush initialization stamps the current origin-shift sequence so the first AI update preserves its player goal. ENCT persistence remains ENC-008. | Tech lead | Resolved; ledger Q467. |

| 31 | Independent encounter follow-ups wait on aggregate tests/unrelated owners | ADR-0100 uses completed real runtime prerequisites for ENC-009/012/013. Existing scopes/tests and later combined integration acceptance remain unchanged. | Tech lead | Resolved; ledger Q468. |

| 32 | ENCT load event ordering and malformed state | The director owns a shared internal save state. Loaded resets only transient active/member and visit state because SaveService restores ENCT before publishing SessionReset(Loaded). NewGame/Cleared reset all state. Parse and validate the complete payload before replacement; retain raw unknown identities and derive resolved global aliases separately. Session combat save blocking now reads the existing AI-008 singleton; tick zero means never damaged. | Tech lead | Resolved; ledger Q469. |
| 34 | ENC-013 names a new wedge asset path, but AI-013 already supplies the exact formation | **ENC-013 default:** reuse `Assets/Game/Content/Ai/formation_wedge.asset` and its existing GUID/content ID. Keep the canonical asset in place because AI-013 tests load that path; do not create a duplicate ID under `Content/Formations`. Apply it to authored encounters whose maximum fleet size is at least three, including variable two-to-three-ship raiders. | Tech lead | Resolved; ledger Q471. |

| 35 | Formation slot shortage, unavailable catalog index, or failed ship-zero spawn | **ENC-013 default:** if the composed fleet needs more slots than the formation supplies, or its catalog index is zero, spawn the entire fleet line abreast without FleetMember. Pure ShipPoses also falls back to line abreast for too few slots. A failed follower keeps later original slot indices. If ship zero fails, retain the surviving authored poses but assign no follower components; never point at a missing leader or silently promote a different ship during spawn. The existing skip-failed-ship rule remains intact. | Tech lead | Resolved; ledger Q471. |
