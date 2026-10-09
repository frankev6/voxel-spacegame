# Audit 06: UI Layer, Ship Editor and Ship Selection

**Scope:** `Assets/Game/UI/**` (C#, UXML, USS, UIShaders), `Assets/UI Toolkit/PanelSettings.asset`, `Assets/Resources/UI/*`, `Assets/Game/Core/ShipEditor/**`, `Assets/Game/Core/ShipSelection/**`, `Assets/Game/Core/Managers/UIManager.cs` (UI parts), `Assets/Game/Scenes/ShipEditorScene.unity`, `Assets/Game/Scenes/StartScene.unity`, `Assets/Game/Prefabs/Ship Edit/*`, `Assets/Game/Prefabs/EditorPlayer.prefab`, plus the `GAME MANAGERS PREFAB` that hosts every view.
**Method:** I read every in-scope `.cs`, `.uxml` and `.uss` file in full. I resolved script, UXML and USS GUIDs from `.meta` files and looked them up across all `.unity`, `.prefab`, `.asset`, `.uxml`, `.uss` and `.mat` files to tell live code from dead code. I read the serialized fields of the scenes and prefabs, and cross-checked every string-queried element name against the UXML that view loads.
**Legend:**
- **Confidence:** **H** means I verified it directly in code or serialized assets. **M** means the code path is verified but the runtime outcome depends on Unity ordering, data or platform. **L** means it's an inference.
- **Severity:** **Crit** means data loss, an economy exploit, or something that blocks the "massive ships" vision. **High** means a frequent user-facing break. **Med** and **Low** follow from there.

All paths are relative to the repo root.

---

## 1. Current architecture map

### 1.1 Runtime topology (verified)

```
StartScene.unity                          ShipEditorScene.unity
 ├─ GameInitializer (prefab)               ├─ GameInitializer (prefab, start_dev_mode = 1 override, :135)
 ├─ AudioListener                          ├─ ShipEditorManager  (TestShip=ship_fighter, thumbnailCamera, prefabs)
 └─ StartMenu  [UIDocument StartView.uxml  ├─ ThumbnailCamera   (GameObject INACTIVE, ortho, mask=EditVoxel)
               + StartView]  (NO Camera)   ├─ Plane (Ignore Raycast, no collider, y=0, scale 121)
                                           ├─ Directional Light, Global Volume
GameInitializer.Awake ──Instantiate──►     └─ EditorPlayer (prefab): Camera + FreeFlyCamera + ShipEditorController
"GAME MANAGERS PREFAB" (DontDestroyOnLoad       + UIDocument(centerPointer.uxml crosshair, sort 1)
 via InputManager.Awake)                         (stale overrides: clearanceMaterial, TestBlocks[5])
 ├─ UIManager  (static facade, 17 view refs)
 ├─ InputManager, PauseManager, AudioManager, SettingsManager, DialogManager, EncounterManager, QuestManager...
 ├─ EventSystem + InputSystemUIInputModule  (uGUI; only needed by per-block world-space canvases)
 └─ 17 × (UIDocument + ViewUI subclass), ALL on Assets/UI Toolkit/PanelSettings.asset
```

The UIDocument sorting orders come from `Assets/Game/Prefabs/Managers/GAME MANAGERS PREFAB.prefab`. The line numbers are the `m_SortingOrder` lines.

| GameObject | View class | UXML | Sort | Blocks raycast | Init path | Opened by | Status |
|---|---|---|---|---|---|---|---|
| UIInteract | `ViewUI` | InteractUI.uxml | -1 (:593) | no | ViewUI.Awake | `UIManager.DisplayInteract` (OpenWorldPlayerController) | live |
| UIShipEditorHUD | `ShipStatsView` | ShipEditorHudUI.uxml | 1 (:51) | no | `UIManager.InitializePages` | ShipEditorManager.OnSceneLoaded | live (editor) |
| UIToolbar | `ToolbarView` | ToolbarViewUI.uxml (content discarded) | 1 (:944) | no | InitializePages | ShipEditorManager.OnSceneLoaded | live (editor) |
| UIGalaxy | `GalaxyView` (partial ×3) | GalaxyViewUI.uxml | 10 (:1720) | no | OnEnable | OpenWorldGalaxyManager | live (HUD) |
| UIPause | `PauseView` | PauseUIPage.uxml | 10 (:1221) | yes | OnEnable | InputManager.HandleBack | live |
| UIFleetView | `FleetView` | FleetView.uxml | 11 (:1523) | yes | InitializePages + lazy | `F` key | live |
| UIIntel | `IntelView` | Resources/UI/IntelViewUI.uxml | 19 (:125) | yes | Awake + InitializeView on every toggle | `Tab` (world) | live |
| UIRunEnd | `RunEndView` | RunEndUI.uxml | 20 (:1296) | yes | InitializePages | `EncounterManager.EndRun`, which has **no callers** | **dead** |
| UISettings | `SettingsView` | SettingsView.uxml | 20 (:1370) | yes | Start | PauseView | live |
| UIEntityTooltip | `EntityTooltipView` | EntityTooltipView.uxml | 20 (:1444) | no | Awake (`new`) | per-frame raycast | live |
| UIShipEditorLoader | `ShipEditorLoadUI` | ShipEditorLoadUI.uxml | 20 (:1794) | yes | OnEnable | `F12` in editor | live (hidden) |
| UIDialog | `NarrativeView` | NarrativeViewUI.uxml | 20 (:1942) | yes | OnEnable | Dialog/Encounter | live |
| UINotifications | `NotificationView` | NotificationUI.uxml | 21 (:199) | no | InitializePages | `UIManager.ShowNotification` | live |
| UIMerchant | `MerchantView` | MerchantUI.uxml | 25 (:667) | yes | InitializePages | DialogManager / NarrativeActionRuntime | live |
| UIStorage | `StorageViewPage` | StoragePageUI.uxml | 100 (:1147) | yes | **never** (commented out, `UIManager.cs:109`) | `ToggleStoragePage` has no callers | **dead** |
| UIInventory | `InventoryView` | InventoryUI.uxml (content discarded) | 1000 (:1868) | yes | InitializePages | `I` key | live |
| UILootPicker | `LootPickerView` | LootPickerUI.uxml | 10000 (:741) | yes | InitializePages | Only `ToggleLootPicker(false)` is ever called | **dead** |
| (none) | `TradeView` | TradeUI.uxml | n/a | n/a | n/a | No component instance anywhere | **dead** |

### 1.2 View lifecycle and navigation (verified)

- **Base class.** `ViewUI` (`Assets/Game/UI/ViewUI.cs`) caches `root` in `Awake` (:18-22). It shows and hides views by writing inline `style.display` (:33). `IsViewOpen()` only reads that inline style (:65-68). On every show it re-registers hover and click sounds on all buttons via `Query<Button>` (:44-55). There is no virtual `Open`, `Close`, `OnBack` or `Initialize`.
- **Four initialization patterns coexist:**
  - `Awake`/`OnEnable` in StartView, PauseView, GalaxyView, ShipEditorLoadUI and NarrativeView.
  - `Start` in SettingsView.
  - `InitializeView()` called centrally from `UIManager.InitializePages` (`UIManager.cs:96-111`).
  - Lazy re-initialization inside `RefreshView` or `DisplayView` (`FleetView.cs:92`, `RunEndView.cs:52`, `ShipStatsView.cs:34`, `UIManager.ToggleIntelView` → `InitializeView` on every toggle, `UIManager.cs:201`).
- **Navigation.** `UIManager` is a static facade with one bespoke `ToggleX(bool, args)` per view. `_openViewsStack` (`UIManager.cs:32-47`) is a list used only for `CurrentPage` (Back-key routing) and `HasOpenViews` (cursor and camera gating). Views don't push or pop themselves consistently (see bugs B1-B3).
- **Pause.** Pause is a single global bool. `PauseManager.PauseGame(bool)` (`PauseManager.cs:10-15`) is called by almost every toggle and isn't reference-counted.
- **Back and Escape.** `InputManager.HandleBack` (`InputManager.cs:96-112`) either opens Pause, or calls `TogglePauseView(false)` followed by `UIManager.CloseAllPages()`. `CloseAllPages` (`UIManager.cs:85-94`) closes only 5 of the 17 views, then clears the stack.
- **Hotkeys.** `Assets/InputSystem_Actions.inputactions` defines global, always-enabled project-wide actions: Inventory `I`, FleetPage `F`, Back `Esc`, Continue `Tab`, IntelPage `Tab`, ChangeCamera `Tab`, SaveShipData `F5`, ShowShipEditor `F12`, ToggleSymmetry `X`. There are no input contexts and no text-entry suppression.
- **Raycast gating.** `UIManager.IsRaycastBlocked()` (`UIManager.cs:337-355`) loops over all views and checks `IsViewOpen && BlocksRaycasts`. The ship editor and the entity tooltip call it every frame.

### 1.3 Data flow and binding (verified)

- **No Unity 6 runtime data binding is used.** The single `dataSource` assignment (`MerchantView.cs:114`) is never consumed. Every view writes `Label.text` and classes imperatively.
- **Event-driven paths:**
  - `ObservableArray.AnyValueChanged` → `Inventory.OnModelChanged(Item[])`. On every change, InventoryView, ToolbarView and ShipEditorController do a full refresh (`InventoryView.cs:111`, `ToolbarView.cs:130`, `ShipEditorController.cs:106,123-133`).
  - `Inventory.OnItemChanged` and `OnCurrencyChanged` feed GalaxyView labels and `InventoryNotificationRelay` toasts.
  - Quest events feed GalaxyView and IntelView.
- **Push from ECS.** `CombatHudSystem` (`Assets/Game/Core/Combat/CombatHudSystem.cs:18-98`) is a managed `SystemBase` that reaches into `UIManager.Instance.galaxyView` every frame. It allocates a `bool[5]` each frame and formats strings each frame (see Perf P6).
- **Polling.** `GalaxyView.Update → UpdateSolarSystemMap` runs every frame (galaxy auditor). `EntityTooltipView.Update` does a `Physics.Raycast` every frame. FleetView rebuilds its whole list on each selection.
- **Styling.**
  - `styles.uss` is the de-facto theme and is also attached as `styleSheet` on every view.
  - Many inline styles appear in UXML and C# (`StartView.cs:84-88, 208-251`, `AbilitySlot.cs:25-65`, `ItemTooltip.cs:20-36`).
  - Fonts come from third-party plugin folders (`Assets/Plugins/QFSW/Quantum Console/.../OfficeCodePro-Medium.ttf` in `styles.uss` `.tooltip`, `LootPickerUI.uxml:6`, `PauseUIPage.uxml:15-18`). Textures come from `Assets/Plugins/Pixel Crushers/...` (`styles.uss` `.button_solid`, `.worldUI_panel`) and `RPGUI` (`TradeUI.uxml:4`, `WorldUI.uxml`).
- **Third-party UI packs.** These are only referenced as art: "HOLO UI" sprites (`FleetView.uxml:13` close button, `holo-scrollview` class) and "scifi_ui white" frames and bars (`PauseUIPage.uxml:7`, `InventoryUI.uxml:9`, `GalaxyViewUI.uxml:48,54`). **Microlight MicroBar isn't referenced by any game code or UXML.**
- **PanelSettings.** `Assets/UI Toolkit/PanelSettings.asset` uses ScaleWithScreenSize with reference 1200×800, Match = 0 (width) and the default runtime TSS. At 1920×1080 the logical panel is 1200×675.

### 1.4 Ship editor (verified)

**Entry points.** `ShipEditorManager.ShipToLoad` is a static set from three places: `FleetView.HandleEditShip` (`FleetView.cs:470-478`, any fleet ship), `DialogManager.ResolveOpenShipEditor` (`DialogManager.cs:626-640`, flagship) and `NarrativeActionRuntime.ResolveOpenShipEditor` (`NarrativeActionRuntime.cs:441-462`, a duplicate of the DialogManager one). Each then calls `SceneLoader.LoadSceneAsync("ShipEditorScene")`.

```
ShipEditorScene loads
 └─ ShipEditorManager.Awake: Instance=this; sceneLoaded += OnSceneLoaded       (ShipEditorManager.cs:80-84)
 └─ OnSceneLoaded (:91-132)
     ├─ CurEditShip = ShipToLoad ?? (DEV ? new Ship(TestShip) : PlayerInfo.CurrentShip)
     ├─ DEV_MODE: Inventory.Clear() + creative loot + 5000 scrap   (:110-117)
     ├─ InstantiateShip(shipData)                                   (:139-165)
     │    └─ for each VoxelData: AddShipVoxel (:238-270)
     │         ├─ Instantiate ShipBlock.prefab  (MeshFilter+Renderer+PlacedBlock + INACTIVE world-space
     │         │   uGUI Canvas/CanvasScaler/GraphicRaycaster/Billboard + HorizontalLayoutGroup panel)
     │         ├─ PlacedBlock.Init → AddComponent<BoxCollider> PER CELL (PlacedBlock.cs:26-45)
     │         ├─ MasterShipGrid.Add; RegisterBlock → _occupiedCells / _reservedClearanceZones (ref-counted)
     │         └─ RecalculateValidation() → full ValidateShip() BFS  ◄── per voxel ⇒ O(N²) load
     └─ ToggleToolbarView(true), ToggleShipStatsView(true)   ◄── AFTER validation ⇒ stale HUD (bug E3)

Per frame: ShipEditorController.Update (EditorPlayer)                 (ShipEditorController.cs:136-164)
 ├─ if UIManager.IsRaycastBlocked() → hide ghost, return
 ├─ HandleBlockSelection: legacy Input scroll wheel / Alpha1..9 → toolbarView.SetSelectedSlot + curBlockIndex
 ├─ Block mode: HandleBlockPlacement (LMB hold place / RMB hold remove, 0.17 s throttle; Physics.Raycast
 │              from camera centre, 50 m, EditVoxel layer) → HandleBlockRotation (R/T/Y Euler offsets)
 │              → UpdateGhostBlock (despawn/spawn pooled wireframe cubes per cell + clearance + indicators,
 │                DOMove tween each frame, Slerp rotation)
 ├─ Module mode: HandleSlottablePlacement (raycast "UI" layer to uGUI slot colliders)
 └─ Consumable mode: HandleConsumablePlacement (paint / add module slot, hold-to-repeat 0.08 s)

Save paths
 ├─ F5  → ShipEditorController.HandleSaveShip → ShipEditorManager.SaveShipDataToShip (validate, new ShipData,
 │        PrebakeStats → blob, thumbnail) → then ALWAYS PlayerInfo.ExportState + SaveActiveSlot  (:178-189)
 ├─ F12 Load UI "Save (Overwrite)" → SaveShipDataToShip only (no disk save)        (ShipEditorLoadUI.cs:117-121)
 ├─ F12 "Save As Template" → JSON in persistentDataPath/ShipTemplates (no validation, no prebake)
 └─ DEV: writes ScriptableObject + PNG into Assets/Resources/Items/Ships (Editor-only)
Leave: Tab (Continue) → hide load UI/toolbar, LoadScene(GalaxyMapScene). No save, no prompt.  (:75-95)
```

**ECS handoff.** The editor writes `CurEditShip.ShipVoxelData = new ShipData{voxels=…}`. `ShipData.PrebakeStats()` recomputes bounds, abilities, officers and global stats, then builds a `BlobAssetReference<ShipDataBlob>` via `ShipBlobBuilder.CreateBlob` (`ShipData.cs:154-252`, `ShipBlobBuilder.cs:18-101`). The runtime spawns from that blob, so the editor and runtime share only `List<VoxelData>`. The editor's working representation is GameObjects plus `Dictionary<Vector3Int,PlacedBlock>`, which is a different model from the runtime's.

**Ship data unit.** `VoxelData` (`Assets/Game/Core/VoxelShip/ShipVoxel.cs:15-78`) is one record per block. It holds root cell ints, Euler rotation as three floats, a `byte` colour index, a `string[] moduleSlots` and an `IsDestroyed` flag. Footprints come from `BlockItem.GetOccupiedCells()` (`BlockItem.cs:59-89`), which uses `VoxelShape` or a `BlockSize` box.

### 1.5 Responsibility split and overlap between `ShipEditorController` and `ShipEditorManager`

| Concern | Controller | Manager | Problem |
|---|---|---|---|
| Cell rotation math ("rotate cell + RoundToInt") | `UpdateGhostBlock:239-243,263-266`, `IsBlockPosValid:987-1013`, `OnDrawGizmos:1033-1058` | `GetBlockOccupiedCells:972-986`, `GetBlockClearanceZones:988-1005` | 5 copies; ghost uses the interpolated rotation (C3) |
| Placement rules | `IsBlockPosValid` (occupancy, clearance) | `ValidateShip` (core, limit, connectivity, radius) | Placement ignores limit and connectivity. Validation runs only after the fact. |
| Symmetry | All mirror logic (`:479-832`) | State and event only (`:66-75`) | No UI listens to `OnSymmetryChanged` |
| Inventory | Consumes items (`ConsumeCurrentItem:607-611`) | Refunds (`RemoveShipVoxel:388-390`, `UninstallModule:339`) | Economy is split and not transactional (A1-A5) |
| Edit mode | Decides the mode from the held item (`UpdateBlockVisual:716-746`) | Owns `curEditType` and re-skins every block (`ToggleEditMode:168-207`) | O(N) GetComponent loop on every inventory change |
| Selected item | `curBlockIndex` **and** `toolbarView.GetSelectedSlot()` | n/a | Two sources of truth (C1) |
| Save | Persists the save slot | Writes the Ship, thumbnail and SO | The two save buttons behave differently (A6) |
| UI coupling | Reads the toolbar view for game state | Pushes to `UIManager.Instance.shipStatsView` | Logic depends on view objects |
| Unused duplicates | `voxelPrefab` (:11), `neighborChecks` (:1018-1023), `ghostCollision` (:24) | `EditVoxel` struct (:58-62), unused `queue` (:887) | Dead code |

Duplicates outside the editor:
- Repair cost is computed three times: `FleetView.CalculateRepairCost:356-381`, `DialogManager.GetFleetRepairCost:646-680` (whose comment says it copies FleetView) and `NarrativeActionRuntime` (~:420-437).
- Thumbnail loading exists twice: `FleetView.LoadShipThumbnail:498-532` and `Player.EnsureShipThumbnailLoaded` (`Player.cs:250-312`).
- Two thumbnail naming schemes are in use. The editor writes `{name}_{ticks}.png` (`ShipEditorManager.cs:479`), but Player recovery looks for `_{ship.Id}.png` (`Player.cs:329`), so editor-made thumbnails can never be recovered.

### 1.6 Ship selection (verified dead)

`ShipSelectionController` and `ShipSelectionVisualizer` (`Assets/Game/Core/ShipSelection/`) aren't attached in any scene or prefab. Their GUIDs `f370f2f4…` and `8a1a2298…` appear only in their own `.meta` files, and the only code reference is between the two classes. `Ship Edit/ShipBlockVisualOnly.prefab`, which would be their voxel prefab, is unreferenced too. If revived as-is, `OnEnable` subscribes anonymous lambdas that are never removed (`ShipSelectionController.cs:32-33`) and dereferences `orbitalCamera.enabled` before its null check (:177). The "Previous" and "Next" actions are bound to `A`/`D`, `1`/`2` and the arrow keys, which collide with WASD and hotbar keys.

---

## 2. Bugs

### A. Ship editor economy and data integrity

| # | Sev | Conf | Evidence | What happens and why |
|---|---|---|---|---|
| A1 | **Crit** | H | `ShipEditorManager.RemoveShipVoxel:377-397`, `ShipEditorController.ConsumeCurrentItem:607-611`, `HandleContinueAction:75-95` | **Item duplication or loss by leaving without saving.** Inventory changes apply immediately: place consumes, remove refunds. Ship changes only apply on save, because `SaveShipDataToShip` builds a new `ShipData`. Tab leaves without saving or prompting. If you remove N blocks and then press Tab, you get N items back and the ship still has them, which can be repeated forever. Placing blocks and then pressing Tab loses the items. |
| A2 | **Crit** | H | `LoadShipFromTemplate:734-748` → `LoadShip:716-732` (`CurEditShip.ShipVoxelData = data`) | **Templates create free blocks.** Outside DEV mode, loading a JSON template replaces the ship with blocks from nowhere and consumes no inventory. Removing them then refunds real items (A1). The previous blocks vanish without a refund. LoadShip also **mutates the live Ship** before any save. |
| A3 | High | H | `HandleSaveShip:178-189`; `SaveShipDataToShip:521-525` returns `void` | **F5 on an invalid ship still writes the save file.** Validation silently aborts the ship write. Only `Debug.LogWarning` runs, with a TODO for the notification at :505. The inventory, with consumed or refunded items, is then exported and persisted anyway, which locks in A1-type desync. |
| A4 | High | H | `AddShipVoxel:238-270` never checks `IsDestroyed`. `RemoveShipVoxel:388` refunds unconditionally. | Destroyed (combat-damaged) blocks **render as intact** in the editor. Removing one **refunds a full new block**, which is a free-repair exploit. Connectivity validation also counts destroyed blocks as connectors. |
| A5 | High | H | `RemoveShipVoxel:389` (`canAddItem` unused). `UninstallModule:339` ignores the result. | Block or module is destroyed while the inventory is full, so the item is lost. |
| A6 | Med | H | `ShipEditorLoadUI.OnSaveClicked:117-121` vs `HandleSaveShip` | The UI "Save Ship (Overwrite)" button doesn't persist to disk. F5 does. |
| A7 | Med | H | `SaveShipDataToShip:597-603`; `Player.cs:142,189-193` | Editing a non-flagship from FleetView and saving sets `PlayerInfo.CurrentShip` to that ship without changing `FlagshipIndex`. On reload, `CurrentShip = Fleet[FlagshipIndex]` reverts it, so the state is inconsistent. |
| A8 | Med | H | `VoxelData.moduleSlots` (`string[]` reference) is shared between the source `ShipData` and the `PlacedBlock` struct copies (`AddShipVoxel` receives `shipData.voxels[i]`). `InstallModule:328`, `UninstallModule:342` write into it. | **Module edits mutate the original Ship in memory without saving**, while block edits don't. The result is a half-applied draft and makes A1's accounting more confusing. |
| A9 | Med | H | `SaveThumbnailToDisk:479` | Each save writes a new `{name}_{ticks}.png` and never deletes old ones, so the folder grows without bound. The old `ThumbnailTexture` and RenderTexture objects are never destroyed (`:443,459,550`). |
| A10 | Med | H | `SaveShipAsTemplate:692-714` | No validation, no `PrebakeStats` (width, height, length and stats all zero) and an unsanitized file name (`templateName + ".json"`). A `/` or other invalid character throws. |
| A11 | Med | H | `GetAvailableShips:781-787`, `ShipEditorLoadUI.OnLoadClicked:104-108` | The non-DEV list shows `Fleet: <id>` entries that can't be loaded. Clicking one only logs a warning, then closes the dialog. |
| A12 | Med | H | `HandleConsumablePlacement:431-440` with `PAINT_DELAY 0.08` | AddModuleSlot consumables repeat while the mouse is held, at about 12 per second. It consumes even when `success` is false (:433). Mirroring consumes a second item. |
| A13 | Med | H | `MerchantView.HandleBuyClick:138-156`; `ObservableArray.TryRemove` sets the whole slot to default | **Buying 1 removes the merchant's entire stack.** The price is also deducted before `TryAddItem`, and if adding fails, the scrap is lost. |
| A14 | Low | H | `OnSceneLoaded:110-117` | DEV_MODE wipes the real inventory every time the editor opens. DEV_MODE can be toggled at runtime through the QFSW console (`Global.cs:10-14`). |

### B. Navigation, pause and input

| # | Sev | Conf | Evidence | What happens and why |
|---|---|---|---|---|
| B1 | **High** | H | `FleetView.cs:75` (`closeBtn.clicked += () => DisplayView(false)`) | Closing Fleet with its X button skips `UIManager.ToggleFleetView(false)`, so the **game stays paused** (`Time.timeScale = 0`) and the view stays in `_openViewsStack`. |
| B2 | **High** | H | `UIManager.CloseAllPages:85-94` | Esc closes only Pause, Inventory, Merchant, Narrative and Intel, then **unpauses and clears the stack**. If Fleet, Settings, ShipEditorLoadUI, Storage, LootPicker or RunEnd is open, it stays visible with the simulation running under it. A second Esc then opens Pause on top. |
| B3 | High | H | `SettingsView.cs:71-78`; `ShipEditorController.cs:78` | The Settings close button and the editor's Continue action call `DisplayView(false)` directly, which leaves stale stack entries. Continue with the load UI open loads the world **while paused**, because `ToggleShipEditorLoadUI(true)` paused the game. |
| B4 | **High** | H | Pause is a bool (`PauseManager.cs:10-15`) | Pause isn't reference-counted. For example, Inventory open → F toggles Fleet open → F again closes Fleet, which calls `PauseGame(false)` while the Inventory is still open, so gameplay runs under a modal. |
| B5 | **High** | H | Global actions (`InputManager.cs:39-59`), TextFields in `ShipEditorLoadUI.uxml:9` and `StartView.uxml:17` | **Hotkeys fire while typing.** Typing a ship name containing `f`, `i` or `x` toggles Fleet, Inventory or Symmetry. **Tab leaves the editor.** Esc closes everything. On the Start screen, `i` opens the in-game inventory and Esc opens the in-game Pause menu (whose Save Game errors with no save, `PauseView.cs:80-83`), because the managers prefab is loaded in StartScene. |
| B6 | Med | H | `UIManager.ToggleMerchantPage:254-258` | `PauseGame(true)` runs before the `m == null` early return, which leaves the game paused with nothing shown. |
| B7 | Med | M | `ShipEditorManager.OnSceneLoaded:126-131` is the only place that hides the toolbar and stats; the manager unsubscribes in `OnDestroy:86-89` | In a Single scene load the editor scene (and this manager) is destroyed before `sceneLoaded` fires for the next scene, so the `else` branch is effectively dead. **The "SHIP STATS" HUD stays visible in the galaxy after you leave the editor.** Tab hides only the toolbar (`:79`). If you leave via Pause → Load, both stay visible. |
| B8 | Med | H | `ShipEditorController.OnEnable:102-108` vs `Start:62-65`, `OnDisable:110-121` | Input handlers are subscribed in Start but unsubscribed in OnDisable, so a disable and re-enable loses them. `OnDisable` dereferences `InputManager.Instance` without a null check, which can throw a NullReferenceException at quit or teardown. |
| B9 | Med | M | `FreeFlyCamera.SetCursorState:80-98` plus `HandleBlockPlacement:616` (same frame) | The click that re-captures the cursor after closing a view also places a block at the crosshair. With the cursor unlocked, a click places at screen centre rather than under the mouse. |
| B10 | Low | H | `FreeFlyCamera.cs:46` default `_initPositonButton = KeyCode.R` | This conflicts with the editor rotate key R (`ShipEditorController.cs:850`). The prefab serializes F1 (`EditorPlayer.prefab:117`, value 282), so it's latent only. |
| B11 | Med | H | `InventoryNotificationRelay.Initialize:16-25` | On re-initialization it unsubscribes from the **new** player's inventory rather than the old one, so the old subscription leaks. `HandleItemChanged:47` dereferences `item.Name` after a null-guarded block. |
| B12 | Low | H | `SaveSlotListPresenter.PopulateInternal:231-235` (`StartView.cs`) | Deleting a save slot (the "X" button) needs no confirmation, so one click causes irreversible data loss. |

### C. Ship editor placement, symmetry and rendering

| # | Sev | Conf | Evidence | What happens and why |
|---|---|---|---|---|
| C1 | **High** | H | `curBlockIndex` (`:21`, reset to 0 on each editor load) is used by `UpdateGhostBlock:213`, `CanPlaceBlockAt:901` and `HandleSlottablePlacement:365`. `PlaceVoxel:750` uses `toolbarView.GetSelectedSlot()`. ToolbarView is DontDestroyOnLoad and keeps `selectedSlot`. | If you leave the editor with slot 4 selected and re-enter, the ghost and validation use slot 0's block while placement uses slot 4's. If slot 4 holds a non-block, `item.details as BlockItem` is null and `CreateVoxelData(null…)` throws a NullReferenceException (`ShipEditorManager.cs:214`). |
| C2 | **High** | H | `TryRemoveMirroredBlock:677-693` → `GetBlockAt(mirroredPos)` returns **any block occupying that cell** | Symmetric removal deletes whatever overlaps the mirrored root cell, including a different block type or a large multi-cell block that merely covers it. |
| C3 | High | H | `GetMirroredRotation:778-784` (Euler negation); `TryPlaceMirroredBlock:811-828` mirrors only the root cell | **Symmetry is wrong for any asymmetric multi-cell block**, such as L or T shapes, or a 2×1 rooted at its corner. A rotation can't produce a reflection, and mirroring the root doesn't mirror the footprint. The mirror plane is hard-coded at world X=0, not the core. Even-width ships can't be built symmetric, because the centre column at x=0 is excluded (`:806`) and there's no half-cell plane. |
| C4 | Med | H | `UpdateGhostBlock:220` validates before `curRotation` updates (:230-231). Footprint cells use `ghostBlock.transform.rotation` (Slerped, :235-241). | **Visual:** during a rotation the footprint wireframe shows the wrong cells for several frames, and validation lags a frame. Point indicators (`SpawnGhostIndicator:307`) also use the tweened and slerped transform. |
| C5 | Med | H | `UpdateGhostBlock:233` | `DOMove` creates a new tween every frame (about 60 per second), so they overlap, the ghost jitters or lags, and it allocates. |
| C6 | Med | M | `Start:41-72` never calls `UpdateBlockVisual`. In DEV mode the inventory change fires before Start, while `ghostBlock` is null (`:127`). | **Visual:** at editor start the ghost shows the GhostPiece prefab's default cube mesh until you scroll or press a number key, while the footprint shows the real multi-cell shape. |
| C7 | Med | H (Metal) | `Wireframe.shader` uses `#pragma geometry` and is used by `WireframeMat` → scene override `visualMaterial` (`ShipEditorScene.unity:705-707`) | **Visual on macOS:** Metal doesn't support geometry shaders, so the footprint cubes render with the error shader or not at all on the owner's Mac. The shader is also built-in CG with no `UnityPerMaterial` CBUFFER, so it isn't SRP-Batcher compatible. |
| C8 | Med | H | `PlacedBlock.UpdateModuleSlots:116-117` (`GetItemByKey(...).ItemImage`); `AddShipVoxel:246` hard cast `(BlockItem)` with no null check | A renamed or removed block or module key throws a NullReferenceException or InvalidCastException, which **aborts loading the whole ship** in the editor. |
| C9 | Med | H | `ValidateShip:941-950` | The build-radius check uses the Euclidean distance from the core root to each block **root**, not to the block's cells or bounds. The comment admits it's rough. |
| C10 | Low | H | `PlacedBlock.SetInvalid:51-77` | Invalid blocks get an infinite `DOShakePosition` tween each. This moves their colliders too, so raycasts jitter. There's no colour feedback (commented out at :66). |
| C11 | Low | M | `ShipEditorScene.unity` Plane at y=0 (:372-373); ship voxels start at y=0 (`ship_fighter.asset` y range 0..6) | **Visual (likely):** the floor plane bisects the bottom layer of blocks. |
| C12 | Low | M | ThumbnailCamera GameObject `m_IsActive: 0` (`ShipEditorScene.unity:396`); `CaptureShipThumbnail:404-462` | `Camera.Render()` on an inactive GameObject may render nothing, which would give an empty thumbnail. Needs a play-mode check. Also `originalEnabled` is captured but never restored (:426,457), `farClip=100` and a camera height of `+20` clip ships taller than 20, and the culling mask is EditVoxel only (non-socket blocks go to layer 0 in Module mode). |
| C13 | Low | H | `HandleBlockRotation:850-863` | Rotation accumulates Euler quaternions on top of the surface "smart rotation", and orientation is stored as floats (`rotX/Y/Z`). There's no canonical 24-orientation set, so equality checks and mirror tables aren't possible. |
| C14 | Low | H | `Physics.Raycast(..., 50, ...)` (`:203,621,652`) | A 50 m reach means large ships can't be edited from a distance. |

### D. UI views

| # | Sev | Conf | Evidence | What happens and why |
|---|---|---|---|---|
| D1 | Med | H | `InventoryView.cs:17` → 50+9 = **59** slots; `Player.cs:61` capacity 60 | Inventory slot 59 is never displayed, so an item there is invisible. |
| D2 | **High** | H | `SettingsView.StartRebinding:292-312` | `PerformInteractiveRebinding` on an enabled project-wide action throws "Cannot rebind action … while it is enabled". The button stays on "Listening…". There's no `OnCancel`/`Dispose` on cancel, nothing is persisted, and `PopulateRebindings:269-289` lists every action including UI Point, Click and Navigate and composite index 0. |
| D3 | Med | H | `SettingsView.ShowTab:122` | At boot `Start → ShowTab("audio")` plays a click sound, and `AudioManager.Instance` isn't null-checked. Video init dereferences `SettingsManager.Instance` without the null check the Audio tab uses (`:190`). `InitializeGeneral` is `async void`. |
| D4 | Low | H | `ViewUI.DisplayView:44-55` | Sound callbacks are registered or unregistered on every show or hide via a `Query`. It works only because delegate equality (same target and method) dedupes them. Buttons created after the show, such as merchant buy buttons rebuilt per purchase (`MerchantView.cs:153`), have no sounds. |
| D5 | Low | H | `ItemTooltip.SetData(AbilitySO):69-93` | The ability tooltip shows the **asset name** only. `ItemInfoHelper.GetAbilityTooltipData` exists but is unused. `GetRarityColor:188` comments "Magenta" but returns green. |
| D6 | Low | H | `StartView.uxml:18` (`display:none`), `StartView.cs:67-98` | The portrait picker is hidden, but `Resources.LoadAll<Texture2D>("Portraits")` still runs on every "New Game", and `selectedPortraitName` is never used. |
| D7 | Low | H | `EntityTooltipView.cs:107,210-257` | It uses legacy `Input.mousePosition` and hand-rolled screen-to-panel math, where `RuntimePanelUtils.ScreenToPanel` is the correct API. It works now because the active input handler is "Both" (`ProjectSettings.asset:808`, `activeInputHandler: 2`). |
| D8 | Low | H | `ToolbarView.SetSelectedSlot:77-80` | It plays a hover sound on every selection, including the init call at boot. |

### E. Visual bugs (summary, including the visual items from A-D)

| # | Sev | Conf | Evidence | Symptom |
|---|---|---|---|---|
| E1 | Med | H | `FleetView.uxml:31-49` (`stat-weapons/energy/regen/thrusters/shields`) are never queried. The name cross-check shows them unqueried. | Fleet stats always show **0** except Blocks. `details-name` has a fixed `width: 945px` (`:20`) that can overflow a 1200-wide panel. The repair button template says "Cr" while the code says "Scrap". |
| E2 | Med | H | Notification sort 21 (`:199`) vs Merchant 25, Storage 100, Inventory 1000, LootPicker 10000 | Toasts such as "+1 item" and "-200 Scrap" from a purchase are **hidden behind the screens that cause them**. Pause and Galaxy HUD are both 10, and six documents tie at 20, so their order isn't defined. |
| E3 | Med | H | `OnSceneLoaded:120` `InstantiateShip` runs before `:123` `ToggleShipStatsView(true)`. `RecalculateValidation:828` skips closed views. | On editor entry the stats HUD shows **"Voxels: 0 / 0"** until the first edit. The error panel isn't hidden initially. |
| E4 | Med | M | B7 | The ship stats HUD and toolbar persist into the galaxy scene. |
| E5 | Med | H | `RemoveQuantity` → `OnItemChanged` (`Inventory.cs:115`) → `InventoryNotificationRelay:46-50` | Every block placed or removed spawns a "-1 Hull" or "+1 Hull" toast. Holding the mouse floods the toast column. |
| E6 | Low | H | `OnSymmetryChanged` has no subscribers (`ShipEditorManager.cs:68,73`) | There's no on-screen symmetry state and no mirror-plane visual. X silently toggles. |
| E7 | Med | M | `PauseUIPage.uxml:6-19`, ref 1200×800 with match-width | On 16:9 screens the logical height is 675. The menu needs 200 px of padding plus 380 + 20 + 234 px of content, so the content compresses or overflows. It also has hard-coded key hints that ignore rebinding, F12 is undocumented, and the font comes from the debug-console plugin. |
| E8 | Low | M | `styles.uss` global `.unity-scroll-view__content-container { justify-content:center; flex-grow:1 }` | Every ScrollView or ListView in documents using styles.uss vertically centres short lists, including Fleet, save lists, Merchant and the ShipEditorLoad ListView. |
| E9 | Low | H | RunEndView `sectorValue/killsValue/damageValue/scrapValue/blueprintCount` never set (`RunEndView.cs:31-36`) | Shows zeros. The view is dead anyway (section 3). |
| E10 | Low | H | `GalaxyView.Combat.cs` adds `combat-shield-online/low/offline` and `combat-autofire-on/off`, but none of them is defined in any USS (class cross-check) | Shield and autofire state never changes the HUD style, only the text (galaxy auditor). |
| E11 | Low | M | `StartScene.unity` has no Camera | UI Toolkit renders over an uncleared back buffer. The "No cameras rendering" warning appears, and smearing is possible where the 80% black background is translucent. |
| E12 | Low | H | `A4` | Damaged ships look pristine in the editor. |

### F. String queries checked

None of the 16 views query an element name that's missing from its UXML. The script cross-checked every `Q`/`Query("…")` against the loaded UXML. The unqueried names are the gaps worth knowing: FleetView `stat-*` and RunEnd `blueprintCount`.

---

## 3. Dead, deprecated and duplicate code (with proof)

| Item | Proof | Recommendation |
|---|---|---|
| `TradeView.cs` (480 lines), `TradeUI.uxml`, `Pages/TradePageUI.uxml` | No prefab or scene has the TradeView script GUID `8857ae61…`. `UIManager.cs:110,231-251` is commented out. `SetupSlots:125` would throw a NullReferenceException because `playerInv` is null at init. | Delete. Merchant replaces it. |
| `StorageViewPage.cs` plus `StoragePageUI.uxml` and the UIStorage GameObject | `ToggleStoragePage` has no callers (grep). `InitializeView` is commented out (`UIManager.cs:109`) and would throw a NullReferenceException (`StorageViewPage.cs:15`). `RefreshView:113-138` uses `INVENTORY_CAPACITY` for both inventories. | Delete, or rebuild as a "Station Storage" screen later. |
| `LootPickerView.cs`, `LootPickerUI.uxml`, UILootPicker GameObject (sort 10000) | Only `ToggleLootPicker(false)` is ever called (`LootPickerView.cs:104,111`). `EncounterManager.GenerateEncounterLoot` and `AddEncounterLoot` exist only for it. This is roguelike-era code. | Delete. Keep `LootPickerStyles.uss`, which Merchant uses. |
| `RunEndView.cs`, `RunEndUI.uxml`, `RunEndStyles.uss`, UIRunEnd | `EncounterManager.EndRun` has no callers (grep). The stats are never populated. This is roguelike-era code. | Delete, along with `EncounterManager.EndRun` and `HandleRunEndContinue` (coordinate with the encounter auditor). |
| `ShipSelectionController`, `ShipSelectionVisualizer`, `Ship Edit/ShipBlockVisualOnly.prefab` | No scene or prefab refs (GUID scan). The template-loading code is commented out. | Delete. Re-implement selection on top of the new ship renderer if needed. |
| `GhostBlockCollision.cs` plus its runtime `AddComponent` (`ShipEditorController.cs:698`) | `IsColliding` is never read. The GhostPiece `MeshCollider` has `m_Mesh: {fileID: 0}` (`GhostPiece.prefab:116`), so it can never trigger. It contains `Debug.Log` spam. | Delete, along with the ghost Rigidbody and collider. |
| `ShipEditorController`: `FixedUpdate` (:166-176), `PlaceSlottable` (:834-841), `neighborChecks` (:1018-1023), fields `voxelPrefab`, `invalidPlacementMaterial`, `SaveShipAction`/`ShowShipEditorAction`/`ContinueAction`, `ghostCollision`, unused locals in `Start:70-71`, pointless `curRotation` swap (:819-824) | Grep shows no uses. | Delete. |
| `ShipEditorManager`: `EditVoxel` struct (:58-62), `ChangeCurrentEditShip` (:804-808), `AddShipSlottable`/`BlockHasAvailableSlotModule` (callers only via dead `PlaceSlottable`), unused `queue` (:887), `[Command]` on save | Grep. | Delete. |
| `ItemInfoHelper.GetAbilityTooltipData` | Unused. | Use it (D5) or delete it. |
| `UIManager`: `ToggleCombatHudView` (duplicates `ToggleGalaxyView`), `RefreshView`, commented Character/Trade/SpaceStation blocks | Grep. | Delete. |
| `AbilitySlot` drag logic | Shift+drag works only if the pointer is released over a slot, with no capture or ghost (`AbilityBarView.cs:74-83`). | Rewrite with a manipulator when porting the HUD. |
| Assets: `UIShaders/TestShader.shadergraph`, `UITestMat.mat`, `WorldUI.uxml`, `event_dialogue.uss`, `InventoryUI.uxml`/`ToolbarViewUI.uxml`/`StoragePageUI.uxml` content (wiped by `root.Clear()` at `InventoryView.cs:21`, `ToolbarView.cs:25`, `StorageViewPage.cs:17`) | GUID scan shows no references, or the content is discarded at runtime. | Delete, or make the UXML the source of truth. |
| `PlacedBlock` module canvases (uGUI) and EventSystem with `InputSystemUIInputModule` | The only uGUI in game code is `PlacedBlock.cs:116` plus `ShipBlock.prefab` Canvas, CanvasScaler and GraphicRaycaster. `GameSettings.cs:6` has an unused `using UnityEngine.UI`. | Remove uGUI entirely once module slots move to UI Toolkit. |
| Stale prefab overrides | `ShipEditorScene.unity:708-739` (`clearanceMaterial`, `TestBlocks[5]`, fields that no longer exist) | Clean up via "Apply/Revert unused overrides". |
| Duplicates | Repair cost ×3, thumbnail loader ×2, `ResolveOpenShipEditor` ×2, rotate-cell math ×5 (section 1.5) | Centralize in a `ShipService` or `ShipEditorService`. |
| Third-party coupling | UI fonts come from `Plugins/QFSW` (debug console) and textures from `Plugins/Pixel Crushers` and `RPGUI` (`styles.uss:1-60`) | Copy the needed art into `Assets/Game/UI/Theme/`, so plugins can be stripped from builds. |

---

## 4. Performance and allocation concerns

| # | Sev | Conf | Evidence | Issue | Scale impact |
|---|---|---|---|---|---|
| P1 | **Crit** | H | `AddShipVoxel:269` → `RecalculateValidation` → `ValidateShip:835-970` | Loading a ship runs a full validation **per voxel**. Each pass rebuilds `gridMap` (a new Dictionary), allocates a List per block (`GetBlockOccupiedCells:974`), plus HashSets and Queues, does a BFS and calls `SetInvalid` on every block. That's **O(N²)**. | The 965-voxel `ship_fighter` takes about 0.9 M cell visits plus thousands of allocations. 10k blocks takes about 10⁸. 100k is infeasible. |
| P2 | **Crit** | H | `ShipBlock.prefab` | Each block is 2 GameObjects, about 9 components including an inactive Canvas, CanvasScaler, GraphicRaycaster, Billboard and LayoutGroup, plus **one BoxCollider per cell** (`PlacedBlock.cs:26-45`). Colours use per-renderer MPBs (`UpdateVisualColor` allocates a new MPB). | 100k blocks means 200k GameObjects and over 100k colliders. Physics broadphase, transform hierarchy, culling and draw-call count all scale linearly with no batching beyond SRP Batcher per renderer. |
| P3 | High | H | `OnInventoryChanged:123-133` → `UpdateBlockVisual` → `ToggleEditMode:168-207` | **Every inventory change**, including every placement since it consumes an item, loops over all blocks with `GetComponent<Renderer>()`, material assignment, layer writes and `DisplayModuleSlots`. Each edit also re-runs P1's full validation. | Each click is O(N) GameObject work plus O(N) validation. |
| P4 | Med | H | `UpdateGhostBlock:192-300` per frame | Despawns and respawns all footprint visuals each frame, calls `GetComponent<Renderer>`, `Get/SetPropertyBlock`, allocates a DOTween tween (C5), uses legacy `Input` calls and `LayerMask.GetMask("UI")` (a `params string[]` allocation each frame in Module mode, `:351`). | Constant GC pressure in the editor. |
| P5 | Med | H | `ModuleSlot` canvases | `DisplayModuleSlots → UpdateModuleSlots` destroys and re-instantiates the slot UI on every mode change (`PlacedBlock.cs:99-125`). | Hitches on mode switch in large ships. |
| P6 | Med | H | `CombatHudSystem.cs:44,52,70-79`; `GalaxyView.Combat.cs:111-127` | Every frame it does `new bool[5]`, `GetComponent<AbilityController>`, `AbilityBarView.Initialize → RefreshSlots` (rebinds sprites), two interpolated strings and four inline-style writes per slot (`AbilitySlot.UpdateState`). | Steady GC churn during flight. |
| P7 | Med | H | `FleetView` | Each `SelectShip` rebuilds the whole list (`:178`). It calls LINQ `Count/Any` over all voxels per ship (`Ship.cs:70-73`), repair cost per destroyed voxel ×3 per refresh, and `officerItem.Create(1)` per officer. Thumbnails do synchronous `File.ReadAllBytes`. | O(fleet × voxels) per click. Bad with massive ships. |
| P8 | Low | H | `StorageView.OnPointerUp:155-158` | LINQ `Where/OrderBy` allocates on every drop, and `InventorySlot.Index` is `parent.IndexOf(this)`, which is O(n). | Minor. |
| P9 | Low | H | `UIManager.IsRaycastBlocked` | Iterates 17 views per call. It's called every frame from two places. | Minor, but a symptom of having no input-context model. |
| P10 | Low | H | Debug logging | `GhostBlockCollision` logs, `ToggleSymmetry` logs, `SceneLoader` logs progress every frame, `PrebakeStats` logs, TradeView logs. | Log spam. |

---

## 5. Recommended target architecture

### 5.1 Principles

1. **One model, many views.** The ship is a chunked voxel model. The editor, runtime ECS, thumbnails, stats and saves all read the same model, so there's no conversion step and no GameObjects per block.
2. **The UI is a function of state.** ViewModels hold the state and UI Toolkit binds to them, with no imperative `label.text =` scattered through gameplay code. ECS feeds ViewModels through a bridge that writes only on change.
3. **Explicit contexts.** Input maps, pause and raycast blocking derive from the top of a navigation stack, not from ad-hoc toggles.
4. **Edits are transactions.** The editor works on a draft. Undo and redo are diffs. Inventory or economy settlement happens once, at commit.

### 5.2 UI target: UI Toolkit, MVVM, navigation stack and input contexts

```
UIRoot (single UIDocument, PanelSettings: ScaleWithScreenSize 1920×1080, Match 0.5, theme.tss)
 ├─ #layer-world     (world-space markers via RuntimePanelUtils or a second world-space PanelSettings)
 ├─ #layer-hud       HudHost: swaps FlightHud | EditorHud | StationHud by GameContext (picking Ignore by default)
 ├─ #layer-screens   UINavigator stack: Fleet, Inventory, Intel, Station/Trade, Codex …
 ├─ #layer-modals    Pause, Settings, Confirm, TextPrompt
 ├─ #layer-toasts    NotificationsVM-bound list (always above screens)
 └─ #layer-tooltip   single TooltipService (cursor-follow, ScreenToPanel)
```
Ordering is by DOM layer, not by 17 sorting-order integers. Each screen is a UXML template instantiated into a layer.

**Navigation, pause and input as one mechanism:**
```csharp
public enum InputContext { Flight, Editor, Menu, TextEntry }

public abstract class Screen {
    public virtual InputContext Context => InputContext.Menu;
    public virtual bool PausesSimulation => true;
    public virtual bool BlocksWorldPointer => true;
    public VisualElement Root { get; internal set; }
    public virtual void OnPush(object args) {}
    public virtual void OnPop() {}
    public virtual bool OnBack() => false;   // return true to consume (e.g., close a sub-panel)
}

public sealed class UINavigator {
    readonly List<Screen> _stack = new();
    readonly PauseService _pause; readonly InputContextService _input;
    public Screen Top => _stack.Count > 0 ? _stack[^1] : null;
    public void Push(Screen s, object args = null) { _stack.Add(s); s.Root.RemoveFromClassList("ui-hidden"); s.OnPush(args); Sync(); }
    public void Pop(Screen s) { if (!_stack.Remove(s)) return; s.OnPop(); s.Root.AddToClassList("ui-hidden"); Sync(); }
    public void Back() { var t = Top; if (t == null) Push(Screens.Pause); else if (!t.OnBack()) Pop(t); }
    public void CloseAll() { for (int i = _stack.Count - 1; i >= 0; i--) Pop(_stack[i]); }
    void Sync() {
        _pause.Set(this, _stack.Exists(s => s.PausesSimulation));            // ref-counted tokens
        _input.Set(Top?.Context ?? GameContext.Current);                     // enables exactly one map set
        WorldPointerBlocked = _stack.Exists(s => s.BlocksWorldPointer);
    }
}

public sealed class InputContextService {   // Input System action maps: Global(Back), Flight, Editor, UI
    public void Set(InputContext c) { /* enable maps for c; TextEntry disables all but UI+Global */ }
    public void Bind(VisualElement panelRoot) {
        panelRoot.RegisterCallback<FocusInEvent>(e => { if (e.target is TextField) Push(InputContext.TextEntry); }, TrickleDown.TrickleDown);
        panelRoot.RegisterCallback<FocusOutEvent>(e => { if (e.target is TextField) Pop(InputContext.TextEntry); }, TrickleDown.TrickleDown);
    }
}
```
This fixes B1-B6 structurally. Close buttons call `navigator.Pop(this)`, and Esc is always `navigator.Back()`.

**MVVM with Unity 6 runtime binding.** ViewModels are plain C# classes with `[CreateProperty]` that implement `INotifyBindablePropertyChanged`:
```csharp
public sealed class FleetShipVM : INotifyBindablePropertyChanged {
    public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;
    string _name; float _hull; bool _flag; int _repair;
    [CreateProperty] public string Name { get => _name; set => Set(ref _name, value); }
    [CreateProperty] public float HullPercent { get => _hull; set => Set(ref _hull, value); }
    [CreateProperty] public bool IsFlagship { get => _flag; set => Set(ref _flag, value); }
    [CreateProperty] public int RepairCost { get => _repair; set => Set(ref _repair, value); }
    void Set<T>(ref T f, T v, [CallerMemberName] string p = null) {
        if (EqualityComparer<T>.Default.Equals(f, v)) return; f = v;
        propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(p));
    }
}
public sealed class FleetVM : INotifyBindablePropertyChanged {
    [CreateProperty] public List<FleetShipVM> Ships { get; } = new();
    [CreateProperty] public FleetShipVM Selected { get; set; }   // + notify
    public void Repair(FleetShipVM s) => _fleetService.Repair(s.Id);   // commands call services, not UI
}
```
```xml
<!-- FleetScreen.uxml -->
<ui:ListView name="ships" binding-source-selection-mode="AutoAssign"
             item-template="project://database/Assets/Game/UI/Fleet/FleetShipItem.uxml">
  <Bindings><ui:DataBinding property="itemsSource" data-source-path="Ships" binding-mode="ToTarget"/></Bindings>
</ui:ListView>
<ui:Label class="title">
  <Bindings><ui:DataBinding property="text" data-source-path="Selected.Name" binding-mode="ToTarget"/></Bindings>
</ui:Label>
```
Use converter groups for `HullPercent → Length` widths and formatting. Use `LocalizedString` bindings from the Localization package (already used in `SettingsView`) for every static label.

**ECS → UI bridge.** A `PresentationSystemGroup` system copies the player's ship stats into a `FlightHudVM` only when values change, using a version or epsilon compare. It doesn't format strings and doesn't allocate. The VM owns formatting through converters. Example: `if (math.abs(vm.Hull - h) > 0.5f) vm.Hull = h;`.

**Theme.** Put tokens in `theme.uss` (`--color-accent`, `--space-2`, `--radius`) and components as USS classes (`.panel`, `.btn`, `.btn--danger`, `.slot`, `.toast`). Use no inline styles, and keep fonts and art under `Assets/Game/UI/Theme`.

**Shared services:**
- `TooltipService` with a `TooltipManipulator(Func<TooltipContent>)`, so tooltips are attached declaratively.
- `NotificationService`, with toast coalescing such as "+12 Hull" instead of 12 toasts.
- `ConfirmDialog`, `TextPrompt`.

**What to keep:**
- The `ViewUI` idea of USS-class hiding (`ui-hidden`).
- One shared PanelSettings.
- `SaveSlotListPresenter`'s reuse between Start and Pause, which should become a `SaveSlotList` component.
- `ItemInfoHelper` as a single source of tooltip text, which should be fed into a TooltipContent VM.
- NotificationView's USS-transition entry and exit.
- `VisualElementExtensions` fluent helpers.
- The CombatHudSystem push direction.

### 5.3 Ship editor target

#### 5.3.1 Data model shared by the editor and runtime

```csharp
// Block catalog (baked once from BlockItem SOs into a BlobAsset)
public struct BlockTypeDef {
    public ushort Id;  public ushort MirrorTypeId;        // chiral pair, or self
    public BlobArray<int3> Cells;                         // canonical footprint
    public BlobArray<int3> Clearance;                     // must-stay-empty cells
    public byte AttachFaceMask;                           // which faces may connect
    public float Mass, Hp, PowerGen, PowerUse; public half3 Thrust; /* … */
    public byte AllowedOrientations;                      // RotationLocked etc. as a mask/set
}
// 24 proper orientations; tables precomputed per type:  Footprint[type][orient] (sorted int3[]),
// MirrorMap[orient, axis] -> (orient', originOffset)  so mirror(place(t,o,p)) == place(t.Mirror, o', p')

public struct BlockInstance { public ushort Type; public byte Orient; public byte Color; public int3 Origin; public uint Flags; }

public sealed class ShipGrid : IDisposable {            // chunked, Burst-friendly
    public const int CS = 16;                           // 16³ = 4096 cells per chunk
    NativeParallelHashMap<int3, int> _chunkIndex;       // chunk coord -> chunk slot
    NativeList<Chunk> _chunks;                          // Chunk: fixed 4096 × uint cell -> instanceId+1 (0 = empty)
    NativeList<BlockInstance> _instances; NativeList<int> _free;
    NativeParallelMultiHashMap<int, int3> _reserved;    // clearance ref-count (replaces _reservedClearanceZones)
    public uint Version;                                // bump per change; views/VMs/blob rebuild compare
    public bool TryGetCell(int3 c, out int inst); public bool CanPlace(ushort t, byte o, int3 p, out PlaceError e);
    public int Place(in BlockInstance b); public BlockInstance Remove(int inst);
    public void MarkDirty(int3 cell);                   // dirty-chunk set for re-meshing
}
// Runtime-only state lives beside it, not inside the definition:
public struct ShipDamageState { /* per-instance HP buffer / destroyed bitset, per chunk */ }
```
- **Same model at runtime.** The ECS ship entity references the serialized `ShipGrid` definition (blob) plus a `ShipDamageState`. Chunk entities own render meshes and colliders, giving one entity per chunk rather than per voxel. Destruction changes damage state and dirties chunks. Repairs clear damage. The editor never has to "convert".
- **Persistence.** Use a versioned binary format: header, block-type palette, instance table, and per-chunk RLE of instance ids. Blueprints, templates and saves all use the same format. Name thumbnails by ship id or content hash and overwrite them (fixes A9).
- **Orientation.** Store a `byte` from 0 to 23 instead of three Euler floats (fixes C13). That gives exact equality, cheap tables and correct mirroring.

#### 5.3.2 Rendering the editing view (100k+ blocks)

- **Chunk meshing** with Burst jobs. Full-cube "structural" blocks get greedy meshing with neighbour face culling. Complex blocks such as weapons, thrusters and portals get **instanced rendering**: `BatchRendererGroup`, or `Graphics.RenderMeshInstanced`, or Entities Graphics if the editor runs in-world. A per-instance GraphicsBuffer carries orientation, colour index and state flags (selected, invalid, damaged, ghost). Only dirty chunks rebuild, so an edit touches 1-2 chunks.
- **Picking without physics.** Use a DDA voxel traversal (Amanatides-Woo) from the **mouse ray** over the grid. It returns the hit cell, face normal and instance id, costs O(cells along the ray) and has no reach limit. Delete per-cell BoxColliders, `PlacedBlock` and the per-block canvases.
- **Overlays.** Ghost, footprint, clearance and mirrored ghosts are drawn as one instanced draw of a unit cube with an URP outline shader. Use barycentric or edge-UV baked into the mesh, not a geometry shader, which fixes C7 on Metal. Invalid or disconnected blocks get a per-instance red tint or pulse in the shader, with no tweens (fixes C10).
- **Large-ship ergonomics.**
  - Layer slicing ("show y ≤ k" or a section plane), x-ray of interior systems and a hide-hull toggle.
  - A grid plane at the working layer.
  - An orbit, pan and zoom camera around a pivot, with focus on the selection and free-fly as an option.
  - Snap views: top, side and front.

#### 5.3.3 Edit operations, tools, undo and redo

```csharp
public readonly struct BlockRecord { public readonly int3 Origin; public readonly ushort Type; public readonly byte Orient, Color; public readonly int Modules; }
public interface IEditCommand { string Label { get; } void Apply(EditSession s); void Revert(EditSession s); bool TryMerge(IEditCommand next); long Bytes { get; } }
public sealed class DiffCommand : IEditCommand {          // used by every tool
    public NativeArray<BlockRecord> Removed, Added;       // minimal diff
    public void Apply(EditSession s)  { foreach (var r in Removed) s.Grid.RemoveAt(r.Origin); foreach (var a in Added) s.Grid.Place(a); s.OnChanged(this); }
    public void Revert(EditSession s) { foreach (var a in Added) s.Grid.RemoveAt(a.Origin); foreach (var r in Removed) s.Grid.Place(r); s.OnChanged(this); }
    public bool TryMerge(IEditCommand n) => /* same stroke id → concatenate */ false;
}
public sealed class EditHistory { /* undo/redo lists, memory budget (e.g., 64 MB), stroke merging, clear redo on new cmd */ }
```
Every tool resolves to a `DiffCommand`:

| Tool | What it does |
|---|---|
| Place | Single, held stroke (merged), or **Line** (drag between two cells) |
| Box | Solid, hollow or frame, with live dimensions shown |
| Fill | Flood-fill a planar region up to bounds |
| Remove | Box remove |
| Replace | Change block type in a selection |
| Paint | Brush, box or flood paint by colour |
| Eyedropper | Picks type and colour |
| Select and transform | Box-select, then move, rotate (orientation tables) or mirror (chirality tables) |
| Copy, cut and paste | Clipboard is a small `ShipGrid` |
| Blueprints | Save or load selections as files, place them as a stamp with a preview, and check BOM availability |
| Module sockets | Click a block to open its socket panel (UI Toolkit, screen-space anchored), drag modules in |

**Symmetry.** Support up to three mirror planes (X, Y and Z). Each plane is anchored at the core or a user point and snaps to either a **cell centre or a cell boundary**, which handles both odd and even widths. Add optional N-fold radial symmetry around an axis. All mirrored placements are computed with `MirrorMap` and validated **as one atomic set**. Show the planes in-scene and the state in the HUD. Mirrored removal only removes instances whose mirrored record matches type, origin and orientation (fixes C2 and C3).

#### 5.3.4 Validation and stats (incremental)

- **Placement-time checks:** occupancy, clearance (including others' reserved cells), attach-face rules, orientation allowed, budget limits (CPU, mass, power, block count) and "would this disconnect?" warnings.
- **Connectivity:** union-find on additions, near O(1). On removal, run a bounded local flood from the removed cells' neighbours toward the core. Recompute chunk-graph connectivity in a Burst job at commit. Disconnected islands get a tint and are listed in the HUD, where clicking focuses the camera.
- **Core rules:** exactly one core, plus build bounds as a real AABB or radius over **cells** (fixes C9).
- **Stats preview:** `ShipStatsAccumulator.Add/Remove(BlockInstance)` keeps running totals: mass and centre of mass, inertia approximation, thrust per axis and thrust-to-weight, power gen and use, DPS, shield, cost and block count. `EditorHudVM` updates after each command. Show the **diff vs the original**, for example "+2.3 t mass, −5% T/W".

#### 5.3.5 Economy: draft and commit

```csharp
public sealed class EditSession {
    public ShipGrid Original, Grid;               // Grid = draft copy
    public EditHistory History;
    public BillOfMaterials Bom => BillOfMaterials.Diff(Original, Grid);   // required vs refunded per type
    public CommitResult TryCommit(Inventory inv, bool creative) {
        var v = Validator.Full(Grid); if (!v.Ok) return CommitResult.Invalid(v);
        if (!creative && !inv.CanAfford(Bom)) return CommitResult.Missing(Bom);
        inv.Apply(Bom); ship.Definition = Grid.Freeze(); thumbnails.Request(ship); save.MarkDirty(); return CommitResult.Ok;
    }
}
```
This makes A1-A5, A8 and A12 impossible by construction. Leaving prompts with **Commit / Discard / Keep editing**. Damaged blocks render with a damage overlay. Removing a destroyed block yields salvage, not a full refund, and repair is a separate costed action (fixes A4). "Creative" becomes a session flag, not `Global.DEV_MODE`.

#### 5.3.6 In-world editing vs a separate scene

**Recommendation: in-world Edit Mode as the primary path**, available when docked or in a safe zone, or always for owned stations. Keep an optional "Blueprint Lab" mode, which is the same code on an empty grid with no economy.
- **Pros:** no scene load, real context (docking ports, scale, neighbouring stations), and one renderer and one model. This is required anyway for station building and live destruction.
- **Cons:** the ship's simulation must be frozen or isolated. Use a safe zone, put the ship entity in a `ShipEditing` state that disables its systems, and hide the live chunks while the draft renders. Colliders rebuild only at commit.
- **Handoff at commit:** swap the ship's definition blob reference, mark all chunks dirty, rebuild render and collision chunks asynchronously, then fire `ShipDefinitionChanged` for the VMs, fleet list and thumbnail.

#### 5.3.7 HUD for the editor (UI Toolkit, `EditorHudVM`)

- A palette with categories, search and favourites. This is **decoupled from inventory slots 0-8**: the hotbar holds palette favourites, and inventory counts show as badges.
- A tool strip, symmetry toggles with plane indicators, a layer slicer, an orientation widget, and the stats and diff panel.
- A validation list, the BOM ("needs 12 Hull, you have 8"), Commit and Discard buttons, undo and redo buttons with labels, and a key-hint footer that reads current bindings.

### 5.4 Migration path

| Phase | Content | Keeps the game playable? |
|---|---|---|
| **0. Stabilize** (days) | Quick wins in section 6, delete dead code (section 3). | Yes |
| **1. UI infrastructure** | `UIRoot` with layers. `UINavigator`, `PauseService` (ref-counted), `InputContextService` (with TextEntry) and `TooltipService`. Port Pause, Settings, Fleet and ShipEditorLoad to `Screen`. Make `UIManager.Toggle*` thin shims that call the navigator, then delete them. | Yes. Views migrate one at a time. |
| **2. MVVM** | VMs and binding for Fleet, Inventory/Hotbar, Flight HUD (with the ECS bridge using change detection), Notifications (coalescing) and Settings (rebinding fixed and persisted). Add theme tokens and localization bindings. | Yes |
| **3. Editor model extraction** | Bake the `BlockCatalog` (orientation, footprint and mirror tables). Introduce `ShipGrid`, which can start managed and move to Native later, plus `EditSession`, `EditHistory` and `Validator`. **Keep the current GameObject renderer temporarily** as an adapter that listens to grid changes. That alone delivers undo/redo, draft/commit economy and correct symmetry, and removes O(N²) validation. Convert `ShipData ↔ ShipGrid` at the boundaries. | Yes |
| **4. Renderer and picking swap** | Chunk meshing plus instancing, DDA picking and URP overlay shaders. Delete `PlacedBlock`, colliders, uGUI canvases and EventSystem. | Yes, behind a feature flag |
| **5. Tools** | Box, line, fill, select, transform, copy and paste, blueprints, slicing and the stats diff. | Yes |
| **6. Unify with runtime** | The ECS ship uses the `ShipGrid` definition directly (chunk entities and damage state). Add in-world edit mode, then station building. | Coordinate with the ECS and voxel-runtime auditors. |

---

## 6. Quick wins (each is small and local)

1. **Close buttons.**
   - `FleetView.cs:75` → `UIManager.ToggleFleetView(false)`.
   - `SettingsView.cs:74` → `UIManager.ToggleSettingsView(false)`.
   - `ShipEditorController.cs:78` → `UIManager.ToggleShipEditorLoadUI(false)`.
   - In `ToggleMerchantPage`, do the null check before `PauseGame` (`UIManager.cs:254-258`).
2. **CloseAllPages** (`UIManager.cs:85-94`): iterate `_openViewsStack` in reverse and close each through its own toggle, or add a virtual `Close()` to `ViewUI`. Recompute pause as `stack.Any(v => v.PausesGame)` instead of a bool.
3. **Text-entry guard.** Register `FocusInEvent` and `FocusOutEvent` on each UIDocument root. While a `TextField` has focus, call `InputSystem.actions.FindActionMap("Player"/"UI"/"Editor").Disable()`. Also skip `InputManager` hotkeys in StartScene.
4. **Editor economy hotfix.** At editor entry, take `Inventory.CreateSnapshot()` (it already exists, `Inventory.cs:317`). On leaving without a successful save, call `RestoreFromSnapshot`. Make `SaveShipDataToShip` return `bool`, and persist only when it's true (`ShipEditorController.cs:180-188`). Show `GetValidationErrorMessage` through `UIManager.ShowNotification` (`ShipEditorManager.cs:505`). Disable template loading outside DEV, or charge the BOM for it.
5. **Destroyed voxels.** Skip refunds for `IsDestroyed` in `RemoveShipVoxel`, and tint destroyed blocks in `AddShipVoxel`.
6. **Refund safety.** Check the `TryAddItem` result in `RemoveShipVoxel` and `UninstallModule` (block removal if the inventory is full). In Merchant, add the item first, then charge, and use `RemoveQuantity(index, 1)` instead of `Remove(item)`.
7. **O(N²) load.** Add a `_suppressValidation` flag around the `InstantiateShip` loop and validate once at the end. Also reuse `_occupiedCells` instead of rebuilding `gridMap` in `ValidateShip` (`:889-897`).
8. **Selected slot.** Delete `curBlockIndex` and always use `toolbarView.GetSelectedSlot()`. Call `UpdateBlockVisual(currentItem)` at the end of `Start`.
9. **Ghost.** Use `curRotation`, not `ghostBlock.transform.rotation`, for footprint cells and indicators. Compute `curRotation` before `CanPlaceBlockAt`. Replace the per-frame `DOMove` with direct assignment, or `DOKill` then a single tween.
10. **Symmetric removal.** Remove only when `mirrored.BlockData == original.BlockData` and the mirrored block's root equals `mirroredPos`. Until mirror tables exist, block symmetry for blocks whose footprint isn't X-symmetric.
11. **Stats HUD.** Call `ToggleShipStatsView(true)` before `InstantiateShip`. Hide the stats view in `HandleContinueAction` and in `ShipEditorManager.OnDestroy`.
12. **InventoryView** slot count: use `playerInv.Capacity` (or 60), not 59 (`InventoryView.cs:17`).
13. **Notifications.** Raise the NotificationView sort order above every screen, for example 20000. Coalesce `±1` toasts in the editor, or suppress them while `EditSession` is active.
14. **Rebinding.** Wrap it in `action.Disable()` … `OnComplete/OnCancel { Dispose(); action.Enable(); Save overrides }`, and filter out UI-map and composite bindings.
15. **FleetView.** Bind the five placeholder stats from `ShipData.GlobalStats`, or remove them. Drop `width: 945px`. Call `Player.EnsureShipThumbnailLoaded` instead of its own loader.
16. **Thumbnails.** Write `ShipThumbnails/{shipId}.png` (overwrite, which also matches the `Player` recovery suffix). `Destroy` the old Texture2D and the RenderTexture.
17. **Symmetry indicator.** Subscribe a ShipStats HUD label to `OnSymmetryChanged`, and draw a translucent X=0 plane.
18. **Metal.** Replace `Wireframe.shader`'s geometry pass with a URP Unlit transparent material, or an edge-UV cube mesh.
19. **Delete dead code and assets** listed in section 3, and remove stale prefab overrides in `ShipEditorScene.unity`.
20. **StartScene.** Add a clearing Camera. Move fonts out of `Plugins/QFSW` into `Assets/Game/UI/Theme/Fonts`.

---

### Appendix: notes for other auditors

- **ECS and persistence:** `ShipData` gets replaced on every editor save. The old instance's blob is disposed in a finalizer (`ShipData.cs:139-146`), which runs on the GC thread and isn't safe for `BlobAssetReference.Dispose`. `Ship.RepairAll` doesn't invalidate the blob.
- **Galaxy:** `GalaxyView.Update` runs the solar-map update every frame. The combat HUD state classes are undefined in USS (E10).
- **Encounters:** `EncounterManager.EndRun`, `GenerateEncounterLoot` and `AddEncounterLoot` exist only for the dead RunEnd and LootPicker views.
- **Input:** `Tab` is bound to Continue, IntelPage and ChangeCamera, and `E` is both Interact and the FreeFly "up" key. Project-wide actions are all enabled all the time.
