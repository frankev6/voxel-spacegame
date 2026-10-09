# Audit 04: Persistence, game data and items, inventory, loot, managers, bootstrap, utilities, player, audio

**Project:** `/Users/frank/DEV/spaceship-game` (Unity 6000.6.5f1, Entities 6.6.0, single assembly under `Assets/Game`)
**Auditor scope:** `Assets/Game/Core/Persistence/*`, `Assets/Game/Core/Game Logic/*`, `Assets/Game/Core/Inventory/*`, `Assets/Game/Core/Loot/*`, `Assets/Game/Core/Managers/{Global,InputManager,PauseManager,SettingsManager,UIManager}.cs`, `Assets/Game/Core/Utils/*`, `Assets/Game/Core/Character/Player.cs`, `Assets/Game/Core/Audio/*`, `Assets/Game/Core/SceneLoader.cs`, `Assets/Game/Core/RotateObject.cs`, `Assets/Game/Core/Editor/PaintBucketGenerator.cs`, `Assets/Game/Visuals/BrightnessController.cs`, `Assets/Game/Editor/{GameDataExporterWindow,ItemDisplayEditor,ShipItemEditor,ShipStatTools}.cs`, `Assets/Game/Data/*`, `Assets/Resources/**`, `Assets/Game/Prefabs/Managers/*`, `Assets/InputSystem_Actions.inputactions`, and the relevant ProjectSettings.

**Method:** I read every in-scope file in full. I traced the boot, new game, save, load and apply flows across files outside my scope where the save pipeline crosses them (`EncounterManager`, `QuestManager`, `OpenWorldGalaxyManager`, `CombatManager`, `ShipData`/`VoxelData`, `ShipEditorManager`, and the UI views). I matched script GUIDs from `.meta` files against every `.unity`, `.prefab` and `.asset` file to tell used code from dead code, and I grepped the whole tree for every public symbol before calling it dead.

**Legend:** **[V]** means verified by reading the code or asset at the cited line. **[I]** means inference from that code, not observed at runtime. All paths are relative to the repo root.

---

## 0. TL;DR: the ten things that matter most

1. **Main cause of "my save is gone": a content-fingerprint gate rejects saves.** [V] `LoadSlot` refuses any save if a fingerprint over *all item keys, faction IDs, every SolarSystemData and SolarSystemEntity in all Resources folders, `Application.version`, and the number of entries in the starting loot table* differs from the current build. It also refuses any save whose schema version is not exactly 3. Adding one item, renaming an entity, bumping the version, or editing `CreativeModeLootTable` rejects every existing save. The start menu then fails silently (`Assets/Game/Core/Persistence/SaveManager.cs:513-526`, `:788-961`, `Assets/Game/UI/StartView.cs:140-144`).
2. **`Player.ImportState` replaces the `Inventory` object on every load** (`Assets/Game/Core/Character/Player.cs:196`) [V]. Everything subscribed to the old inventory is orphaned:
   - the notification/event relay (`InventoryNotificationRelay`, which subscribes once at boot);
   - the player's own ShipItem-to-fleet conversion (`Player.cs:62`, subscribed only in the constructor);
   - the HUD in `GalaxyView` (subscribed once in `OnEnable` of the DDOL managers prefab).
3. **Ship damage is never persisted.** [V] `CombatManager.CaptureShipDamage` has zero callers (`Assets/Game/Core/Combat/CombatManager.cs:173`). Destroyed voxels live only in ECS, so the ship comes back intact after every save/load or respawn.
4. **Inventory layout, toolbar and capacity are not saved.** [V] `ExportState` writes a packed list of (key, amount) and `ImportState` refills from slot 0 (`Player.cs:144-229`). The hotbar (inventory slots 0..8) is reshuffled on every load. `ToolbarInventory` is always written empty. Stacks beyond the 60th are silently dropped because the direct-indexer restore path never expands capacity.
5. **The ship blob cache key ignores rotation, colour and modules, and treats unresolved block IDs as 0.** [V] (`Assets/Game/Core/Game Logic/GameDatabase.cs:62-74`, `Assets/Game/Core/VoxelShip/ShipData.cs:88-115`). Repainting or re-rotating blocks does not show on the respawned ship for the whole session. Different ships with the same voxel positions can share one blob. The cache is never cleared. This is a strong candidate for the owner's "visual bugs".
6. **Loading from the pause menu while in the galaxy loses the saved position.** [V mechanism] `OpenWorldGalaxyManager.Update` writes the *current* position into `CurrentSaveData` every frame while the async reload is in flight, after `LoadSlot` has already swapped in the loaded state.
7. **There is no autosave and no save-on-quit.** [V] `QueueAutosave` is never called and there is no `OnApplicationQuit` or `Application.quitting` handler anywhere. Progress since the last encounter checkpoint is lost on quit.
8. **Roguelike-era flows still wipe progress.** [V] Defeat trims the fleet to the flagship, resets position and saves. "Continue" after a run ends calls `InitializeFreshRun`: it wipes inventory, fleet and quests and grants the **CreativeModeLootTable** plus 5000 credits (`Assets/Game/Core/Managers/EncounterManager.cs:483-585`, `Assets/Resources/GameSettings/NewGameSettings.asset:16`).
9. **Runtime ScriptableObject mutation and fragile IDs.** [V]
   - `GameDatabase.RegisterItems` writes `RegistryKey` and `Name` into item SOs at runtime.
   - DEV_MODE ship editing writes voxel data and price into `ShipItem` SOs at runtime.
   - Runtime item ID `0` is both a valid item and the "unresolved" default (`[NonSerialized] int`).
   - `ShipData.PrebakeStats` resolves IDs on a `foreach` copy, so every unresolved voxel is priced as runtime item 0.
   - The galaxy `Seed` is never assigned, so every campaign uses seed 0.
10. **Settings and input are half-wired.** [V]
    - Music volume compounds towards 0 on every slider tick, and `AudioManager` never subscribes because it wakes before `SettingsManager`.
    - Brightness, mouse sensitivity, invert-Y and key rebinds are stored but never consumed or persisted.
    - All four input action maps are always enabled with overlapping bindings (Tab fires three actions).
    - 36 legacy `Input.*` calls bypass the narrative input lock.

**Not a cause (verified):** Enter Play Mode Options are **disabled**, so domain reload happens on every Play and static state does not leak between Play sessions today (`ProjectSettings/EditorSettings.asset:26`, `m_EnterPlayModeOptionsEnabled: 0`). If the owner turns on fast Enter Play Mode for DOTS iteration, the statics listed in section 2.7 will all leak. In particular `GameInitializer.isGameInitialized` would stay `true`, so managers would never be instantiated.

---

## 1. Third-party dependencies used by in-scope game code (not audited in depth)

| Plugin | Used by game code? | Evidence |
|---|---|---|
| QFSW Quantum Console | Yes, 3 `[Command]`s plus an unused `using` | `Assets/Game/Core/Managers/Global.cs:10`, `PauseManager.cs:9`, `ShipEditorManager.cs`; `UIManager.cs:2` (`using QFSW.QC` with no command) |
| DOTween (Demigiant) | Yes, 6 files (UI) | `grep "using DG.Tweening"` |
| Microlight MicroBar | Yes, 1 file | `grep "using Microlight.MicroBar"` |
| AYellowpaper SerializedCollections | **No usages in `Assets/Game`** | grep for `SerializedDictionary|AYellowpaper` returns nothing. Removable. |
| ParrelSync (two folders: `Assets/ParrelSync`, `Assets/Plugins/ParrelSync`) | Editor-only; left over from the netcode experiment (commit `5d666a5e`) | duplicate install |
| FastScriptReload | Editor tool | hot-patching in play mode keeps static and SO state, which confuses "reset" debugging [I] |
| Newtonsoft JSON | Available transitively (`com.unity.nuget.newtonsoft-json` 3.2.1 in `Packages/packages-lock.json`) but **unused**; saves use `JsonUtility` | n/a |
| Addressables / Localization | Localization is used by `SettingsManager`/`SettingsView`; Addressables only through Localization | no direct `Addressables.` calls |
| `CartoonFX.RotateObject` (JMO, copyright notice) | Copied into `Assets/Game/Core/RotateObject.cs`; used by 5 prefabs | third-party code living in game code |

---

## 2. Current architecture map

### 2.1 Boot order (as built) [V]

```
Any scene (StartScene / GalaxyMapScene / ShipEditorScene) contains GameInitializer.prefab
  GameInitializer.Awake  (PersistentSingleton, DDOL)          Game Logic/GameInitializer.cs:17-58
    if (!static isGameInitialized):
      Global.DEV_MODE = start_dev_mode  (true only in ShipEditorScene override, ShipEditorScene.unity:135)
      GameDatabase.Initialize(gameSettings, voxelVisualData)  GameDatabase.cs:94-108
        RegisterItems()      Resources.LoadAll<ResourceItem>("Items"), sort, assign runtime ids 0..N-1, MUTATE SOs
        RegisterAbilities()  Resources.LoadAll<AbilitySO>("Abilities")
        InitPlayer()         PlayerInfo = new Player()  (new Inventory(60))
        RegisterFactions()   Resources.LoadAll<FactionData>("Factions")
        EnsureFactionRelationsInitialized()
        LoadOpenWorldConfig() Resources.Load("Galaxies/OpenWorldGalaxyConfig")
        InventoryNotificationRelay.Initialize(PlayerInfo)   subscribes to THIS Inventory instance only
      Instantiate(GAME MANAGERS PREFAB)  one GameObject carrying 9 managers, in component order:
        UIManager, CombatManager*, InputManager, PauseManager*, EncounterManager,
        AudioManager, SettingsManager, DialogManager, QuestManager   (* = PersistentSingleton)
        Child UIDocuments: all ViewUI pages (GalaxyView, InventoryView, PauseView, ...)
      #if UNITY_EDITOR && DEV_MODE: SaveManager.Instance.AttemptAutoLoadMostRecentSlot()
  SaveManager: in NO scene or prefab. Created lazily by PersistentSingleton.Instance on first access
               (StartView/PauseView/EncounterManager), as "SaveManagerAutoCreated".
```

Evidence for component order: the root `m_Component` list in `Assets/Game/Prefabs/Managers/GAME MANAGERS PREFAB.prefab:233-241`, resolved to script GUIDs. Evidence that SaveManager is in no asset: GUID `8af38999bd85e49428408cb2b33e0d34` matches no `.unity`, `.prefab` or `.asset`.

### 2.2 Singletons and service locators [V]

| Type | Pattern | DDOL | Notes |
|---|---|---|---|
| `GameDatabase` | `static class` with ~20 static fields | n/a | Registry, PlayerInfo, CurrentSaveData, settings refs and blob cache are all global mutable statics |
| `GameInitializer`, `SaveManager`, `PauseManager`, `CombatManager` | `PersistentSingleton<T>` | yes | `Instance` **auto-creates** a GameObject if none is found (`Assets/Game/Core/Utils/PersistentSingleton.cs:13-36`) and special-cases a non-existent `GalaxyManager` type (`:22-26`) |
| `UIManager` | `public static UIManager Instance;` field | via root | Destroys its whole GameObject if it is a duplicate (`UIManager.cs:54-58`) |
| `InputManager`, `AudioManager`, `SettingsManager`, `EncounterManager`, `DialogManager`, `QuestManager` | hand-rolled `Instance` plus `DontDestroyOnLoad(gameObject)` | yes | All live on the same GameObject, so `Destroy(gameObject)` on a duplicate kills a whole manager root |
| `OpenWorldGalaxyManager` | scene-local `Instance` | no | Lives in GalaxyMapScene |
| `Global` | `static bool DEV_MODE` | n/a | Toggled by a QC console command |
| `PauseManager.isGamePaused` | `static bool` | n/a | Shared by every view |
| `InventoryNotificationRelay` | `static class` with `_isSubscribed` | n/a | `Assets/Game/UI/InventoryNotificationRelay.cs:8-28` |
| `SceneLoader` | `static class`, `static AsyncOperation` | n/a | |
| `SimplePool` | `static Dictionary<GameObject,Pool>` | n/a | |
| `GameEventBus` | static event bus (Quests folder) | n/a | Used as the save capture/apply hook (`BeforeSave`, `SaveDataLoaded`) |

### 2.3 Data definitions [V]

- **Authoring:** ScriptableObjects in `Assets/Resources/**`, 158 `.asset` files in total. Items are `ResourceItem` with subclasses `BlockItem`, `CoreBlockItem`, `WeaponBlockItem`, `MirrorBlockItem`, `ShieldBlockItem`, `PortalBlockItem`, `SlottableItem`, `PaintBucketItem` and `ShipItem`.
- **Item identity:**
  - `ResourceItem.RegistryKey` (string) is the persistent key. All 69 item assets have explicit keys such as `block_hull` and `ship_frigate`; none uses the `core:` prefix that the code comments mention.
  - The runtime ID is the array index after sorting by asset name with `StringComparison.Ordinal` (`GameDatabase.cs:190-227`).
- **Loading:** `Resources.LoadAll` / `Resources.Load` with string paths (17 call sites). Paths that do not exist:
  - `"Portraits"`: `Assets/Game/UI/StartView.cs:73`, `Player.cs:234`
  - `"NarrativeImages/..."`: `Assets/Game/UI/Pages/NarrativeView.cs:184`
  - `"Materials/OrbitLineMaterial"`: `Assets/Game/Core/Galaxy Map/OpenWorldSolarSystemGenerator.cs:346`

  None of these folders exist under `Assets/Resources` (its folders are Abilities, Audio, Encounters, Factions, Galaxies, GameSettings, Items, Loot Tables, Quests, UI).
- **Ship templates:** `ShipItem.ShipVoxelData` is a `ShipData` holding a `List<VoxelData>` serialized as YAML, about 10 lines per voxel. `ship_vestari.asset` is 162 KB.
- **Blobs:** `ShipBlobBuilder` builds ship blobs at spawn. `BlockBlobBuilder` builds a block database blob from the runtime registry. The `VoxelPrefabAuthoring` baker builds a mesh map keyed by `string.GetHashCode()` (`Assets/Game/Core/VoxelShip/VoxelPrefabAuthoring.cs:91-93`).
- **JSON and CSV in `Assets/Game/Data`:** `GameData.json`, `ItemsData.json` and `CombatEncountersData.json` are exports from `GameDataExporterWindow`. They contain `instanceID` object references and the old int `Id` fields, and no code or asset reads them. `NarrativeEvents.csv` uses the old numeric item IDs (`ADD_ITEM(500, 1)`) and is not referenced; the importers read `Data/Narrative/*.json` instead.
- **Stale serialized fields:** 48 assets in `Assets/Resources` still carry an orphaned `Id:` int from before the item-ID removal (e.g. `Assets/Resources/GameSettings/NewGameSettings.asset:15`).

### 2.4 Save pipeline as built [V]

**On-disk layout:** `Application.persistentDataPath/` (company "Frank", product "Spaceship Game")

```
saves/profiles/<profileId>/profile_descriptor.json
saves/profiles/<profileId>/slots/<slotId>/descriptor.json        (SaveDescriptorData, pretty JSON)
                                         /state.json             (SaveStateData{Version=3, CampaignState}, JsonUtility)
                                         /state.bak.json         (previous state)
                                         /state.tmp.json         (transient)
                                         /audit_save_state.json  (debug snapshot, ALWAYS on)
                                         /audit_load_state.json
                                         /audit_history.log      (append-only, grows forever)
ShipThumbnails/<ShipName>_<shipId>.png    (outside slots; ABSOLUTE path stored in save)
ShipTemplates/*.json                      (ShipEditorManager, separate format)
PlayerPrefs: audio/video/input settings
```

**What is in `CampaignStateData`** (`Assets/Game/Core/Persistence/GameSaveData.cs:100-186`):

| Field | Written by gameplay? | Notes |
|---|---|---|
| `Seed` | **Never assigned** (grep for `\.Seed\b`) | Galaxy is always seed 0 |
| `IsInOpenWorld`, `OpenWorldPlayerPosition` (Vector2) | yes, OpenWorldGalaxyManager | 2D only; no rotation or system anchor |
| `SolarSystemSaveDataList[].EntityStates` | yes, OpenWorldGalaxyManager | per-system delta of destroyed/interacted entities (good idea) |
| `SpawnedEntityRecords`, `ReplacementRecords`, `RuntimeSpawnCounter`, `PlayerAnchorState`, `CameraState`, `EntityStateEntryData.HasTransform/Position/EulerRotation` | **no** | dead schema |
| `PlayerState` (Currency, Inventory list, Fleet list of `Ship` with full voxel lists, FlagshipIndex, AbilityLoadout) | yes, `Player.ExportState` | `Fuel` and `ToolbarInventory` are never filled |
| `SeenEncounters`, `WorldState`, `PersistentWorldState`, `QuestState`, `IntroPhase`, `FirstSpawnAnchorEntityId`, `FactionRelations` | yes | |
| `HasSpawnedInCampaign`, `PersistentWorldMutations`, `Extensions` (both levels), `TotalPlayTimeTicks` | **no** | dead schema |

**What is not saved at all:**
- ship damage (see B3);
- inventory slot positions, capacity and toolbar;
- merchant stock;
- mined or destroyed asteroid voxels;
- NPC fleet positions;
- the player's 3D transform, rotation and camera;
- the galaxy seed;
- key rebinds;
- locale.

**New game** (`StartView.StartNewGame` calls `SaveManager.StartNewGame`, `SaveManager.cs:363-397`):
1. Create a new manual slot named after the commander, then `SetActiveSlot`.
2. `GameDatabase.CurrentSaveData = new GameSaveData()`.
3. `EncounterManager.InitializeFreshRun()`: clears inventory and fleet, grants `GameSettings.StartingItems` (= `CreativeModeLootTable`, 37 entries), and forces currency to 5000 (`EncounterManager.cs:557-585`).
4. Publish `SaveDataLoaded`, then `SaveActiveSlot(Manual)`, then load GalaxyMapScene.

**Save** (`SaveActiveSlot`, `SaveManager.cs:399-471`):
1. `saveData = CurrentSaveData ?? new` (the new one is **not** assigned before step 3).
2. `PlayerState = PlayerInfo.ExportState()`. The exported `Fleet` is the live list, an alias, not a copy (`Player.cs:152`).
3. Publish `BeforeSave`. EncounterManager and QuestManager write into `GameDatabase.CurrentSaveData.CampaignState`.
4. `BuildCurrentBaseline`: two `Resources.LoadAll<T>(string.Empty)` calls (all Resources folders), plus SHA256 fingerprints.
5. Read the descriptor from disk, fill the summary, write the audit snapshot (pretty JSON plus a `Debug.Log` of the whole snapshot), write the state (temp file, copy to `.bak`, delete, move), write the descriptor, read and rewrite the profile descriptor.

**Load** (`LoadSlot`, `SaveManager.cs:473-565`):
1. Read the descriptor and state (falling back to `.bak`).
2. **Reject** if `Version != 3` or any fingerprint differs.
3. Write the load audit and compare its hash.
4. `CurrentSaveData = loaded`.
5. `pending = DeepClone(state)` through a JsonUtility round-trip.
6. `TryApplyPendingCampaignState()`. It applies only if the active scene is GalaxyMapScene; otherwise it waits for `sceneLoaded` or `Update`.
7. Apply = `PlayerInfo.ImportState(...)` (**replaces the Inventory**), swap `CampaignState` to the clone, publish `SaveDataLoaded`. Encounter and Quest managers re-hydrate; OpenWorldGalaxyManager reads `CurrentSaveData` lazily.

**Save points** (all synchronous on the main thread):
- `EncounterManager.EndEncounter` checkpoint, after every encounter (`EncounterManager.cs:309-311`);
- `ExtractSuccess`, `EndRun`, `HandlePlayerDefeat`;
- pause-menu manual save (`Assets/Game/UI/PauseView.cs:86`);
- ship editor F5 save (`ShipEditorController.cs:178-188`).

### 2.5 Settings [V]
`SettingsManager` uses PlayerPrefs (`SettingsManager.cs:54-91`) and calls `PlayerPrefs.Save()` on every setter call, i.e. every slider tick. Consumers:
- **Volumes:** read by `AudioManager` at play time.
- **Brightness:** `BrightnessController` is the only consumer and it is in no scene or prefab (GUID `0a7944e9...` is unreferenced).
- **MouseSensitivity, InvertY:** no readers outside `SettingsView`.
- **ResolutionIndex:** saved, never applied.
- **Locale:** set, not persisted (`SettingsManager.cs:184-189`).
- **Rebinds:** not persisted; the persistence code is commented out (`Assets/Game/UI/SettingsView.cs:306-309`).

### 2.6 Input [V]
- `activeInputHandler: 2` (both systems; `ProjectSettings/ProjectSettings.asset:808`).
- Project-wide actions are `Assets/InputSystem_Actions.inputactions`, set as `com.unity.input.settings.actions` in `EditorBuildSettings.asset`.
- Maps: Player (9 actions), UI (13), Combat (6, including a placeholder "New action"), Editor (5, including "New action").
- `InputManager` caches 14 actions by **bare name** with `InputSystem.actions.FindAction("Fire")`. "Fire" exists in Player and Combat, so the first match wins (`InputManager.cs:39-52`).
- No action-map switching per context. The only gating is `SetNarrativeShortcutOnlyMode`, which disables the 14 cached actions (`InputManager.cs:151-174`).
- 36 legacy `Input.*` calls remain (FreeFlyCamera 12, ShipEditorController 11, ShipInputSystem 3, OrbitalCamera 3, ThirdPersonCamera 2, AsteroidMiner 2, and others). `ShipInputSystem` steers with `Input.GetAxis("Vertical"/"Horizontal")` (`Assets/Game/Core/VoxelShip/ShipInputSystem.cs:25-27`).

### 2.7 Statics that would leak if Enter Play Mode Options are enabled [V list]
`GameDatabase.*` (all registries, `PlayerInfo`, `CurrentSaveData`, `_shipBlobCache`, `SyncTransformDictionary`), `GameInitializer.isGameInitialized`, `PersistentSingleton<T>.instance`, `UIManager.Instance`, `InputManager.Instance`, `AudioManager.Instance`, `SettingsManager.Instance`, `PauseManager.isGamePaused`, `Global.DEV_MODE`, `InventoryNotificationRelay._isSubscribed`, `SimplePool.pools`, `SceneLoader.asyncLoad`, plus the out-of-scope `EncounterManager`, `QuestManager`, `DialogManager` and `OpenWorldGalaxyManager` instances and `GameEventBus` subscribers. None has a `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` reset.

---

## 3. Bugs

Severity: **Critical** = loses or corrupts player progress in normal play; **High** = visible wrong behaviour or data loss in common flows; **Medium** = wrong in specific flows or latent; **Low** = minor or edge.

| # | Severity | Confidence | Summary |
|---|---|---|---|
| B1 | Critical | High | Fingerprint/version gate rejects saves after almost any content change; start menu fails silently |
| B2 | Critical | High | `ImportState` replaces the Inventory object, orphaning relay, ship conversion and HUD subscribers |
| B3 | High | High | Ship damage never captured into save data |
| B4 | High | High | Inventory slot layout, toolbar and capacity not saved; >60 stacks dropped on load |
| B5 | High | High | Ship blob cache key ignores rotation, colour and modules; never cleared, so stale ship visuals |
| B6 | High | High (mechanism) | Pause-menu load: galaxy manager overwrites the loaded position during the async reload |
| B7 | High | High | No autosave, no save-on-quit |
| B8 | High | High | Roguelike defeat and run-end flows wipe fleet, inventory and quests; new game gets creative-mode loot |
| B9 | Medium | High | `ShipData.PrebakeStats` resolves IDs on a `foreach` copy, so wrong price and abilities |
| B10 | Medium | High | Runtime item ID 0 is ambiguous ("unresolved" vs first item); FleetView misprices |
| B11 | Medium | High | Galaxy seed never assigned, so every campaign is identical |
| B12 | Medium | High | Music volume compounds towards zero; AudioManager never subscribes to settings |
| B13 | Medium | High | Brightness, sensitivity, invert-Y, resolution, rebinds and locale stored but unused or not persisted |
| B14 | Medium | High | DEV auto-load leaves a stale pending apply that reverts ship-editor changes on entering the galaxy |
| B15 | Medium | High | SaveManager only ever auto-created; `PersistentSingleton.Instance` auto-creates on any access |
| B16 | Medium | High | Pause is one shared bool; closing any overlay unpauses while others are open |
| B17 | Medium | Medium | All input maps always enabled with conflicting bindings; legacy input bypasses the lock |
| B18 | Medium | Medium | Interactive rebinding of enabled project-wide actions likely throws |
| B19 | Medium | High | Thumbnail absolute paths in saves; recovery only works when the path is empty |
| B20 | Medium | High | Runtime SO mutation (registry keys, DEV ship editor) gives editor-vs-build divergence |
| B21 | Low-Med | High | Non-atomic state write window |
| B22 | Low | High | `BeforeSave` with a null `CurrentSaveData` loses manager state for that save |
| B23 | Low | High | Save audit: wrong "current system", ItemId always 0, null key aborts audit |
| B24 | Low | High | SceneLoader quirks; CombatScene in build list but missing |
| B25 | Low | High | Missing Resources paths (Portraits, NarrativeImages, Materials) |
| B26 | Low | High | Duplicate InputManager throws an NRE in `OnDisable` (latent) |
| B27 | Low | High | Merchant stock regenerated per dialog with unseeded RNG; never persisted |
| B28 | Low | Medium | `ShipData` finalizer disposes blobs on the GC thread; PrebakeStats creates unused blobs |
| B29 | Low | High | SimplePool cap counts pools, not instances |
| B30 | Low | High | Capacity constants disagree (59 UI slots vs 60 model slots); expanded slots invisible |
| B31 | Low-Med | Medium | `string.GetHashCode()` used as a baked key between editor and runtime |
| B32 | Low | High | Player identity fields unused or wrong (`Name`, `OnInitialized`, `CommanderName`) |

### B1: The content-fingerprint and exact-version gate silently rejects saves (Critical, High) [V]
- `LoadSlot` returns `null` when `stateData.Version != SaveStateSchemaVersion`. There is no migration path (`SaveManager.cs:513-518`).
- `ValidateCompatibility` (`SaveManager.cs:788-841`) compares four hashes:
  - **GameContentFingerprint** (`:856-940`): every item key, faction ID, `SolarSystemData` ID and `SolarSystemEntity` ID found by `Resources.LoadAll<T>(string.Empty)`.
  - **EnabledContentFingerprint** (`:942-945`): `Application.identifier|Application.version|core-content`.
  - **GenerationSettingsFingerprint** (`:947-961`): the seed plus **the number of entries in `GameSettings.StartingItems`**.
  - The descriptor's composite fingerprint.
- Any added or renamed item, faction, system or entity, any `bundleVersion` bump, or any edit to the size of `CreativeModeLootTable` makes every existing save unloadable.
- `StartView.LoadSlot` just `return`s on null with no message (`Assets/Game/UI/StartView.cs:140-144`). `PauseView` only logs (`Assets/Game/UI/PauseView.cs:107-111`).
- The descriptor still lists the slot, so the player sees saves that "do nothing", or starts a new game. This matches "data getting reset" in an actively developed game.
- **Fix now:** turn the gate into warnings. Keep the hashes only for diagnostics. Missing keys should degrade per item (drop with a log) rather than reject the whole save.

### B2: Load replaces the Inventory instance and orphans its subscribers (Critical, High) [V]
- `Player.ImportState` does `this.Inventory = new Inventory(new List<Item>(), 60);` (`Assets/Game/Core/Character/Player.cs:196`).
- These subscriptions are made exactly once, to the boot-time instance:
  - `Player.InitializePlayer`: `Inventory.OnItemChanged += HandleInventoryItemChanged` (`Player.cs:61-62`). After a load, **buying, looting or receiving a ShipItem no longer converts it into a fleet ship**. It stays as an inventory item (`Player.cs:68-92`).
  - `InventoryNotificationRelay.Initialize(PlayerInfo)`, called only from `GameDatabase.Initialize` (`GameDatabase.cs:107`, `InventoryNotificationRelay.cs:12-25`). After a load, item and currency toasts stop, and `GameEventBus` `inventory.item_changed` / `inventory.currency_changed` are no longer published. That may stall quest re-evaluation that happens on events [I].
  - `GalaxyView.OnEnable` → `RegisterInventoryEvents()` (`Assets/Game/UI/GalaxyView.cs:20-34, 65-73`). `GalaxyView` lives in the DDOL managers prefab and `ViewUI.DisplayView` toggles `style.display`, not the GameObject (`Assets/Game/UI/ViewUI.cs:29-33`). `OnEnable` therefore runs once, and the HUD fuel and scrap labels stop updating after any load.
- **Fix:** keep one `Inventory` for the session and add `Inventory.LoadFrom(state)` that mutates in place and raises one reset event. Same for `Fleet`.

### B3: Ship damage is never persisted (High, High) [V]
- `CombatManager.CaptureShipDamage(CombatShip)` is documented as "Call this … before saving game" (`Assets/Game/Core/Combat/CombatManager.cs:168-185`) but has **no callers** (grep).
- No `BeforeSave` participant reads ECS voxel survival.
- The `Ship.ShipVoxelData.voxels[i].IsDestroyed` flags that `FleetView` repair costs rely on are never set by open-world combat. Damage evaporates on save/load and on respawn [V]. Whether the live ECS ship also "heals" on despawn and respawn is [I] (it is rebuilt from `ShipData`).

### B4: Inventory layout, toolbar and capacity are not persisted (High, High) [V]
- `ExportState` iterates `Inventory.Items` and emits only non-null `(RegistryKey, quantity)`, with no slot index (`Player.cs:156-167`). `ToolbarInventory` is created empty and never filled (`Player.cs:149`). Capacity is not saved.
- `ImportState` fills slots from index 0 in list order (`Player.cs:198-225`).
- The toolbar is inventory slots 0..8 (`Assets/Game/UI/Pages/InventoryView.cs:58`, `ToolbarView.cs:23-35`), so the hotbar arrangement is rewritten on every load.
- Items beyond capacity 60: the direct indexer path never calls `CheckAndExpand` (it is only reached through `TryStackItem`, `Inventory.cs:147-153`). When the 60 direct slots are full, `TryAddItem` falls into `Add` → `TryAdd` → `false`, and the stack is **silently dropped** (`Player.cs:218-222`).
- Duplicate stacks of a stackable item are restored as separate stacks without merging (minor).

### B5: Ship blob cache produces stale or wrong visuals (High, High) [V]
- `GameDatabase.GetOrCreateShipBlob` caches by `ShipData.GetContentHash()` (`GameDatabase.cs:62-74`).
- The hash covers only `width/height/length/count` and `x,y,z,runtimeBlockId` of non-destroyed voxels (`Assets/Game/Core/VoxelShip/ShipData.cs:88-115`). It ignores `rotX/rotY/rotZ`, `colorIndex` and `moduleSlots`.
- `runtimeBlockId` is `[NonSerialized]` (`Assets/Game/Core/VoxelShip/ShipVoxel.cs:24-25`), so it is 0 for every voxel loaded from a save or copied from a `ShipItem` SO. The hash then ignores block types too.
- `ClearShipBlobCache` is never called.
- `CombatManager.CreateShipEntity` uses this cache for the player ship and NPCs (`CombatManager.cs:77`, called from `OpenWorldGalaxyManager.cs:399`).
- Consequences:
  - paint-bucket recolours and in-place rotations do not appear on the respawned ship for the rest of the session;
  - two different ship templates with identical cell positions share the first one's blob;
  - blobs accumulate for the whole session (leak).
- **Fix:** key the cache on a full content hash (including rotation, colour and resolved block IDs) or a per-ship revision counter, and dispose on despawn.

### B6: Loading from the pause menu while flying loses the saved position (High, mechanism High) [V]
1. `PauseView.LoadSlot` calls `SaveManager.LoadSlot`, which applies immediately because the active scene is GalaxyMapScene (`SaveManager.cs:563`, `:675-712`). It then starts `LoadSceneAsync(GalaxyMapScene)` (`PauseView.cs:104-114`).
2. Until the new scene activates (at least one frame, usually several), the old `OpenWorldGalaxyManager.Update` → `SavePlayerState()` writes `absolutePlayerPosition + current transform` into `GameDatabase.CurrentSaveData.CampaignState.OpenWorldPlayerPosition` every frame (`Assets/Game/Core/Galaxy Map/OpenWorldGalaxyManager.cs:159-197`). At this point that is the freshly loaded state.
3. The new scene's `InitializeOpenWorld` reads the overwritten value (`:74-82`).

### B7: No autosave and no save on quit (High, High) [V]
- `SaveManager.Update` services `autosaveQueued` (`:221-227`), but `QueueAutosave` (`:567-577`) has no callers.
- No `OnApplicationQuit`, `Application.quitting` or `wantsToQuit` exists anywhere in `Assets/Game`.
- Saves happen only at encounter end, manual save, ship-editor F5, extract, defeat and run end. Anything since then (position, entity states, loot picked up in flight, ship edits without F5) is lost on quit.

### B8: Roguelike-era flows still wipe progress (High, High) [V]
- `HandlePlayerDefeat` (`EncounterManager.cs:483-511`):
  - replaces the fleet with only the flagship (`PlayerInfo.Fleet = new List<Ship>{flagship}`, without unsubscribing ship events or raising `OnFleetChanged`);
  - zeroes `OpenWorldPlayerPosition`;
  - resets the galaxy manager;
  - saves with `SaveReason.Defeat`.
- `EndRun(false)` sets `awaitingRunRestart`. `HandleRunEndContinue` → `InitializeFreshRun` (`:513-555`) wipes `SeenEncounters`, `WorldState` and `QuestState`, then `InitializePlayerForFreshRun` (`:557-585`) clears inventory and fleet and gives starting loot plus `SetCurrency(5000)`.
- `GameSettings.StartingItems` points at **`CreativeModeLootTable`** (37 entries; `Assets/Resources/GameSettings/NewGameSettings.asset:16`, GUID `a00def4f…` = `Assets/Resources/Loot Tables/CreativeModeLootTable.asset`). Every new game ships with the creative-mode dev loot.
- In an open-world game these are "data resets" by design of the old run model. (EncounterManager is another auditor's file; listed here because it drives persistence.)

### B9: `ShipData.PrebakeStats` resolves IDs on a struct copy (Medium, High) [V]
- In `foreach (var voxelData in voxels) { … voxelData.ResolveRuntimeId(); var block = GameDatabase.GetItemByRuntimeId(voxelData.runtimeBlockId); … }` (`ShipData.cs:173-196`), the `foreach` variable is readonly. Calling a mutating method on it operates on a defensive copy (C# semantics), so `runtimeBlockId` stays 0.
- `GetItemByRuntimeId(0)` returns registry item 0. By the ordinal name sort that is `Module_Armor_Plating` [I, computed from the asset names: uppercase sorts first].
- Ship `BasePrice` (returned total) and `AbilityIds` are therefore computed from the wrong item for every voxel whose ID was not pre-resolved. That covers all voxels from SO YAML or save JSON.
- The editor tools `ShipItemEditor` "Recalculate Stats" (`Assets/Game/Editor/ShipItemEditor.cs:18`) and `ShipStatTools` (`Assets/Game/Editor/ShipStatTools.cs:21`) run exactly this on SO data and then `SaveAssets`. They bake wrong prices into assets.
- `ShipStatTools` also discards the returned price, unlike `ShipItemEditor`.

### B10: Runtime ID 0 is ambiguous (Medium, High) [V]
- Runtime IDs are 0-based array indices (`GameDatabase.cs:221`). The deserialized default of `runtimeBlockId` is also 0.
- Most callers use `<= 0` to mean "unresolved", which re-resolves item 0 every time (wasteful but correct): `ShipBlobBuilder.cs:55`, `ShipStatCalculator.cs:31`, `ShipEditorManager.cs:241`.
- `FleetView` uses `< 0` (`Assets/Game/UI/FleetView.cs:369-373`), so every unresolved voxel is treated as item 0 and repair cost is wrong.
- **Fix:** reserve 0 for "none/air" and start real IDs at 1.

### B11: Galaxy seed never assigned (Medium, High) [V]
- `CampaignStateData.Seed` (`GameSaveData.cs:106`) has no writer anywhere (grep `\.Seed\b`).
- `GetOpenWorldSystemSeed` uses it whenever save data exists, so every campaign gets `baseSeed = 0`. The `DateTime.Now.Ticks` fallback (`OpenWorldGalaxyManager.cs:118-129`) is effectively dead.
- The baseline's `MapSeed` is always 0.

### B12: Audio settings: compounding volume and missed subscription (Medium, High) [V]
- `UpdateAudioSettings` does `musicSourceA.volume = GetEffectiveMusicVolume(musicSourceA.volume)`, i.e. `v = v * master * music` (`Assets/Game/Core/Audio/AudioManager.cs:81-93`). Each settings event shrinks the volume multiplicatively. Dragging the slider collapses it towards 0, and raising the slider cannot raise it. Only source A is touched, but B may be the active source after a crossfade (`:210`).
- `AudioManager.Initialize` subscribes only `if (SettingsManager.Instance != null)` (`:66-70`). On the managers prefab AudioManager is component 6 and SettingsManager is component 7. Awake runs in component order in practice, so `SettingsManager.Instance` is still null and **AudioManager never subscribes** [V order; I "in practice" ordering, since Unity does not document it]. Music volume changes then only take effect on the next `PlayMusic` crossfade. That also masks the compounding bug.
- No `AudioMixer` exists in the project (`find -name "*.mixer"` returns nothing). All volume maths is manual per source, so currently playing SFX ignore setting changes.

### B13: Settings that do nothing or do not persist (Medium, High) [V]
- **Brightness:** `BrightnessController` is attached nowhere (its GUID is unreferenced).
- **MouseSensitivity and InvertY:** no consumers in cameras or controllers.
- **ResolutionIndex:** persisted but `ApplyVideoSettings` never applies it (`SettingsManager.cs:149-159`).
- **Rebinding:** overrides are not saved (`SettingsView.cs:306-309`).
- **Locale:** not saved.
- To the player, all of these look like settings that reset.

### B14: DEV auto-load reverts ship-editor work (Medium, High) [V]
- `GameInitializer` (DEV_MODE, so ShipEditorScene only) calls `AttemptAutoLoadMostRecentSlot` (`GameInitializer.cs:51-56`).
- `LoadSlot` sets `pendingCampaignState`/`hasPendingCampaignApply`. It does not apply because the active scene is not the World scene. `AttemptAutoLoad…` then *also* imports immediately (`SaveManager.cs:668-671`).
- The pending clone is only cleared when GalaxyMapScene loads (`:675-712`). `SaveActiveSlot` never clears or refreshes it.
- Edits, purchases and paint made in the editor scene, **even after an F5 save**, are overwritten by the stale clone when you fly to the galaxy.

### B15: Singleton infrastructure (Medium, High) [V]
- `PersistentSingleton<T>.Instance` creates `"<T>AutoCreated"` whenever no instance is found (`PersistentSingleton.cs:17-31`):
  - `SaveManager` is **only** ever auto-created; it is in no prefab or scene. Its inspector fields (`autosaveIntervalSeconds`, `emitSaveLoadAuditLogs`, `SaveManager.cs:188-189`) can never be configured, so audit logging is always on in builds.
  - Any access during teardown spawns stray objects [I].
  - Every `PersistentSingleton.Instance?.` null check is meaningless.
- The `"GalaxyManager"` special case refers to a class that no longer exists.
- `InitializeSingleton` destroys `this.gameObject` for duplicates (`:58-63`). Combined with nine managers on one GameObject, any duplicate manager destroys its whole root. That is safe today only because the prefab is instantiated once behind a static flag.

### B16: Pause is one shared boolean (Medium, High) [V]
- Every `UIManager.Toggle*` calls `PauseManager.PauseGame(show)` (`UIManager.cs:137-330`), which sets `isGamePaused` and `Time.timeScale` (`PauseManager.cs:10-15`).
- Example: open Intel (paused), open Inventory, close Inventory. `PauseGame(false)` runs while Intel is still open.
- `ToggleNarrativeView` deliberately does not pause.
- `InputManager.HandleBack` decides between opening pause and closing everything from `CurrentPage` plus the bool (`InputManager.cs:96-112`).
- **Fix:** a reference-counted pause.

### B17: Input contexts (Medium, Medium) [V bindings / I gameplay impact]
- Project-wide maps are all enabled at once. Conflicts from the `.inputactions` dump:
  - **Tab** → `Player/Continue`, `UI/IntelPage` and `Combat/ChangeCamera`;
  - **Space** → `Player/Fire` and `Combat/Fire`;
  - **D/Right** → `Player/Next` and `UI/Navigate`, plus legacy `Input.GetAxis("Horizontal")` steering;
  - **A/Left** → `UI/Previous` and `UI/Navigate`;
  - **1/2** → `UI/Previous`/`Player/Next` and `Combat/SelectWeaponGroup`;
  - **F5** (`Editor/SaveShipData`) is live in every scene.
- `SetNarrativeShortcutOnlyMode` disables only the cached actions. Legacy polling in `ShipInputSystem` (`:25-27`), `FreeFlyCamera`, `OrbitalCamera` and others still moves the ship or camera during "locked" intro sequences [I].

### B18: Rebinding enabled actions (Medium, Medium) [I]
- `SettingsView.StartRebinding` calls `PerformInteractiveRebinding()` on project-wide actions without disabling them first (`SettingsView.cs:292-311`).
- In Input System 1.x this raises `InvalidOperationException` for enabled actions (from the package's documented behaviour; not reproduced here).
- Rebinds are not saved either (B13).

### B19: Thumbnail paths are absolute and the recovery is narrow (Medium, High) [V]
- `ThumbnailPath = Path.Combine(Application.persistentDataPath, "ShipThumbnails", …)` is stored verbatim in the save (`Player.cs:376-382`, `ShipEditorManager.cs:474-481`).
- `EnsureShipThumbnailLoaded` attempts recovery only `if (string.IsNullOrWhiteSpace(ship.ThumbnailPath))` (`Player.cs:286`). A save moved between machines, users, product names or cloud sync keeps a stale absolute path and never recovers.
- Thumbnails live outside slot folders and are never deleted with a slot (`DeleteSlot`, `SaveManager.cs:618-643`).

### B20: Runtime ScriptableObject mutation (Medium-High impact on editor-vs-build divergence, High) [V]
- `GameDatabase.RegisterItems` writes `item.RegistryKey = "core:" + …` when empty and `item.Name = item.name` when empty (`GameDatabase.cs:204-213`). In the editor these in-memory edits survive across Play sessions until reimport, and get written to disk whenever something else dirties and saves the asset. In builds they reset every launch.
  - It is currently mostly a no-op because all items have keys.
  - But `PaintBucketGenerator` creates buckets **without** a key (`Assets/Game/Core/Editor/PaintBucketGenerator.cs:29-38`) and overwrites the existing assets at the same path. If re-run, the 17 paint buckets' keys change from `PaintBucket_05_Yellow` to the runtime-generated `core:paintbucket_05_yellow`, and saved inventories lose them. Its log also says "16" while it creates 17.
- `ShipEditorManager` (DEV_MODE) writes `TestShip.ShipVoxelData = shipData; TestShip.BasePrice = price;` into the `ShipItem` SO at runtime (`ShipEditorManager.cs:555-596`). In the editor that is persisted with `SetDirty`/`SaveAssets`. In a build with DEV_MODE toggled via QC it mutates the in-memory SO: it resets on restart, and every `new Ship(shipItem)` in that session uses the mutated template.

### B21: Non-atomic state write (Low-Medium, High) [V]
- `WriteStateAtomically` writes the temp file, then `File.Copy(state → bak)`, `File.Delete(state)`, `File.Move(tmp → state)` (`SaveManager.cs:963-994`).
- A crash between Delete and Move leaves no `state.json`. `TryReadState` then falls back to `.bak`, which is one save older, and ignores the newer `.tmp`.
- `File.Replace(tmp, state, bak)` does this atomically on NTFS and APFS.

### B22: `BeforeSave` with no current save (Low, High) [V]
- `SaveActiveSlot` uses `CurrentSaveData ?? new GameSaveData()` (`:405`) and only assigns it to `GameDatabase.CurrentSaveData` after writing (`:462`).
- `BeforeSave` handlers write into `GameDatabase.CurrentSaveData?.CampaignState` (`EncounterManager.cs:97-104`, `QuestManager.cs:892-899`). If it was null, quest and encounter state is not in that save.

### B23: Save-audit correctness (Low, High) [V]
- `ResolveCurrentSolarSystemSaveData` always returns list element `[0]`, not the current system (`SaveManager.cs:1410-1423`).
- `SaveAuditInventoryItemData.ItemId` is never set, so the `OrderBy(ItemId)` is meaningless (`:1294-1338`).
- `byItemId[itemId]` throws on a null `ItemKey`. The exception is caught and the whole audit is dropped with a warning.
- The full pretty-printed audit JSON is `Debug.Log`ged on every save and load (`:1067`).
- The audit subsystem is ~560 of 1749 lines (`:54-167`, `:1031-1479`).

### B24: SceneLoader (Low, High) [V]
- `LoadSceneAsync` sets `allowSceneActivation = false`, polls, and at `progress >= 0.9` sets it to true and **breaks**. The coroutine ends before activation, so callers cannot await completion (`Assets/Game/Core/SceneLoader.cs:15-35`).
- It logs every frame (`:17-29`). A single static `asyncLoad` is shared by concurrent loads.
- `CombatScene.unity` is enabled in Build Settings but the file does not exist (`ProjectSettings/EditorBuildSettings.asset:15`).
- `BattleSceneName`, `ManagersSceneName` ("PersistentScene", which does not exist) and `SpaceStationScene` are unused.

### B25: Missing Resources paths (Low, High) [V]
`Resources.LoadAll<Texture2D>("Portraits")` (`StartView.cs:73`, so the portrait picker is empty), `Resources.Load<Texture2D>($"Portraits/…")` (`Player.cs:234`), `NarrativeImages/…` (`NarrativeView.cs:184`) and `Materials/OrbitLineMaterial` (`OpenWorldSolarSystemGenerator.cs:346`) all return null. None of those folders exist under `Assets/Resources`. The chosen portrait (`StartView.selectedPortraitName`) is never stored on `Player.ProfileImageName` or saved.

### B26: InputManager duplicate path (Low, latent, High) [V]
A duplicate `InputManager` calls `Destroy(gameObject); return;` before assigning any action (`InputManager.cs:28-37`). Its `OnDisable` then dereferences `backAction`, `inventoryAction` and `fleetPageAction` (`:86-94`) and throws an NRE. Subscriptions are made in `Awake` but removed in `OnDisable`, with no `OnEnable` counterpart, so disabling and re-enabling the manager loses input handling.

### B27: Merchant stock not persisted, unseeded RNG (Low, High) [V]
`DialogManager` creates `new Merchant(narr.lootTable, itemsToSell)` every time the action runs (`DialogManager.cs:563`; also `NarrativeActionRuntime.cs:324`). `LootSystem` uses unseeded `UnityEngine.Random` (`Assets/Game/Core/Loot/LootSystem.cs:27, 33, 86`). Stock re-rolls on every visit and is not part of the save.

### B28: Blob lifetime in ShipData (Low, Medium) [V code / I risk]
- `~ShipData()` disposes `_blobAsset` from the GC finalizer thread (`ShipData.cs:139-146`).
- `PrebakeStats` creates a blob on every call (`:252-254`), but runtime spawning uses the `GameDatabase` cache, not `ShipData.GetOrCreateBlobAsset()` (no external callers). Those blobs are pure waste.
- Every `ShipData` instance (copies, JSON round-trips, DeepClone) is finalizable, which adds GC overhead.

### B29: SimplePool cap (Low, High) [V]
`if (pools.Count >= MaxPoolSize) return null;` (`Assets/Game/Core/Utils/SimplePool.cs:70-73`) counts *prefab types*, not instances. Instance count is unbounded, and after 100 prefab types every `Spawn` returns null, so callers such as `ShipEditorController.cs:245` would throw an NRE.

### B30: Capacity constants disagree (Low, High) [V]
- `INVENTORY_CAPACITY (50) + TOOLBAR_CAPACITY (9) = 59` UI slots (`InventoryView.cs:17`), while the model uses 60 (`Player.cs:61, 196`; the `Inventory` default is 60).
- `CheckAndExpand` grows the model by 10 when fewer than 5 slots are free (`Inventory.cs:157-176`). Slot 59 and all expanded slots are invisible in the UI [V UI sizing; I visible effect].

### B31: Non-deterministic hash as a baked key (Low-Medium, Medium) [V code / I current impact]
- `VoxelPrefabAuthoring` maps `key.GetHashCode()` to a mesh index at bake time (`VoxelPrefabAuthoring.cs:91-93`). `BlockBlobBuilder` looks the same hash up at runtime (`BlockBlobBuilder.cs:33-45`).
- .NET documents `string.GetHashCode` as not stable across processes or runtimes. It happens to be stable on Mono today. Moving to CoreCLR, or any runtime with randomized hashing, would map every block to mesh 0.
- **Fix:** use a stable 32-bit FNV/xxHash, or the content numeric ID.

### B32: Player identity and minor issues (Low, High) [V]
- `Player.Name` is never set.
- `OnInitialized` is invoked inside the constructor, before anyone can subscribe (`Player.cs:45, 65`).
- `PopulateDescriptorSummary` sets `CommanderName = ProfileName` ("Commander"), not the name the player typed (`SaveManager.cs:734-736`).
- `Ship.Id` is a 32-bit hash of a GUID (`Ship.cs:62-67`). It is fine for now but is used as the thumbnail file key.
- `Player.AddShipToFleet` calls `Sprite.Create` for every notification and never destroys it (`Player.cs:118-122`).

---

## 4. Dead, deprecated and duplicate code (with proof)

"Proof" means a grep across `Assets/Game` (code) plus a GUID search across all `.unity`, `.prefab` and `.asset` files under `Assets`.

### 4.1 Whole files or types

| Item | Proof | Action |
|---|---|---|
| `Assets/Game/Core/Inventory/InventoryData.cs` | no references outside its file | delete |
| `Assets/Game/Core/Inventory/BindableProperty.cs` | no references | delete (or keep if adopting UI Toolkit binding) |
| `Assets/Game/Core/Utils/SerializableGuid.cs`, `GuidExtensions.cs` | only used by dead `InventoryData` | delete or move to the new save layer |
| `Assets/Game/Core/Utils/Editor/InstanceCombiner.cs` | MonoBehaviour in an Editor folder; GUID unreferenced | delete |
| `Assets/Game/Core/Utils/TimelineSpeed.cs` | GUID unreferenced; only `GetComponent<TimelineSpeed>()` probes in `ShipSelectionController.cs:71-79` | delete with the ship-selection intro |
| `Assets/Game/Core/Game Logic/LootObject.cs`, `StorageObject.cs` | GUID unreferenced, no code references; empty `OnInteract` | delete |
| `Assets/Game/Core/Audio/CombatAudioListener.cs` | GUID unreferenced; "Combat scene" era | delete |
| `Assets/Game/Visuals/BrightnessController.cs` | GUID unreferenced, so the brightness setting is dead | wire it up or delete |
| `Assets/Game/UI/TradeView.cs` (and with it `Inventory.CreateSnapshot/RestoreFromSnapshot/CombineFrom`, `InventorySnapshot`) | TradeView GUID unreferenced; no `TradeView` mentions elsewhere | delete or revive |
| `Assets/Game/Data/GameData.json`, `ItemsData.json`, `CombatEncountersData.json` | GUIDs unreferenced; contain `instanceID` refs and old int `Id`s | delete |
| `Assets/Game/Data/NarrativeEvents.csv` | GUID unreferenced; old numeric `ADD_ITEM(500, 1)` | delete (JSON is the source) |
| `Assets/Game/Data/GameDescription.txt` | describes the old roguelike | replace with the current pitch |
| `Assets/Game/Editor/GameDataExporterWindow.cs` | default path `Assets/Game/Resources` does not exist; output contains meaningless `instanceID`s | delete or rewrite as a content validator |
| `Assets/Resources/Loot Tables/StartShipsTable.asset` | 0 entries; GUID unreferenced | delete |
| `AYellowpaper SerializedCollections` plugin | no usages in `Assets/Game` | remove |
| Duplicate `ParrelSync` install (`Assets/ParrelSync`, `Assets/Plugins/ParrelSync`) | netcode experiment leftover | remove one or both |

### 4.2 Dead members inside live files

- **`GameDatabase`** (`Assets/Game/Core/Game Logic/GameDatabase.cs`):
  - `ShipMeshDictionary` (`:40, :97`); `RegisterVisualData`, `TryGetVisual`, `_registry`, `VisualData` struct (`:41, :136-155, :675-679`): no callers.
  - `RegisterSyncTransform`, `TryGetSyncObject`, `UnregisterSyncTransform` (`:157-176`): no callers. `GameObjectSyncSystem` therefore always early-outs on a null dictionary (`GameObjectSyncSystem.cs:28`).
  - `HandleScrapGained` (`:131-134`): its subscription is commented out.
  - `ClearShipBlobCache` (`:80-91`): never called (see B5).
  - `GetResourceItemByName` (`:298-317`): legacy name-based lookup still used as a fallback by quests and narrative. Deprecate.
- **`GameInitializer`:** `disableBurst` is always false, so `ConfigureBurstForBuildDebugging` is dead (`GameInitializer.cs:9, 24-34`). `using System.Collections.Generic` is unused.
- **`PersistentSingleton`:** the `"GalaxyManager"` hack (`:22-26`).
- **`SaveManager`:**
  - autosave machinery (`:188, :199-201, :221-227, :567-577`); `CreateProfile` (`:245-266`) and `ListProfiles` (`:293-324`) have no callers, so the profile feature is unreachable;
  - `ResolveExistingGalaxyManager` (`:778-786`) has no callers;
  - `SaveReason.Autosave` and `SceneTransition` are never passed;
  - the baseline/fingerprint system (`:758-961`) is harmful (B1);
  - the audit subsystem (~560 lines) should be dev-only.
- **`GameSaveData`:** `OrbitalCameraStateData` (`:286-308`; `OrbitalCamera.TryCaptureState/ApplySavedState` are never called), both `Extensions` lists, `TotalPlayTimeTicks`, `HasSpawnedInCampaign`, `PersistentWorldMutations`, `SpawnedEntityRecords`, `ReplacementRecords`, `RuntimeSpawnCounter`, `PlayerAnchorState`/`SolarSystemCameraStateData` (never written outside Persistence), `EntityStateEntryData.HasTransform/Position/EulerRotation`, `PlayerState.Fuel`, `PlayerState.ToolbarInventory`.
- **`Player`:** `ProfileImageName`/`GetProfileTexture` (never set; folder missing), `RemoveShipFromFleet`, `EnsureFleetThumbnailsLoaded`, `OnInitialized`, `Name`.
- **`Ship`:** `RepairVoxel` (no callers).
- **`UIManager`:** commented `ToggleCharacterView`/`ToggleTradePage`; `ToggleCombatHudView` duplicates `ToggleGalaxyView` (`:157-165`); unused `using QFSW.QC`.
- **`SceneLoader`:** `BattleSceneName`, `ManagersSceneName`, `SpaceStationScene`.
- **`InputManager`:** `characterPageAction`, `changeCameraAction`, `continueAction`, `autofireToggleAction`, `showShipEditorAction` and `saveShipAction` are cached and toggled but have no handlers here (other files may poll them). Placeholder "New action" entries sit in the Combat and Editor maps.
- **48 assets** still carry the orphaned YAML `Id:` field. Re-serialize them with *Force Reserialize Assets*.

### 4.3 Roguelike "run" leftovers in or near scope

- `SaveReason.Checkpoint/Defeat`, and `StartNewGame` comments "Initialize managers for a fresh run" (`SaveManager.cs:384`).
- `EncounterManager.InitializeFreshRun`, `EndRun`, `HandleRunEndContinue`, `awaitingRunRestart`, `ExtractSuccess` (B8).
- `QuestRunStateData`, `WorldState` ("run") vs `PersistentWorldState` ("formerly meta", `GameSaveData.cs:128`).
- `UIManager.ToggleRunEndView` / `RunEndView`.
- `CreativeModeLootTable` as new-game starting items.
- `GameSettings` reduced to one `StartingItems` field.
- `SceneLoader.BattleSceneName = "CombatScene"` with a missing scene still in the build list.
- `GalaxyData_01` with `lootTable` (`EncounterManager.cs:426`).
- `ExtractStablePoint` asset and `ExtractionRiftTrigger`.
- `ShipTableSO` "starter ships" / ship selection intro.
- `GameDescription.txt`.
- `CombatEncountersData.json`.

### 4.4 Duplicates and overlap
- Two save paths for ship designs: campaign `Fleet` in `state.json` vs `ShipTemplates/*.json` via `ShipEditorManager` (`:704-740`), each with its own format.
- `SolarSystemCameraStateData` and `OrbitalCameraStateData` are identical classes (`GameSaveData.cs:262-308`).
- Two `ResolveExistingGalaxyManager` implementations (SaveManager `:778`, EncounterManager `:751`).
- Three inventory "combine" APIs: `Combine`, `CombineArrays` (static) and `CombineFrom` (`Inventory.cs:76-89, :348-418`).
- Manual volume maths repeated in 5 places in `AudioManager` instead of a mixer.

---

## 5. Performance and allocation concerns

1. **`Resources.LoadAll<T>(string.Empty)` twice per save and twice per load** (`SaveManager.cs:899, 919`; load goes through `ValidateCompatibility` → `BuildCurrentBaseline`). This loads every asset in every Resources folder, including TextMesh Pro's 2.3 MB, just to build a hash. Saves happen after **every encounter** (`EncounterManager.cs:311`), so this is a guaranteed hitch.
2. **Whole-campaign `JsonUtility.ToJson` on the main thread.** Ships are stored as `List<VoxelData>` with a per-voxel string key, a `string[]` and float rotations. That is about 120 bytes of JSON per voxel. A 1M-voxel ship is roughly 120 MB of JSON per save, built as one string, plus a SHA256 over it, plus a DeepClone round-trip on load (`SaveManager.cs:976-979, :1025-1026`). Incompatible with "MASSIVE ships".
3. **The audit snapshot runs on every save and load:** LINQ, a pretty JSON build, a full-text `Debug.Log` (`:1061-1067`) and an ever-growing `audit_history.log`. It is always on because SaveManager cannot be configured (B15).
4. **Lots of small synchronous file I/O per save:** `GetProfileName` reads `profile_descriptor.json` on every call and is called 3-4 times per save. Descriptor read/write, profile read/write, temp/copy/delete/move. Thumbnails may also be PNG-encoded and written during a save (`HydrateShipThumbnails` → `EnsureShipThumbnailPersisted`, `Player.cs:347-389`).
5. **`ObservableArray` fires the full array on every index set** (`ObservableArray.cs:61-74, 97-107`). `Inventory.Sort()` (~60 sets), `StackAll()` and bulk adds each trigger dozens of full UI refreshes. `Count` uses LINQ on every call. `Inventory.AddCurrency/SetCurrency` allocate `Items.ToArray()` for an unrelated event (`Inventory.cs:48, 56`).
6. **The ship blob cache leaks** for the whole session (B5). `ShipData.PrebakeStats` allocates a persistent blob it never uses (B28). Every `ShipData` is finalizable.
7. **Per-voxel LINQ:** `Ship.IntactVoxelCount`, `DestroyedVoxelCount` and `IsDamaged` (`Ship.cs:70-73`) are O(N) LINQ over voxels per UI refresh. Fine for 100 voxels, not for 10^5 to 10^6.
8. **`PlayerPrefs.Save()` on every slider tick** (`SettingsManager.cs:90`, called from every setter).
9. **One entity per sound effect:** `PlaySoundEvent` entities are created and destroyed per sound (structural changes; `AudioSystem.cs:17-34`). The SFX pool steals the oldest source even if it is still playing (`AudioManager.cs:213-218`).
10. **Debug logging on hot paths:** `SceneLoader` every frame during loads, `Player.HandleInventoryItemChanged` on every item change, `Inventory.CombineFrom`, `ShipData.PrebakeStats`, the `GameInitializer` boot logs.
11. **Leaked graphics objects:** a `Sprite.Create` per fleet notification (`Player.cs:120`), and thumbnail `Texture2D`s that are never destroyed when ships leave the fleet.
12. **Linear scans:** `GameDatabase.GetResourceItemByName` is a linear scan, still used by quests and narrative fallbacks. `Player.FuelCount` scans 60 slots on every HUD item event.

---

## 6. Recommended target architecture

The owner asked for boldness and said existing code is not a constraint. The goals: one composition root; authored definitions that are immutable at runtime; one session object that is the single source of truth; a versioned, chunked save format that scales to huge voxel structures and galaxy deltas; and explicit input and audio contexts.

### 6.1 Composition root and services

**One Boot scene (build index 0) holding one `GameRoot` prefab.** Content scenes (Menu, Galaxy, Builder) never contain managers. For editor convenience, a static bootstrapper guarantees the root exists no matter which scene you press Play in:

```csharp
public static class Bootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Services.Reset(); }          // makes Enter-Play-Mode-Options safe

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void EnsureRoot()
    {
        if (Object.FindAnyObjectByType<GameRoot>() != null) return;
        var prefab = Resources.Load<GameRoot>("GameRoot");     // the ONE Resources entry
        Object.DontDestroyOnLoad(Object.Instantiate(prefab));
    }
}

public sealed class GameRoot : MonoBehaviour
{
    [SerializeField] ContentDatabase content;
    [SerializeField] AudioMixer mixer;
    [SerializeField] InputActionAsset input;

    void Awake()   // explicit, ordered construction; no Awake-order roulette
    {
        var settings = Services.Register(new SettingsService(SavePaths.Settings));
        var db       = Services.Register(new ContentRegistry(content));        // ids, blobs
        Services.Register(new InputService(input, settings));
        Services.Register(new AudioService(mixer, settings));
        Services.Register(new PauseService());
        Services.Register(new SaveService(SavePaths.Root, db));
        Services.Register(new SceneFlow(this));
        Services.Register(new SessionService(db));                              // owns GameSession
        settings.ApplyAll();
    }
}

public static class Services   // tiny typed locator; replace with VContainer later if desired
{
    static readonly Dictionary<Type, object> map = new();
    public static T Register<T>(T s) where T : class { map[typeof(T)] = s; return s; }
    public static T Get<T>() where T : class => (T)map[typeof(T)];
    public static void Reset() { foreach (var d in map.Values.OfType<IDisposable>()) d.Dispose(); map.Clear(); }
}
```

- Delete `PersistentSingleton` (no auto-creation) and the `GameInitializer`-in-every-scene pattern.
- MonoBehaviours that need scene presence (UI documents, the galaxy streamer) receive services in `Awake` via `Services.Get<T>()`.
- ECS systems access content and session data through singletons (below), not through `GameDatabase` statics.

### 6.2 Authored content: immutable definitions with stable IDs, baked to blobs

```csharp
public abstract class ContentDef : ScriptableObject
{
    [SerializeField, Delayed] string id;               // "block.hull.1x1" - validated unique, immutable once released
    public string Id => id;
    [NonSerialized] internal ushort runtimeIndex;      // assigned by ContentRegistry; 0 is reserved for "none/air"
    public ushort RuntimeIndex => runtimeIndex;
}

public sealed class ItemDef : ContentDef
{
    public LocalizedString displayName; public LocalizedString description;
    public Sprite icon; public int maxStack = 999; public int basePrice; public ItemRarity rarity; public ItemTags tags;
    public BlockDef placesBlock;                        // optional: item -> block it builds
}

public sealed class BlockDef : ContentDef
{
    public Mesh mesh; public Vector3Int[] cells = { Vector3Int.zero }; public bool rotationLocked;
    public float health = 100, mass = 1; public float energyRegen, energyStorage, passiveDrain;
    [SerializeReference] public List<BlockFeature> features = new();   // Weapon, Shield, Portal, Mirror, Thruster, Core, Socket...
}

[CreateAssetMenu] public sealed class ContentDatabase : ScriptableObject
{
    public List<ContentDef> all;      // rebuilt by an editor menu plus AssetPostprocessor; validated (unique ids, no empties)
}
```

- **No runtime writes to SOs.** Fields are serialized privately and exposed through getters. A debug-build guard hashes `ContentDatabase` at boot and on exit, and logs if anything mutated a definition.
- **Composition instead of the `BlockItem` inheritance tree.** A weapon is a `BlockDef` with a `WeaponFeature`. Mirror, portal and shield become features. This kills the `OnValidate` category juggling (`BlockItem.cs:90-100`, `WeaponBlockItem.cs:27-34`, `MirrorBlockItem.cs:17-22`).
- **Runtime index = 1..N, with 0 reserved.** That removes the B10 ambiguity. Indices are never persisted; saves store string IDs.
- **Bake to blobs once at boot**, or in a `ContentAuthoring` subscene baker using stable IDs, never `string.GetHashCode`:

```csharp
public struct BlockBlob { public int MeshIndex; public float Health, Mass; public BlobArray<int3> Cells; public BlockFeatureMask Features; public int WeaponIndex, ShieldIndex; }
public struct ContentBlob { public BlobArray<BlockBlob> Blocks; public BlobArray<WeaponBlob> Weapons; public BlobArray<ShieldBlob> Shields; }
public struct ContentSingleton : IComponentData { public BlobAssetReference<ContentBlob> Value; }
```

- **Loading:** `GameRoot` references `ContentDatabase` directly. Drop the `Resources` string paths. Move to Addressables labels only if DLC or streaming appears.

### 6.3 Runtime state: one `GameSession`, stable identities

```csharp
public sealed class GameSession : IDisposable
{
    public readonly ProfileState Profile = new();        // commander name, portrait id, playtime
    public readonly Wallet Wallet = new();
    public readonly InventoryModel Cargo;                // fixed slots, value-type stacks
    public readonly InventoryModel Hotbar;               // explicit, saved separately
    public readonly FleetModel Fleet = new();            // ShipRecord list (id, name, VoxelStructure, flagship)
    public readonly GalaxyState Galaxy = new();          // seed + per-system deltas
    public readonly QuestLog Quests = new();
    public readonly WorldFlags Flags = new();            // replaces WorldState / PersistentWorldState split
    public PlayerLocation Location;                      // system id, anchor id, local pose (3D + rotation)
}
```

- **Identity is stable for the session.** Loading calls `session.Restore(reader)`, which mutates these objects **in place** and raises `SessionReset` once. No subscriber is ever orphaned (fixes B2).
- `SessionService.NewGame(seed, name)` and `SessionService.Load(slot)` are the only entry points. Returning to the menu disposes the session.
- **ECS stays authoritative for live simulation** (damage, positions). Sync points:
  - a `ShipStateWriteback` participant reads surviving voxels into `VoxelStructure` on save **and** on despawn (fixes B3);
  - the galaxy streamer writes `Location` as a participant on save, not every frame into a save DTO (fixes B6).

### 6.4 Save system: versioned, chunked, participant-based

```csharp
public interface ISaveParticipant
{
    string Key { get; }          // "player", "fleet", "galaxy", "quests", "flags"
    int Version { get; }         // per-chunk schema version
    void Capture(SaveWriter w);  // main thread; copy into a snapshot (cheap)
    void Restore(SaveReader r, int version);
}

public sealed class SaveService
{
    readonly List<ISaveParticipant> participants = new();          // explicit registration order
    readonly Dictionary<(string key, int from), Action<JObject>> migrations = new();
    public Task SaveAsync(SlotId slot, SaveReason reason);          // capture on main thread -> serialize+write on worker
    public LoadResult Load(SlotId slot);                            // never "reject"; migrate + report warnings
}
```

**Container** (`world.sav`):

```
Header : magic "SSGS", u16 formatVersion, u32 contentVersion, i64 savedUtc, f64 playtime
Chunks : repeat { char[4] fourCC, u16 chunkVersion, u32 length, u32 crc32, byte[length] payload }
         "META" small JSON (descriptor - also mirrored to meta.json for the slot list)
         "IDPL" string-id palette for this save (block/item ids -> local u16)
         "PLYR" JSON (wallet, cargo+hotbar slots WITH indices + capacity, location, profile)
         "FLET" JSON list of ship records (id, name, flagship, thumbnail RELATIVE path, structure ref)
         "VOXS" one per voxel structure (ship/station/edited asteroid) - binary, see below
         "GLXY" galaxy seed + per-system deltas (destroyed/looted/visited/spawned, edited voxel chunks)
         "QUST", "FLAG", "FACT" JSON
Unknown chunks are preserved on rewrite (forward compatible). CRC mismatch -> fall back to .bak.
```

- **Format choice:** use Newtonsoft (already in the lock file, `com.unity.nuget.newtonsoft-json` 3.2.1) for the JSON chunks: dictionaries, polymorphism and `JObject` migrations. Use binary only for voxels.
- **Migrations:** each chunk carries `chunkVersion`. Migrations are `(key, fromVersion) → JObject transform`, applied in sequence. Content additions **never** invalidate a save. Unknown item or block IDs map to a "Missing" placeholder and log a warning. The B1 gate disappears.
- **Writing:**
  1. capture participants on the main thread into plain DTOs (voxel structures already live in `NativeArray`s and are copied with `CopyTo`);
  2. serialize and Deflate on a worker task;
  3. write `world.sav.tmp`, then `File.Replace(tmp, sav, bak)` (fixes B21);
  4. update `meta.json` last.
- **Triggers:**
  - autosave on a timer and on scene transitions;
  - on quit via `Application.wantsToQuit` (return false, finish the save, then quit) (fixes B7);
  - "quick save" from the pause menu.
- **Layout:** `saves/<profile>/<slot>/{meta.json, world.sav, world.bak, thumbs/*.png}`. Thumbnails live inside the slot with **relative** paths (fixes B19).

**Voxel structure encoding** (ships, stations, edited asteroids):

```csharp
public struct VoxelCell { public ushort Palette; }            // 0 = air; palette entry = (blockIndex, rot, color)
public struct PaletteEntry { public ushort Block; public byte Rot; public byte Color; }   // Rot = one of 24 orientations
public sealed class VoxelStructure : IDisposable
{
    public NativeList<PaletteEntry> Palette;                    // per structure, usually < 256 entries
    public NativeParallelHashMap<int3, int> ChunkIndex;         // chunk coord -> chunk slot (16^3)
    public NativeList<VoxelChunk16> Chunks;                     // 4096 x u16 + destroyed bitset (512 bytes)
    public NativeParallelHashMap<int3, ModuleSlots> Modules;    // sparse: only cells with sockets/modules
    public int Revision;                                        // bump on any edit -> blob cache key
}
```

Per chunk on disk:
- `int3 coord`;
- RLE over palette indices in Morton or linear order: `(u16 value, varint runLength)`, with all-air chunks omitted;
- the destroyed bitset, RLE-coded (usually empty, flagged by a single byte);
- then the whole VOXS payload is Deflated.

Multi-cell blocks store the root cell only, and occupancy is derived from `BlockDef.cells`; filler cells use palette value `0xFFFF` = "covered".

Estimate [I]: a 1M-cell hull-heavy ship is 2 MB of raw u16 per 1M cells. RLE gives about 50-200 KB and Deflate about 20-80 KB, versus roughly 120 MB of JSON today.

**Galaxy deltas:** the world is regenerated from `GalaxyState.Seed` (generated per new game; fixes B11). Only deltas are saved, keyed by stable entity IDs. For destructible procedural voxel bodies, save only the *edited* chunks, as XOR or replace chunks relative to the regenerated baseline.

### 6.5 Inventory as a data model with UI binding

```csharp
public struct ItemStack { public ushort Item; public int Count; public bool IsEmpty => Item == 0 || Count <= 0; }

public sealed class InventoryModel
{
    ItemStack[] slots; public int Capacity => slots.Length;
    public event Action<int> SlotChanged; public event Action Reset;
    int batchDepth; bool dirty;
    public IDisposable Batch();                         // coalesces events: Sort/StackAll fire one Reset
    public int TryAdd(ushort item, int count);          // returns leftover; honours ItemDef.maxStack
    public bool TryMove(int from, InventoryModel dst, int to, int count = -1);
    public int Remove(ushort item, int count);
    public void Restore(IReadOnlyList<SavedSlot> saved, int capacity);   // in place -> Reset
}
public struct SavedSlot { public int Index; public string ItemId; public int Count; }
```

- The UI binds to `session.Cargo` and `session.Hotbar` (stable references) through presenters or UI Toolkit runtime binding (`[CreateProperty]`). Views never cache an inventory that can be replaced.
- Currency moves to `Wallet`, so it is not part of the inventory.
- "ShipItem → fleet ship" becomes an explicit `FleetService.AcquireShip(def)` called by the purchase or loot code, not a side effect of `OnItemChanged`.

### 6.6 Input: action maps per context
- Maps: `Global` (Pause, Screenshot), `Flight` (Move, Look, Boost, Fire, Interact, Target), `Build` (Place, Remove, Rotate, Symmetry, Paint, Orbit/Pan/Zoom, Save), `Dialog` (Choice1-9, Continue), `UI` (Navigate, Submit, Cancel, Point/Click/Scroll).
- `InputService` keeps a context stack (`using (input.Push(InputContext.Dialog)) { … }`) and enables exactly the maps for the top context plus `Global`. That resolves Tab, Space, 1-9 and WASD collisions by construction (B17).
- Port the 36 legacy `Input.*` calls, then set `activeInputHandler` to Input System only.
- Persist rebinds with `asset.SaveBindingOverridesAsJson()` in `settings.json`. Disable the action before `PerformInteractiveRebinding` (B18).
- Generate the C# wrapper (`generateWrapperCode: 1`) so actions are typed, not `FindAction("Fire")` strings.

### 6.7 Audio: mixer groups
- One `AudioMixer` with groups Master > {Music, SFX > {Weapons, Impacts, Ambience}, UI}. Exposed parameters are set as `Mathf.Log10(max(v, 1e-4)) * 20` dB.
- `AudioService` owns pooled sources per group, with voice limiting per `SoundID` and priority stealing.
- `AudioLibrary` stays as the ID-to-clip table and gains a group, priority, pitch range and max-voices.
- ECS sound requests go into a `DynamicBuffer<SoundRequest>` on a singleton entity, drained by one presentation system. That avoids an entity per sound.

### 6.8 Settings, pause, scene flow
- **Settings:** `settings.json` (versioned) holds audio, video (resolution and fullscreen mode applied at boot), input (sensitivity, invert, rebinds), locale and accessibility. `SettingsService` raises typed change events, and every consumer is wired (B13). PlayerPrefs are no longer used.
- **Pause:** `PauseService.Acquire(reason)` returns an `IDisposable` token. Time scale is 0 while any token is alive, and ECS sees a `GamePaused` singleton (B16).
- **Scene flow:** `await sceneFlow.Go(Scene.Galaxy)`:
  1. capture participants (if needed);
  2. load the target, single or additive;
  3. wait for activation;
  4. call `ISceneReady` hooks, which pull from the session.

  This removes "pending apply" polling (`SaveManager.Update`), the dependence on `sceneLoaded` subscription order, and the B6 and B14 classes of bugs.

### 6.9 What is worth keeping
- **The string `RegistryKey` concept.** All 69 items already have clean keys such as `block_hull` and `ship_frigate`. Keep them verbatim as `ContentDef.id`, so the migration needs no key remapping.
- **Profile and slot folders, descriptor separated from state, temp+backup writes.** These are the right shape; finish them with `File.Replace`.
- **Per-system delta records** (`SolarSystemSaveData.EntityStates`). This is the right "Minecraft-style" model. Extend it to voxel-chunk deltas.
- **Capture/apply hooks** (`BeforeSave`/`SaveDataLoaded`). Formalize them as `ISaveParticipant`.
- **Faction relation persistence** (`GameDatabase.cs:589-670`). Reasonable logic; move it into the session.
- **Save-audit hashing** as a *dev-only* round-trip test. Turn it into an automated play-mode test: save, load, re-save, and compare bytes.
- **`AudioLibrary` SO**, the weighted `LootSystem.GenerateLootPicker` (add a seeded `Unity.Mathematics.Random`), and the `ShipBlobBuilder` idea (key it on `Revision`).

### 6.10 Migration path (incremental; each step shippable)

| Phase | Work | Fixes |
|---|---|---|
| 0 | Quick wins (section 7) | B1, B2, B4 (partially), B5, B7, B12 |
| 1 | `ContentDatabase` and `ContentDef` with ids = existing RegistryKeys; IDs 1..N; validators; remove runtime SO writes; blobs from the registry | B9, B10, B20, B31 |
| 2 | `GameSession` + `InventoryModel`/`Wallet`/`FleetModel` with in-place restore; route all UI through the session | B2, B4, B16 (with `PauseService`) |
| 3 | `SaveService` + participants + container + migrations. Write a one-shot `LegacyV3Importer` that reads old `state.json` with the existing `GameSaveData` DTOs (kept in a `Legacy` namespace) and emits the new format. Alternatively, declare pre-alpha saves disposable, since B1 already rejects most of them. | B1, B3, B6, B7, B11, B19, B21, B22 |
| 4 | `VoxelStructure` (NativeContainers, chunked, palette) replaces `List<VoxelData>`; binary VOXS chunks; blob cache keyed on `Revision` | B5, perf 2 and 7 |
| 5 | Boot scene / `GameRoot` / `Services`; delete `PersistentSingleton`, `GameInitializer` and statics; InputService contexts; AudioService and mixer; SettingsService; enable Enter Play Mode Options | B12-B18, B24-B26 |
| 6 | Delete the dead code in section 4; remove the run-model flows (B8) or reframe them as open-world death penalties | B8 |

---

## 7. Quick wins (each well under a day)

1. **Disable the compatibility rejection** (B1). In `LoadSlot`, log fingerprint mismatches as warnings and continue (`SaveManager.cs:521-526`). Accept `Version <= 3` with a TODO for migrations. Show an error toast in `StartView.LoadSlot` when the load fails.
2. **Make `ImportState` mutate in place** (B2). Replace `this.Inventory = new Inventory(...)` (`Player.cs:196`) with `Inventory.Clear()`, reset `Capacity`/`Items.Resize(60)`, then fill. That keeps all subscriptions alive with a one-line change.
3. **Save slot indices** (B4). Add `public int Slot;` to `InventoryItemData`, write `i` in `ExportState`, and restore into `Items[Slot]`. Expand capacity first if `Slot >= Capacity`.
4. **Call `CombatManager.CaptureShipDamage`** for the player ship from a `BeforeSave` handler and in `DespawnShip` (B3).
5. **Fix the blob cache key** (B5): include rotation and colour in `GetContentHash`, resolve `runtimeBlockId` via `blockKey` before hashing, and call `ClearShipBlobCache` on scene change.
6. **Autosave and quit save** (B7): call `SaveManager.Instance.QueueAutosave(SaveReason.Autosave)` from `OpenWorldGalaxyManager` every N seconds and before scene loads; add an `Application.wantsToQuit` handler.
7. **Stop the per-frame position write during loads** (B6): gate `SavePlayerState` on a `SceneFlow.IsTransitioning` flag, or write position only in the `BeforeSave` handler.
8. **Remove the `Resources.LoadAll(string.Empty)` calls and the always-on audit** (perf 1 and 3): set `emitSaveLoadAuditLogs` default to `false` or `#if UNITY_EDITOR`, and drop the content fingerprint entirely.
9. **Fix audio** (B12): compute music volume from the stored target volume, not the current volume; apply it to both sources; move the subscription to `Start()` (after every Awake); or simply create the mixer now.
10. **Assign a seed** on new game: `campaignState.Seed = Environment.TickCount` in `StartNewGame` (B11).
11. **Fix the `PrebakeStats` struct copy** (B9): iterate with `for (int i…) { var v = voxels[i]; v.ResolveRuntimeId(); voxels[i] = v; … }`. Make the runtime IDs 1-based or change FleetView to `<= 0` (B10).
12. **Point `GameSettings.StartingItems` at a real starter table** instead of `CreativeModeLootTable` (B8). Make the defeat flow stop trimming the fleet (B8).
13. **Remove `CombatScene` from Build Settings**; remove the per-frame logs in `SceneLoader` (B24).
14. **Persist rebinds and locale** in `SettingsManager`; add `BrightnessController` to the global Volume or delete the slider; wire sensitivity and invert-Y into `OrbitalCamera`/`ThirdPersonCamera` (B13).
15. **Delete the dead files in 4.1** and run *Assets > Force Reserialize Assets* to strip the 48 orphaned `Id:` fields.
16. **Ref-count pause:** a quick `HashSet<object>` of pause owners inside `PauseManager` (B16).
17. **Use `File.Replace`** in `WriteStateAtomically` (B21).
