# SPEC-61 — Screens and HUD

| | |
|---|---|
| **Status** | Draft |
| **Area code** | HUD (task IDs are `HUD-001`, `HUD-002`, …) |
| **Module(s)** | `Game.UI` (views, view models, UXML, screen USS), `Game.Presentation` (`HudSnapshotSystem`, `HudSyncSystem`, presenters for economy/narrative/galaxy screens, hotkey router), `Game.App` (presenters that need `SceneFlow`, `SaveService`, `SettingsService`, `GameSession`: folder `Assets/Game/App/Presenters/`, because only `Game.App` may reference those services) |
| **Depends on specs** | SPEC-60 (framework), SPEC-62 (`MainCameraState`, `MapViewTransform`), SPEC-01 (scene flow, session, settings, root screens), SPEC-03 (actions), SPEC-05 (save slots), SPEC-10/13 (`VoxelGrid`, `Hulk`), SPEC-20 (`PlayerShip`), SPEC-21 (weapon state, target), SPEC-22 (`ShipStats`, `ShieldState`, `ShipPower`), SPEC-24 (abilities), SPEC-30/31 (galaxy, bodies, interaction), SPEC-32 (destination), SPEC-33 (relations), SPEC-41 (dialog presenter), SPEC-42 (quests, clues, rumors), SPEC-50 (inventory, wallet, merchant) |
| **Depended on by** | SPEC-41 (writes `DialogViewModel`), SPEC-51 (mounts panels into `ShipEditorView`, reuses `StatRowViewModel`), SPEC-01 (`SetRoot` with `MainMenuView`/`FlightHudView`/`ShipEditorView`, uses `LoadingView`), SPEC-70 (harness element names) |
| **Milestone** | M3 |

> Read [01-architecture.md](../01-architecture.md), [02-conventions.md](../02-conventions.md) and [03-glossary.md](../03-glossary.md) before this spec. This spec may not contradict them.

## 1. Purpose

This spec defines every game screen: what it is for, its view model, its commands, its input context, whether it pauses, and a wireframe of its layout in tokens. It also defines the flight HUD and the pipeline that feeds it: a Burst `HudSnapshotSystem` that reads ECS once per frame into one unmanaged snapshot, and a managed `HudSyncSystem` that copies changed values into view models with zero allocation. UI code never queries ECS. It fixes the old prototype's overlapping HUD texts and quarter-screen radar (R7), dialog options below the screen and HUD drawn over dialogs (R11), merchant tabs off both screen edges (R12), the editor HUD left in the galaxy (R13, 06-B7), clipped inventory rows and buttons floating outside panels, overlapping fleet headers (R31), the unreadable name field (R4), save slots deleted with one click (06-B12) and per-frame HUD allocation (06-P6).

## 2. Scope

**In scope:** the screens in §4.1; HUD zones and widgets (hull/shield/energy meters, speed, weapon groups + cooldowns, ability slots, target info, interaction prompt, location, tracked objective, radar); `HudSnapshotSystem`, `HudSyncSystem`; presenters; screen hotkey routing; map canvas control; layout regression tests.

**Out of scope (and who owns it instead):** framework, controls, tooltip/toast/confirm/prompt services → SPEC-60; ship editor panel contents → SPEC-51; dialog flow, Yarn and dialog input → SPEC-41; quest/clue data → SPEC-42; the 3D camera → SPEC-62; final look → SPEC-80/81; thumbnails of ships and saves → not in M3 (§10 Q16).

## 3. Decisions (binding)

1. **Screen table §4.1 is binding** for key, layer, context, pause, pointer blocking, HUD hiding and root status.
2. **Three root screens:** `MainMenuView` (MainMenu scene), `FlightHudView` (World scene), `ShipEditorView` (BlueprintLab scene and in-world edit). `SceneFlow` (SPEC-01) installs them with `UINavigator.SetRoot`, so exactly one HUD root is visible at any time (R13).
3. **HUD data path:** ECS → `HudSnapshotSystem` (Burst, once per frame) → `HudSnapshot` singleton + `HudRadarBlip` buffer → `HudSyncSystem` (managed, reads the singleton once) → view models (only changed, quantised values) → views (direct element updates for per-frame values). No other UI-side code reads ECS.
4. **HUD zones are fixed** (§4.4); each zone is one `.hud-region` with one anchor class. Widgets never use absolute positioning inside a zone.
5. **Presenter placement:** presenters that need only module models live in `Game.Presentation/Presenters/` (Inventory, Merchant, ShipLog, GalaxyMap, StarSystemMap, HUD objective, HUD notifications, hotkeys); presenters that need `SceneFlow`, `SaveService`, `SettingsService` or `GameSession` live in `Game.App/Presenters/` (MainMenu, SaveSlots, Settings, Pause, Fleet, Hulk, Loading). New folder `Presenters/` in both modules: reason, one presenter per screen and neither `Services/` nor `Views/` describes them.
6. **View models are created once at boot** by `Game.App` and live for the app lifetime; presenters rebind to new session models on `SessionReset` (never replace view model instances, so bindings never go stale: 04-B2).
7. **Lists:** `ListView` with virtualization for variable-length lists (save slots, merchant stock and cargo, quests, clues, rumors, fleet, settings rebinds); fixed pools created at build for grids (inventory slots: 200; radar blips: 64; weapon groups: 5; ability slots: 4; dialog options: 9).
8. **Destructive actions confirm** through `ConfirmDialog`: delete save, overwrite save, load while in game, quit to main menu, quit game.
9. **Maps are 2D** (`MapCanvas` + `MapViewTransform`, SPEC-62). Panning moves the drawn content with `style.translate`; geometry is re-tessellated only when data changes or zoom changes by more than 10 %.
10. **Screen hotkeys toggle** through `ScreenHotkeyRouter` (§5.5); the Flight and Menu maps both carry the five screen actions so the same key closes the screen.

## 4. Data model

### 4.1 Screen inventory

| Key | View (Game.UI.Views) | Layer | Context | Pauses | Blocks pointer | Hides HUD | Root | View model | Commands | Presenter (module) |
|---|---|---|---|---|---|---|---|---|---|---|
| `main-menu` | `MainMenuView` | Screens | Menu | no | yes | no | **yes** | `MainMenuViewModel` | `IMainMenuCommands` | `MainMenuPresenter` (App) |
| `save-slots` | `SaveSlotsView` | Screens | Menu | yes | yes | yes | no | `SaveSlotsViewModel` | `ISaveSlotsCommands` | `SaveSlotsPresenter` (App) |
| `settings` | `SettingsView` | Modals | Menu | yes | yes | no | no | `SettingsViewModel` | `ISettingsCommands` | `SettingsPresenter` (App) |
| `pause` | `PauseView` | Modals | Menu | yes | yes | no | no | `PauseViewModel` | `IPauseCommands` | `PausePresenter` (App) |
| `hud` | `FlightHudView` | Hud | Flight | no | no | no | **yes** | `HudViewModel` | — | `HudSyncSystem`, `HudObjectivePresenter`, `HudNotificationPresenter` (Presentation) |
| `inventory` | `InventoryView` | Screens | Menu | yes | yes | no | no | `InventoryViewModel` | `IInventoryCommands` | `InventoryPresenter` (Presentation) |
| `merchant` | `MerchantView` | Screens | Menu | yes | yes | yes | no | `MerchantViewModel` | `IMerchantCommands` | `MerchantPresenter` (Presentation) |
| `dialog` | `DialogView` | Screens | Dialog | yes | yes | yes | no | `DialogViewModel` | `IDialogCommands` | SPEC-41 presenter |
| `ship-log` | `ShipLogView` | Screens | Menu | yes | yes | yes | no | `ShipLogViewModel` | `IShipLogCommands` | `ShipLogPresenter` (Presentation) |
| `galaxy-map` | `GalaxyMapView` | Screens | Menu | yes | yes | yes | no | `GalaxyMapViewModel` | `IGalaxyMapCommands` | `GalaxyMapPresenter` (Presentation) |
| `system-map` | `StarSystemMapView` | Screens | Menu | yes | yes | yes | no | `StarSystemMapViewModel` | `IStarSystemMapCommands` | `StarSystemMapPresenter` (Presentation) |
| `fleet` | `FleetView` | Screens | Menu | yes | yes | yes | no | `FleetViewModel` | `IFleetCommands` | `FleetPresenter` (App) |
| `ship-editor` | `ShipEditorView` | Hud | Editor | no | no | no | **yes** | `ShipEditorViewModel` (SPEC-51) | `IShipEditorCommands` (SPEC-51) | SPEC-51 host (App) |
| `hulk` | `HulkView` | Modals | Menu | no | yes | no | no | `HulkViewModel` | `IHulkCommands` | `HulkPresenter` (App) |
| `loading` | `LoadingView` | Modals | None | yes | yes | yes | no | `LoadingViewModel` | — | `LoadingPresenter` (App) |

Back behaviour: `FlightHudView.OnBack()` pushes `PauseView` and returns true. `MainMenuView.OnBack()` returns true (nothing happens). `ShipEditorView.OnBack()` returns `commands.OnBackPressed()`. `HulkView.OnBack()` and `LoadingView.OnBack()` return true (cannot be dismissed). `SettingsView.OnBack()` cancels a listening rebind first (returns true), otherwise false (pops). All others return false (pop).

### 4.2 View model rule and the view models

Every view model is `public sealed class <Name>ViewModel : ViewModelBase` in `Game.UI.ViewModels` (file `Assets/Game/UI/ViewModels/<Name>ViewModel.cs`). For every scalar property `P` of type `T` below, write exactly: a private field, `static readonly BindablePropertyChangedEventArgs PChanged = new BindablePropertyChangedEventArgs(nameof(P));`, and `[CreateProperty] public T P { get => p; set => Set(ref p, value, PChanged); }`. For every list property, write `[CreateProperty] public List<TItem> P { get; } = new List<TItem>(capacity);` plus `public void NotifyP() => Notify(PChanged);`. Item view models follow the same rule. Defaults are C# defaults unless stated.

```csharp
// Shared
StatRowViewModel        { string StatId; string Label; string Value; string Delta; int DeltaSign /* -1 worse, 0, +1 better */; }

MainMenuViewModel       { bool HasContinue; string ContinueSubtitle; string VersionText; }

SaveSlotsViewModel      { SaveSlotsMode Mode /* Load, Save */; List<SaveSlotItemViewModel> Slots (64); int SelectedIndex = -1;
                          bool CanLoad; bool CanSave; bool CanDelete; string EmptyText; }
SaveSlotItemViewModel   { string SlotId; string Title; string LocationText; string TimestampText; string PlaytimeText; bool IsAutosave; bool IsSelected; }

SettingsViewModel       { int SelectedTab; float MasterVolume; float MusicVolume; float SfxVolume; float UiVolume;
                          List<string> Resolutions (32); int ResolutionIndex; int FullscreenModeIndex; bool VSync; List<string> QualityNames (8); int QualityIndex;
                          float MouseSensitivity; bool InvertOrbitY; List<RebindRowViewModel> Rebinds (64); int ListeningRow = -1;
                          int CameraYawModeIndex; float ScreenShake; float UiScale; int TextScaleIndex; bool ReduceMotion; }
RebindRowViewModel      { string ActionPath; string DisplayName; string BindingText; bool IsListening; }

PauseViewModel          { bool CanSave; string SaveBlockedReason; }

HudViewModel            { bool HasShip; bool IsHulk;
                          int HullValue; int HullMax; float HullFraction; int ShieldValue; int ShieldMax; float ShieldFraction; int ShieldStatus /* 0 none,1 online,2 recharging,3 offline */;
                          int EnergyValue; int EnergyMax; float EnergyFraction; int SpeedMps; bool Boosting;
                          bool HasTarget; string TargetName; int TargetRelation /* 0 neutral,1 ally,2 enemy */; float TargetHullFraction; float TargetShieldFraction; int TargetDistanceM;
                          bool HasInteraction; string InteractionVerbText; string InteractionTargetName; string InteractionKeyText; bool InteractionInRange;
                          string StarSystemName; string ZoneName; bool HasObjective; string ObjectiveTitle; string ObjectiveText;
                          List<HudWeaponGroupViewModel> WeaponGroups (5, filled once with 5 items); List<HudAbilityViewModel> Abilities (4, filled once with 4 items);
                          int RadarVersion; float RadarRangeM = 2000; float RadarYawDeg; }
HudWeaponGroupViewModel { int GroupNumber; int WeaponCount; float ReadyFraction; bool Firing; string KeyText; }
HudAbilityViewModel     { bool HasAbility; string Name; UnityEngine.Sprite Icon; float CooldownFraction; int Charges; bool Ready; bool Active; string KeyText; }

InventoryViewModel      { int Capacity; int UsedSlots; List<InventorySlotViewModel> Slots (200, filled once with 200 items); int SelectedIndex = -1;
                          string SelectedName; string SelectedDescription; int SelectedCount; int SelectedValue; int Wallet; }
InventorySlotViewModel  { int Index; string ItemId; UnityEngine.Sprite Icon; int Count; bool IsEmpty = true; bool IsSelected; bool InCapacity; }

MerchantViewModel       { string MerchantName; int Wallet; List<string> Categories (16); int SelectedCategory;
                          List<MerchantRowViewModel> Stock (128); List<MerchantRowViewModel> Cargo (200); int SelectedStockIndex = -1; int SelectedCargoIndex = -1;
                          int Quantity = 1; int MaxQuantity; int BuyTotal; int SellTotal; bool CanBuy; bool CanSell; string BuyBlockedReason; string SellBlockedReason;
                          string DetailName; string DetailDescription; }
MerchantRowViewModel    { string ItemId; string Name; UnityEngine.Sprite Icon; int Price; int Quantity; bool IsSelected; }

DialogViewModel         { string SpeakerName; UnityEngine.Sprite Portrait; bool HasPortrait; string LineText; int LineVersion;
                          List<DialogOptionViewModel> Options (9); bool CanContinue; bool IsWaitingForChoice; }
DialogOptionViewModel   { int Number /* 1..9 */; string Text; bool Enabled; string DisabledReason; }

ShipLogViewModel        { int SelectedTab /* 0 quests,1 clues,2 rumors */; List<QuestRowViewModel> Quests (64); int SelectedQuest = -1;
                          string QuestTitle; string QuestStageText; string QuestDescription; List<ObjectiveRowViewModel> Objectives (16);
                          List<ClueRowViewModel> Clues (256); int SelectedClue = -1; string ClueBody; List<RumorRowViewModel> Rumors (64); }
QuestRowViewModel       { string QuestId; string Title; string StageText; bool IsTracked; bool IsCompleted; bool IsSelected; }
ObjectiveRowViewModel   { string Text; bool IsDone; }
ClueRowViewModel        { string ClueId; string Title; string MysteryTitle; bool IsNew; bool IsSelected; }
RumorRowViewModel       { string RumorId; string Title; string LocationText; bool HasMapTarget; }

GalaxyMapViewModel      { MapData Data /* class below, replaced contents + DataVersion bump */; int DataVersion; int SelectedIndex = -1; string SelectedName; string SelectedInfo;
                          bool CanSetDestination; string DestinationBlockedReason; }
StarSystemMapViewModel  { MapData Data; int DataVersion; int SelectedIndex = -1; string SelectedName; string SelectedInfo; bool CanSetWaypoint; }

FleetViewModel          { List<FleetShipRowViewModel> Ships (32); int SelectedIndex = -1; string DetailName; string DetailClass; List<StatRowViewModel> DetailStats (12);
                          bool CanEdit; string EditBlockedReason; bool CanSetActive; }
FleetShipRowViewModel   { ulong PersistentId; string Name; float HullFraction; bool IsActive; int BlockCount; bool IsSelected; }

HulkViewModel           { string Title; string Body; bool HasSave; }
LoadingViewModel        { string StatusText; float Progress; bool ShowProgress; }
```

`MapData` (plain class, `Game.UI.Data`, not a view model; presenters fill it, then bump `DataVersion`):

```csharp
public sealed class MapData
{
    public readonly List<MapPoint> Points = new List<MapPoint>(1024);   // stars or bodies
    public readonly List<MapLine> Lines = new List<MapLine>(4096);      // lanes (galaxy); unused for star systems
    public readonly List<MapRing> Rings = new List<MapRing>(64);        // orbits (star system)
    public readonly List<MapMarker> Markers = new List<MapMarker>(64);  // player, destination, rumors
    public Unity.Mathematics.double2 BoundsMin, BoundsMax;              // map units
}
public struct MapPoint  { public Unity.Mathematics.double2 Pos; public string Name; public float RadiusPx; public MapPointFlags Flags; public byte ColorIndex; }
[System.Flags] public enum MapPointFlags : byte { None = 0, Known = 1, Visited = 2, Current = 4, Destination = 8, Selected = 16, Interactable = 32 }
public struct MapLine   { public int A, B; public bool Gated; public bool Known; }   // indices into Points
public struct MapRing   { public Unity.Mathematics.double2 Center; public double Radius; }
public struct MapMarker { public Unity.Mathematics.double2 Pos; public MapMarkerKind Kind; public float HeadingDeg; public string Label; }
public enum MapMarkerKind : byte { Player = 0, Destination = 1, Rumor = 2, Waypoint = 3 }
```

`ColorIndex` maps to custom properties `--maps-color-0` … `--maps-color-7` declared on the class `.maps-canvas` in `Theme/screens/maps.uss` (values are `var(--color-…)` tokens only); the canvas reads them from its `customStyle` on `CustomStyleResolvedEvent`. Screen-scoped classes of this spec: `.hud-narrow`, `.hud-blip`, `.hud-blip--ship`, `.hud-blip--station`, `.hud-blip--body`, `.hud-blip--poi`, `.hud-blip--beyond`, `.maps-canvas`, `.maps-label`, `.dialog-option`, `.inventory-grid-row`, `.merchant-row`, `.fleet-row`, `.ship-log-row` (SPEC-60 decision 12).

### 4.3 Command interfaces (`Assets/Game/UI/Views/Commands/`, namespace `Game.UI.Views`)

```csharp
public enum SaveSlotsMode : byte { Load = 0, Save = 1 }
public enum SettingKey : byte { MasterVolume, MusicVolume, SfxVolume, UiVolume, Resolution, FullscreenMode, VSync, Quality,
                                MouseSensitivity, InvertOrbitY, CameraYawMode, ScreenShake, UiScale, TextScale, ReduceMotion }

public interface IMainMenuCommands       { void Continue(); void NewGame(); void Load(); void OpenLab(); void OpenSettings(); void Quit(); }
public interface ISaveSlotsCommands      { void Select(int index); void LoadSelected(); void SaveToSelected(); void SaveNew(); void DeleteSelected(); }
public interface ISettingsCommands       { void SelectTab(int tab); void SetFloat(SettingKey key, float value); void SetBool(SettingKey key, bool value);
                                           void SetIndex(SettingKey key, int index); void StartRebind(int row); void CancelRebind(); void ResetBindings(); }
public interface IPauseCommands          { void Resume(); void Save(); void Load(); void OpenSettings(); void MainMenu(); void Quit(); }
public interface IInventoryCommands      { void Select(int slot); void Move(int fromSlot, int toSlot); void Sort(); void StackAll(); }
public interface IMerchantCommands       { void SelectCategory(int index); void SelectStock(int index); void SelectCargo(int index); void SetQuantity(int quantity); void Buy(); void Sell(); }
public interface IDialogCommands         { void Choose(int number); void Continue(); }
public interface IShipLogCommands        { void SelectTab(int tab); void SelectQuest(int index); void ToggleTrack(int index); void SelectClue(int index); void ShowRumorOnMap(int index); }
public interface IGalaxyMapCommands      { void Select(int pointIndex); void SetDestination(); void FocusCurrent(); }
public interface IStarSystemMapCommands  { void Select(int pointIndex); void SetWaypoint(); void FocusPlayer(); }
public interface IFleetCommands          { void Select(int index); void Edit(); void SetActive(); }
public interface IHulkCommands           { void LoadLastSave(); void MainMenu(); }
```

Close buttons (`<key>-close`) call `UIScreen.Close()` inside the view; they are not commands.

### 4.4 HUD zones (`FlightHudView`, `Theme/screens/hud.uss`)

| Zone | Region name | Anchor class | Contents (element names) |
|---|---|---|---|
| Z-TL | `hud-region-location` | `.anchor-top-left` | `hud-star-system` (label), `hud-zone` (label) |
| Z-TC | `hud-region-objective` | `.anchor-top-center` | `hud-objective-title`, `hud-objective-text` (max width 560 px, wraps, max 3 lines then ellipsis) |
| Z-TR | `hud-region-radar` | `.anchor-top-right` | `hud-radar` (240 × 240 px square), `hud-radar-range` (label) |
| Z-R | `hud-region-target` | `.anchor-right-center` | `hud-target-name`, `hud-target-relation` (marker), `hud-target-hull` (Meter), `hud-target-shield` (Meter), `hud-target-distance` (width 280 px) |
| Z-C | `hud-region-prompt` | `.anchor-center` (offset 18 % of height below centre) | `hud-interaction-key` (KeyHint), `hud-interaction-verb`, `hud-interaction-target` |
| Z-BL | `hud-region-status` | `.anchor-bottom-left` | `hud-hull-meter`, `hud-shield-meter`, `hud-energy-meter` (Meters, width 320 px), `hud-speed`, `hud-shield-status` |
| Z-BC | `hud-region-weapons` | `.anchor-bottom-center` | `hud-weapon-group-1` … `hud-weapon-group-5` (Slots), `hud-ability-1` … `hud-ability-4` (Slots) |
| Z-BR | — | — | reserved (empty) |

Toasts stay in `layer-toasts` in the column defined by SPEC-60 (`toast-column`, right edge, below the radar). Every zone has class `.hud-plate`. At logical 1663×1247 (4:3) the objective max width shrinks to 480 px via the `.hud-narrow` class that `FlightHudView` adds when the panel width is below 1700 px (set on `GeometryChangedEvent`, no per-frame work).

Wireframe (1920 × 1080 logical):

```
┌──────────────────────────────────────────────────────────────────────────────┐
│ Z-TL Kestrel System           Z-TC ◆ Find the beacon          Z-TR ┌──────┐ │
│      Ark debris field              Scan the derelict (0/1)          │radar │ │
│                                                                     └──────┘ │
│                                                         toasts column → ▭▭  │
│                                                                   Z-R Target │
│                               [E] Dock  Ark Station                 ▬▬ hull  │
│                                                                     ▬▬ shld  │
│ Z-BL HULL  ▬▬▬▬▬▬▬ 965/965        Z-BC [1][2][3][4][5]  (Q)(E)(R)(F)         │
│      SHLD  ▬▬▬▬▬ 200/200                                                    │
│      ENRG  ▬▬▬▬ 80/120   72 m/s                                             │
└──────────────────────────────────────────────────────────────────────────────┘
```

### 4.5 HUD snapshot (ECS, `Game.Presentation.Components`, *internal to Presentation*)

```csharp
/// Implements SPEC-61 §4.5. Singleton written once per frame by HudSnapshotSystem.
public struct HudSnapshot : Unity.Entities.IComponentData
{
    public HudFlags Flags;
    public int HullCells, HullCellsMax;                 // §10 Q1
    public float ShieldHp, ShieldMax; public byte ShieldStatus;
    public float Energy, EnergyMax;
    public float SpeedMps;
    public Unity.Collections.FixedList64Bytes<HudWeaponGroupState> WeaponGroups;  // up to 5, index = group - 1
    public Unity.Collections.FixedList128Bytes<HudAbilityState> Abilities;        // up to 4
    public Game.Core.ContentHash TargetNameHash;        // display-name key of the target's blueprint (§10 Q4)
    public byte TargetRelation; public float TargetHull01, TargetShield01, TargetDistanceM;
    public byte InteractionVerb; public float InteractionDistanceM; public byte InteractionInRange;
    public ulong InteractionBodyKey;                    // BodyId packed by SPEC-31 helper (§10 Q5)
    public uint StarSystemIdValue;                      // StarSystemId.Value of the current star system
    public uint RadarVersion;                           // +1 on every radar refresh (10 Hz)
    public uint FrameIndex;
}
[System.Flags] public enum HudFlags : byte { None = 0, HasShip = 1, IsHulk = 2, HasTarget = 4, HasInteraction = 8, Boosting = 16 }
public struct HudWeaponGroupState { public byte Group; public byte WeaponCount; public byte Firing; public byte Pad; public float Ready01; }   // 8 bytes
public struct HudAbilityState { public Game.Core.ContentHash Ability; public float Cooldown01; public byte Charges; public byte Ready; public byte Active; public byte Pad; } // 16 bytes

/// Buffer on the snapshot entity. [InternalBufferCapacity(0)]; at most 64 elements.
public struct HudRadarBlip : Unity.Entities.IBufferElementData
{
    public Unity.Mathematics.float2 OffsetXZ;          // metres relative to the player ship, world axes
    public byte Kind;                                  // 0 ship, 1 station, 2 planet/large body, 3 interactable point
    public byte Relation;                              // 0 neutral, 1 ally, 2 enemy
    public byte Flags;                                 // 1 = targeted, 2 = beyond range (clamped to edge)
    public byte Pad;
}
```

Memory: snapshot ≈ 260 bytes + 64 × 12 bytes of blips. Constant regardless of world size.

## 5. Systems, services and flow

### 5.1 `HudSnapshotSystem`

- `ISystem`, `[BurstCompile]`, `[UpdateInGroup(typeof(PresentationBridgeGroup))]`, `[UpdateAfter(typeof(CameraTargetSyncSystem))]`, `[UpdateBefore(typeof(HudSyncSystem))]`. `OnCreate`: create the snapshot entity (with `HudSnapshot` and the blip buffer), `RequireForUpdate<GameplayActive>()`.
- Reads: player ship (`PlayerShip`, `ShipStats`, `ShieldState`, `ShipPower`, `PhysicsVelocity`, `Hulk`, target component), weapon functional entities, ability buffer, interaction singleton, current star system singleton, grid roots with `Allegiance`+`LocalToWorld`, body entities. Writes only `HudSnapshot` and `HudRadarBlip`.

Per frame:
1. Find the single `PlayerShip` entity with `SystemAPI.TryGetSingletonEntity<PlayerShip>`. None → write `Flags = None`, keep the radar buffer empty, increment nothing else, return.
2. Fill ship fields from components (§10 Q1). `SpeedMps = length(PhysicsVelocity.Linear)`. `Boosting` from `ShipCommand.Flags` (SPEC-20 boost bit). `IsHulk` when the ship has `Hulk`.
3. Weapon groups: schedule `HudWeaponGatherJob` (`IJobChunk`, single-threaded `Schedule`, Burst) over entities with `FunctionalBlock` and the SPEC-21 weapon state (§10 Q2), filter `FunctionalBlock.Grid == player`, aggregate per group 1..5: `WeaponCount++`, `Ready01 = min(Ready01, 1 - remaining/period)`, `Firing |= firing`. Groups above 5 are ignored. Cost: O(weapon entities in the world) in Burst; no main-thread loop.
4. Abilities: copy up to 4 entries of the SPEC-24 slot buffer (§10 Q3).
5. Target: if the ship's target entity (§10 Q4) exists: relation via §10 Q7, hull and shield fractions from its `ShipStats`/`ShieldState`, distance between `LocalToWorld` positions, name hash from its blueprint hash; else clear `HasTarget`.
6. Interaction: copy the SPEC-31 interaction singleton (§10 Q5).
7. Current star system: copy `StarSystemIdValue` (§10 Q6).
8. Radar, only when `elapsedUnscaled - lastRadar ≥ 0.1 s` (10 Hz): schedule `HudRadarGatherJob` (`IJobChunk`, `Schedule`, Burst) over grid roots (`VoxelGrid` + `Allegiance` + `LocalToWorld`, `WithNone<PlayerShip>`) and bodies (§10 Q8): compute `offset = (pos - playerPos).xz`; keep the 64 nearest using a fixed-size max-heap in the job (`NativeArray` of 64, `Allocator.TempJob`); entries beyond `RadarRangeM` (2,000 m) are clamped to the range circle with flag 2. Then a second single-threaded Burst job writes the buffer sorted by distance (stable tie-break by `Entity.Index`). `RadarVersion++`.
9. `FrameIndex = (uint)frameCount`. Dependencies are left on `state.Dependency`; `HudSyncSystem` completes them.

### 5.2 `HudSyncSystem`

- Managed `SystemBase` listed in the architecture pipeline (`PresentationBridgeGroup`), `[UpdateAfter(typeof(HudSnapshotSystem))]`, `[UpdateBefore(typeof(ManagedEventDrainSystem))]`, `RequireForUpdate<HudSnapshot>()` and `RequireForUpdate<GameplayActive>()`.
- `public void Bind(HudViewModel vm, IHudNames names, Func<string, string> bindingDisplay)` called once by `Game.App`; unbound → return. `IHudNames` (Presentation) resolves `ContentHash` → display name, body key → body name, star system id → name; all names are cached strings (no allocation per lookup).
- `public event Action<bool> PlayerHulkStateChanged` (instance event).

Per frame (marker `HudSyncSystem.Apply`):
1. `CompleteDependency()`; read `HudSnapshot` and the blip buffer once (one `GetSingleton`, one `GetSingletonBuffer`, `AsNativeArray`).
2. Compare with the previous snapshot copy stored in the system (`HudSnapshot last`). Write only changed fields, with quantisation: fractions rounded to 1/500 before comparing, ints exact, `SpeedMps` → `(int)round`, distance → `(int)round` metres, cooldowns and ready fractions rounded to 1/100.
3. Strings: `TargetName`, `InteractionTargetName`, `StarSystemName` are assigned only when their key changed (lookup returns a cached instance). `InteractionVerbText` from a fixed table of cached strings indexed by verb.
4. Weapon groups and abilities: write the 5 / 4 fixed item view models in place.
5. Radar: if `RadarVersion` changed, copy blips into a reusable `HudRadarBlip[64]` array owned by the system and set `vm.RadarVersion` (the view reads the array through `IHudRadarSource`, implemented by the system, §6); set `vm.RadarYawDeg` from `MainCameraState` yaw when it changed by ≥ 0.5°.
6. When `IsHulk` changes, raise `PlayerHulkStateChanged(isHulk)`.
7. No allocation in steps 1–7 (gate §8).

### 5.3 `FlightHudView` per-frame update path

`FlightHudView` subscribes to `HudViewModel.propertyChanged` in `OnBuild`. In the handler it switches on the args' property name (compared by reference with the static args instances) and updates elements directly: `Meter.SetValue`, `Slot.SetCooldown`, `Label.text = NumberStringCache.Get(...)`, class toggles. Strings that change rarely (names, objective) use UXML `DataBinding`. Radar: on `RadarVersion` change, the view positions its 64 pooled blip elements (`style.translate` in px = offset / range × 120, rotated by `-RadarYawDeg`), sets kind/relation classes (`.marker--ally/--enemy/--neutral` + `.hud-blip--ship/--station/--body/--poi`, `.hud-blip--beyond` for flag 2), and hides unused ones with `.is-hidden`. No element is created after `OnBuild`.

### 5.4 Presenters

All presenters are plain classes constructed by `Game.App` (`UiComposition`, HUD-002) with their models, view model and services, implementing the screen's command interface. They subscribe in a `Bind()` method and unsubscribe in `Unbind()`; `SessionReset` → `Unbind(); Bind(newModels)` and refresh the view model in place.

| Presenter | Inputs (owning spec) | Refresh trigger | Command behaviour |
|---|---|---|---|
| `MainMenuPresenter` | `SaveService` slots (SPEC-05), `SceneFlow`, `GameSession.NewGame` (SPEC-01) | screen `OnPush` | `NewGame` → `TextPrompt` (title "New game", label "Commander name", max 24, validator: 1–24 chars of letters, digits, space, `-`, `'`) → `GameSession.NewGame(name)` → `SceneFlow` to World. `Continue` → load most recent slot. `OpenLab` → SPEC-51 launcher (creative). `Quit` → confirm → `Application.Quit()` through SPEC-01 |
| `SaveSlotsPresenter` | `SaveService` (SPEC-05) | `OnPush(SaveSlotsMode)`, `GameSaved` | `LoadSelected` (in game) → confirm "Unsaved progress since the last save will be lost." [Load (danger) / Cancel]. `SaveToSelected` on an existing slot → confirm overwrite. `SaveNew` → `TextPrompt` (max 32). `DeleteSelected` → confirm [Delete (danger) / Cancel] (06-B12). Errors → `Notification { Kind = Danger }` and a `ConfirmDialog` with the message |
| `SettingsPresenter` | `SettingsService` (SPEC-01), `InputService` rebinding (SPEC-03) | `OnPush`, settings changed | Each `Set*` writes the setting immediately (persisted by `SettingsService`). `StartRebind(row)` → SPEC-03 rebind with Esc cancel; row shows "Press a key…" |
| `PausePresenter` | `SaveService.CanSaveNow` (SPEC-05) | `OnPush` | `Save` → push `SaveSlotsView` (Save). `Load` → push `SaveSlotsView` (Load). `MainMenu` → confirm → `SceneFlow` to MainMenu. `Quit` → confirm → quit |
| `InventoryPresenter` | `InventoryModel`, `Wallet`, `ItemDefinition` (SPEC-50) | `SlotChanged`, `Reset`, wallet change | `Select`, `Move` (`TryMove`), `Sort`, `StackAll` |
| `MerchantPresenter` | merchant session API (SPEC-50) | open with merchant args, model events | `Buy`/`Sell` call SPEC-50; failures set `BuyBlockedReason`/`SellBlockedReason` |
| `ShipLogPresenter` | quest log, clues, rumors (SPEC-42) | `FactChanged`, quest events | `ToggleTrack` → SPEC-42; `ShowRumorOnMap` → push `GalaxyMapView` with the rumor's star system selected |
| `GalaxyMapPresenter` | galaxy definition, known/visited facts (SPEC-30/42), destination (SPEC-32) | `OnPush`, `StarSystemEntered`, `FactChanged` | `SetDestination` → SPEC-32 |
| `StarSystemMapPresenter` | bodies and orbits at current `UniversalTime` (SPEC-31), player pose | `OnPush` | `SetWaypoint` → SPEC-31/32 (hidden when unsupported, §10 Q14) |
| `FleetPresenter` | fleet in `GameSession` (SPEC-01), stats (SPEC-22 `BlockStatMath`) | `OnPush`, fleet change | `Edit` → SPEC-51 launcher (only when docked, §10 Q15) |
| `HulkPresenter` | `HudSyncSystem.PlayerHulkStateChanged` | event | on `true` push `HulkView`; `LoadLastSave` → confirm → load; `MainMenu` → confirm |
| `LoadingPresenter` | `SceneFlow` progress (SPEC-01) | SceneFlow events | shows/hides `LoadingView` |
| `HudObjectivePresenter` | tracked quest (SPEC-42) | quest events | writes `HasObjective`, `ObjectiveTitle`, `ObjectiveText` |
| `HudNotificationPresenter` | `ItemAcquired`, `GameSaved` (EventBus) | events | `ItemAcquired` → `Notification { Kind = Positive, Label = item name, Amount = count, CoalesceKey = cached "item:" + id }`; `GameSaved` → "Game saved" |

### 5.5 `ScreenHotkeyRouter` (Presentation, `Assets/Game/Presentation/Presenters/ScreenHotkeyRouter.cs`)

Actions (SPEC-03, §10 Q11): `OpenInventory` (I), `OpenShipLog` (L), `OpenGalaxyMap` (M), `OpenSystemMap` (N), `OpenFleet` (F) in **both** Flight and Menu maps. On `performed` of action → target screen T:
1. `top = navigator.Top`. If `top == T` → `navigator.Pop(T)`; return.
2. If `top` is `FlightHudView` → `navigator.Push(T)`; return.
3. If `top` is one of the five hotkey screens → `navigator.Pop(top); navigator.Push(T)`; return.
4. Otherwise (pause, settings, confirm, merchant, dialog, hulk, loading, editor) → ignore.

### 5.6 Screen layouts (wireframes; spacing in tokens, sizes as listed)

All full screens: a `.panel` centred, width `min(1600 px, 92 %)`, height `min(920 px, 90 %)`, header with title and `<key>-close` button in `.panel__header-actions`, footer with the primary actions. Content that can exceed the panel scrolls inside `.panel__body` (`ScrollView` or `ListView`). Action buttons always live in `.panel__header-actions` or `.panel__footer` (R31).

| Screen | Layout |
|---|---|
| `main-menu` | Left column 420 px at `--space-8` from the left edge: title (`.text-display .text-xxl`), buttons `main-menu-continue` (subtitle label below), `main-menu-new-game`, `main-menu-load`, `main-menu-lab`, `main-menu-settings`, `main-menu-quit` (`.btn--wide`, `.stack-m`); `main-menu-version` bottom-right `.text-muted .text-xs` |
| `save-slots` | Panel: `save-slots-list` (ListView, rows: title, location, timestamp, playtime, autosave tag) fills body; footer: `save-slots-delete` (danger, left), `save-slots-new` (Save mode), `save-slots-save` or `save-slots-load` (primary, right); `save-slots-empty` label when no slots |
| `settings` | Modal window 960 × 720 px max: `settings-tabs` (TabBar: Audio, Video, Controls, Gameplay, Accessibility → `settings-tab-audio` …); body ScrollView with one `.row` per setting: label left (`.grow`), control right (Slider/Toggle/DropdownField 280 px). Controls tab: `settings-rebind-list` (ListView rows: action name, binding text, `Rebind` button named `settings-rebind-<row>`), `settings-reset-bindings`. Footer: `settings-close` |
| `pause` | Modal window 420 px wide centred: title "Paused", buttons `pause-resume` (primary), `pause-save`, `pause-load`, `pause-settings`, `pause-main-menu`, `pause-quit` (`.stack-m`); `pause-save-blocked` label (muted) when `CanSave == false`; key hints read live bindings via `KeyHint` |
| `hud` | §4.4 |
| `inventory` | Panel 1100 × 760 px: header title `inventory-title`, `inventory-capacity` ("used / capacity"), header actions `inventory-sort`, `inventory-stack-all`, `inventory-close`; body: left ScrollView `inventory-grid` with 200 pooled `Slot`s named `inventory-slot-<index>` in wrapping rows of 10 (slot 72 px); right detail column 320 px (`inventory-detail-name`, `-description`, `-count`, `-value`); footer `inventory-wallet` |
| `merchant` | Panel full size: header `merchant-name`, `merchant-wallet`, `merchant-close` ("Leave"); `merchant-tabs` (TabBar, scrolls horizontally, R12); body two columns: `merchant-stock-list` and `merchant-cargo-list` (ListViews, rows: icon 40 px, name `.grow .text-ellipsis`, price, quantity); detail strip: `merchant-detail-name`, `-description`, `merchant-quantity` (SliderInt 1..MaxQuantity), `merchant-buy-total`, `merchant-sell-total`; footer `merchant-buy` (primary), `merchant-sell` |
| `dialog` | Comms panel in Z-L: left-anchored, width 640 px, top and bottom margins `--space-6`; header `dialog-speaker`; row: `dialog-portrait` (96 × 96 px, hidden when `HasPortrait == false`) and `dialog-text` (ScrollView with the line label; scroll offset reset to 0 when `LineVersion` changes); `dialog-options` ScrollView with 9 pooled buttons `dialog-option-1` … `dialog-option-9` showing "[n] text"; `dialog-continue`. Panel `max-height: 100 %` of the safe area; options scroll, never leave the screen (R11) |
| `ship-log` | Panel: `ship-log-tabs` (Quests, Clues, Rumors); Quests: `ship-log-quest-list` (ListView 420 px) + detail (`ship-log-quest-title`, `-stage`, `-description`, `ship-log-objectives` list with done class); Clues: `ship-log-clue-list` grouped by mystery title + `ship-log-clue-body`; Rumors: `ship-log-rumor-list` rows with `Show on map` buttons `ship-log-rumor-map-<index>`; header `ship-log-close` |
| `galaxy-map` | Full panel: `galaxy-map-canvas` (`MapCanvas`, `.grow`) and right column 360 px: `galaxy-map-selected-name`, `galaxy-map-selected-info`, `galaxy-map-set-destination` (primary, disabled with reason), `galaxy-map-focus-current`, `galaxy-map-zoom-in`, `galaxy-map-zoom-out`, legend; header `galaxy-map-close` |
| `system-map` | Same layout with `system-map-*` names and `system-map-set-waypoint` |
| `fleet` | Panel: left `fleet-ship-list` (ListView 420 px; rows: name `.text-ellipsis`, hull Meter, active tag); right: `fleet-detail-name`, `fleet-detail-class`, `fleet-stats` (ListView of `StatRowViewModel`, two columns label/value, row height fixed), footer `fleet-edit`, `fleet-set-active`; header `fleet-close` |
| `ship-editor` | Root host: regions `ship-editor-region-top` (full width, 64 px), `ship-editor-region-left` (360 px), `ship-editor-region-right` (380 px), `ship-editor-region-bottom` (full width, 48 px), all `.hud-region .hud-plate`, center empty (world shows through, `picking-mode Ignore`). Panels mounted by SPEC-51 |
| `hulk` | Modal window 520 px: `hulk-title`, `hulk-body`, `hulk-load-last` (primary, hidden when no save), `hulk-main-menu` |
| `loading` | Full-screen scrim `--color-bg`; centred `loading-status` and `loading-progress` (Meter variant neutral, 480 px) |

### 5.7 `MapCanvas` control (`Assets/Game/UI/Controls/MapCanvas.cs`)

`[UxmlElement] public partial class MapCanvas : VisualElement` with one child `content` (absolute, `picking-mode Ignore`) that draws through `generateVisualContent` with `Painter2D`.
1. `SetData(MapData data, int version)`: when `version` changed, mark content dirty.
2. `View` (`MapViewTransform`) is owned by the canvas; `ViewSize` updated on `GeometryChangedEvent`.
3. Draw (only when dirty): lines first (`Painter2D` stroke 2 px; gated lanes dashed 6/4 px; unknown lanes skipped), rings (polyline of 96 segments), points (filled circles `RadiusPx`), markers (player triangle rotated by heading). Colours from `--maps-color-<i>`, `--color-accent` (selected), `--color-text-muted` (unknown). Geometry is generated in view space at `tessellationUnitsPerPixel`.
4. Pan (pointer drag ≥ 4 px with left button, or Menu-map pan actions): update `View.Pan`, then set `content.style.translate` by the accumulated pixel delta since the last tessellation; no redraw.
5. Zoom (wheel over the canvas or zoom buttons): `View.ZoomAt(cursor, steps)`; if `|log(View.UnitsPerPixel / tessellationUnitsPerPixel)| > log(1.1)` → mark dirty (re-tessellate, reset translate); otherwise apply `content.style.scale` around the cursor.
6. Click (pointer up with drag < 4 px): nearest point within 12 px in view space → raise `PointClicked(int index)` (the view calls the command). Linear scan over points (≤ 1,024) only on click.
7. Labels: names drawn as up to 64 pooled `Label` children for points with flags Current, Destination, Selected or Visited within view, positioned with `style.translate`; hidden otherwise.

## 6. Public API

```csharp
namespace Game.UI.Views
{
    /// One file per screen in Assets/Game/UI/Views/. Constructor stores dependencies; OnBuild wires elements.
    public sealed class FlightHudView : UIScreen
    { public FlightHudView(HudViewModel vm, IHudRadarSource radar, System.Func<string, string> bindingDisplay); }
    public interface IHudRadarSource { System.ReadOnlySpan<HudRadarBlipView> Blips { get; } }       // Game.UI
    public struct HudRadarBlipView { public UnityEngine.Vector2 OffsetXZ; public byte Kind; public byte Relation; public byte Flags; }
    // All other screens: public <Name>View(<Name>ViewModel vm, I<Name>Commands commands) — LoadingView(LoadingViewModel vm),
    // ShipEditorView(ShipEditorViewModel vm, IShipEditorCommands commands) — see §4.1.
}

namespace Game.App.Presenters
{
    /// Builds every view model, view and presenter once and registers the views. Called by GameRoot (SPEC-01) after step 9.
    public sealed class UiComposition : System.IDisposable
    {
        public UiComposition(Game.UI.Services.UIRoot ui /* + services, see HUD-002 */);
        public MainMenuView MainMenu { get; }       // root screens for SceneFlow.SetRoot
        public FlightHudView FlightHud { get; }
        public ShipEditorView ShipEditor { get; }
        public LoadingView Loading { get; }
        public void Dispose();
    }
}
```

`HudRadarBlipView` is the `Game.UI` copy of a blip (the UI module cannot see Presentation components); `HudSyncSystem` converts `HudRadarBlip` → `HudRadarBlipView` into its reusable array.

## 7. Edge cases and failure behaviour

| Situation | Required behaviour |
|---|---|
| No player ship (intro, after despawn) | HUD shows only location/objective zones; ship zones get `.is-hidden` via `HasShip == false` |
| Player ship becomes a hulk | `HulkView` pushed once; HUD meters stay at their last values |
| Two `PlayerShip` entities | Snapshot uses the one `TryGetSingletonEntity` fails on → treat as none and `GameLog.WarnOnce` |
| More than 5 weapon groups | Groups 6+ ignored by the HUD |
| More than 64 radar contacts | Nearest 64 shown (stable by distance, then entity index) |
| Contact beyond 2,000 m | Clamped to the radar edge with the "beyond" style |
| Target destroyed between frames | `HasTarget` false next frame; no exception |
| Name lookup misses | Shows the content id (cached) and logs `WarnOnce` |
| Inventory capacity above 200 | Slots 200+ not shown; `GameLog.Error` at bind time ("raise pool") |
| Merchant with 10 categories at 4:3 | Tabs scroll horizontally inside the panel (R12) |
| Dialog line longer than the panel | Text scrolls; options remain reachable (R11) |
| Dialog with 9 options at 1280×720 logical | Options ScrollView shows all via scrolling; none outside the viewport |
| Delete save clicked | Confirm required; Cancel/Esc keeps the slot (06-B12) |
| Save fails (disk full) | Danger toast + confirm dialog with the error text |
| Load chosen while in game | Confirm; on accept `SceneFlow` load (SPEC-01/05 own the flow) |
| Hotkey while a modal is open | Ignored (§5.5 step 4) |
| Hotkey while typing | No effect (context `TextEntry` disables Flight and Menu maps) |
| Leaving the World scene with screens open | `SetRoot` of the next scene pops every screen (no stale pause, R13/B7) |
| `SessionReset` | Presenters rebind; view models updated in place; notifications cleared |

## 8. Performance budget

| Gate (06) | Target | Measured by |
|---|---|---|
| Idle flight / heavy combat GC | 0 B/frame from `HudSnapshotSystem`, `HudSyncSystem`, `FlightHudView` | `HudPerformanceTests.CombatValuesEveryFrame_ZeroGc` (values change every frame for 600 frames) |
| `HudSnapshotSystem` | ≤ 0.10 ms main thread (scheduling) + ≤ 0.20 ms job time with 30 ships × 20 weapons, 64 radar blips | `HudPerformanceTests.Snapshot_30Ships` |
| `HudSyncSystem` + view update | ≤ 0.15 ms main thread when every value changes | `HudPerformanceTests.Sync_AllChanged` |
| Menus | ≤ 4 ms main thread, ≤ 1 KB/frame GC with any screen open and idle | `ScreensPerformanceTests.EachScreenIdle` |
| Galaxy map | 1,024 stars + 4,096 lanes: pan frame ≤ 1 ms; re-tessellation ≤ 6 ms | `MapCanvasPerformanceTests` |

Profiler markers: `HudSnapshotSystem.Ship`, `HudSnapshotSystem.Weapons`, `HudSnapshotSystem.Radar`, `HudSyncSystem.Apply`, `FlightHudView.Radar`, `MapCanvas.Tessellate`. Scale: no main-thread loop over entities (weapons and radar are Burst jobs); HUD element count is constant (pools); lists are virtualised.

## 9. Tasks

### HUD-001 — Public contract types
- **Milestone:** M3
- **Tier:** standard — transcribes §4.2, §4.3, §4.5 and §6 with the stated generation rule
- **Depends on:** `UI-001`, `CORE-001`, `CAM-001`
- **Do:** Create every view model and item view model of §4.2 (rule in §4.2), `MapData` and its structs, `SaveSlotsMode`, `SettingKey`, every command interface of §4.3, `IHudRadarSource`, `HudRadarBlipView`, the snapshot types of §4.5 (Presentation), view class shells for every screen in §4.1 (constructors per §6, `Key`/`Layer`/`Context`/flags exactly as §4.1, `OnBuild` empty), and the `UiComposition` shell.
- **Files:** `Assets/Game/UI/ViewModels/*.cs` (one file per view model), `Assets/Game/UI/Data/{MapData,MapPoint,MapPointFlags,MapLine,MapRing,MapMarker,MapMarkerKind}.cs`, `Assets/Game/UI/Views/Commands/*.cs`, `Assets/Game/UI/Views/{MainMenuView,SaveSlotsView,SettingsView,PauseView,FlightHudView,InventoryView,MerchantView,DialogView,ShipLogView,GalaxyMapView,StarSystemMapView,FleetView,ShipEditorView,HulkView,LoadingView,IHudRadarSource,HudRadarBlipView}.cs`, `Assets/Game/Presentation/Components/{HudSnapshot,HudFlags,HudWeaponGroupState,HudAbilityState,HudRadarBlip}.cs`, `Assets/Game/App/Presenters/UiComposition.cs`
- **Acceptance criteria:**
  - [ ] Compiles; every screen's flags equal §4.1 (checked by test).
  - [ ] `HudSnapshot`, `HudRadarBlip` are unmanaged; `HudWeaponGroupState` is 8 bytes, `HudAbilityState` 16 bytes.
- **Tests:** `Assets/Game/Tests/EditMode/UI/ScreenTableTests.cs`: `EveryScreen_FlagsMatchSpecTable` (table literal in the test). `Assets/Game/Tests/EditMode/Presentation/HudSnapshotLayoutTests.cs`: `Sizes_AsSpecified`, `Unmanaged`.

### HUD-002 — UI composition, screen registration and root switching
- **Milestone:** M3
- **Tier:** frontier — F5: boot order, scene flow root switching, session rebind across App, UI and Presentation
- **Depends on:** `HUD-001`, `SPEC-60`, `FND-001`
- **Do:** Implement `UiComposition`: create all view models, views (registered on `UIRoot`), and presenters with their services (§5.4); register screen UXML entries in `UIConfig` (`Prewarm` true for `hud`, `pause`, `loading`, `confirm`, `text-prompt`); bind `HudSyncSystem` and the hotkey router; `SessionReset` handling. Provide the three root screens to `SceneFlow` (SPEC-01 calls `SetRoot`, §10 Q12).
- **Files:** `Assets/Game/App/Presenters/UiComposition.cs`, `Assets/Game/UI/Data/UIConfig.asset`
- **Acceptance criteria:**
  - [ ] After boot into each scene exactly one root screen is in the stack and visible (R13).
  - [ ] Scene change MainMenu → World → BlueprintLab → World leaves pause count 0 and the context of the new root.
  - [ ] `SessionReset` keeps every view model instance (reference equality) and refreshes values.
- **Tests:** `Assets/Game/Tests/PlayMode/UI/UiCompositionTests.cs`: `BootEachScene_OneRootVisible`, `SceneRoundTrip_NoPauseLeak_ContextMatchesRoot`, `SessionReset_ViewModelsKept`, `EditorRoot_FlightHudHidden` (R13).

### HUD-003 — Main menu and new game
- **Milestone:** M3
- **Tier:** standard — view on an existing view model; presenter steps in §5.4
- **Depends on:** `HUD-002`
- **Do:** `MainMenuView` (UXML + `screens/main-menu.uss`), `MainMenuPresenter` (§5.4).
- **Files:** `Assets/Game/UI/Views/MainMenuView.cs`, `Assets/Game/UI/Views/Uxml/MainMenuView.uxml`, `Assets/Game/UI/Theme/screens/main-menu.uss`, `Assets/Game/App/Presenters/MainMenuPresenter.cs`
- **Acceptance criteria:**
  - [ ] Continue hidden when no save exists.
  - [ ] New game name prompt is readable (contrast audit passes, R4) and rejects empty or invalid names.
  - [ ] Layout audit clean at all five logical sizes.
- **Tests:** `Assets/Game/Tests/PlayMode/UI/Screens/MainMenuViewTests.cs`: `NoSave_ContinueHidden`, `NewGame_EmptyName_OkDisabled`, `NewGame_ValidName_CallsNewGame`, `Layout_AllLogicalSizes_NoIssues`, `Contrast_NameField_Passes`.

### HUD-004 — Save and load slots
- **Milestone:** M3
- **Tier:** standard — view on an existing view model; presenter steps in §5.4
- **Depends on:** `HUD-002`, `PERS-001`
- **Do:** `SaveSlotsView` (ListView with `makeItem`/`bindItem`), `SaveSlotsPresenter` with confirmations.
- **Files:** `Assets/Game/UI/Views/SaveSlotsView.cs`, `Assets/Game/UI/Views/Uxml/{SaveSlotsView,SaveSlotItem}.uxml`, `Assets/Game/UI/Theme/screens/save-slots.uss`, `Assets/Game/App/Presenters/SaveSlotsPresenter.cs`
- **Acceptance criteria:**
  - [ ] Delete requires confirmation; Esc on the confirm keeps the slot (06-B12).
  - [ ] Overwrite and in-game load require confirmation.
  - [ ] 64 slots scroll; layout audit clean.
- **Tests:** `Assets/Game/Tests/PlayMode/UI/Screens/SaveSlotsViewTests.cs`: `Delete_RequiresConfirm`, `Delete_EscKeepsSlot`, `Overwrite_RequiresConfirm`, `SaveError_ShowsDangerToast`, `Layout_64Slots_AllLogicalSizes_NoIssues`.

### HUD-005 — Settings screen
- **Milestone:** M3
- **Tier:** standard — view on an existing view model; rebinding through SPEC-03's API
- **Depends on:** `HUD-002`
- **Do:** `SettingsView` with five tabs, `SettingsPresenter`; accessibility settings call `UIRoot.ApplyAccessibility`; camera settings forwarded to SPEC-62 by SPEC-01's settings hookup.
- **Files:** `Assets/Game/UI/Views/SettingsView.cs`, `Assets/Game/UI/Views/Uxml/{SettingsView,RebindRow}.uxml`, `Assets/Game/UI/Theme/screens/settings.uss`, `Assets/Game/App/Presenters/SettingsPresenter.cs`
- **Acceptance criteria:**
  - [ ] Changing a slider persists through `SettingsService` immediately.
  - [ ] Rebind: row shows "Press a key…", Esc cancels without closing the screen, success shows the new binding; no exception (06-D2).
  - [ ] Only actions from Global, Flight, Editor, Dialog maps are listed (no UI pointer actions, no composite parts).
- **Tests:** `Assets/Game/Tests/PlayMode/UI/Screens/SettingsViewTests.cs`: `Slider_PersistsImmediately`, `Rebind_EscCancels_ScreenStays`, `Rebind_Success_UpdatesRow`, `RebindList_ExcludesPointerAndComposites`, `Layout_AllTabs_AllLogicalSizes_NoIssues`.

### HUD-006 — Pause screen
- **Milestone:** M3
- **Tier:** standard — view on an existing view model
- **Depends on:** `HUD-002`
- **Do:** `PauseView`, `PausePresenter` (§5.4).
- **Files:** `Assets/Game/UI/Views/PauseView.cs`, `Assets/Game/UI/Views/Uxml/PauseView.uxml`, `Assets/Game/UI/Theme/screens/pause.uss`, `Assets/Game/App/Presenters/PausePresenter.cs`
- **Acceptance criteria:**
  - [ ] Esc in flight opens Pause; Esc again resumes (pause count back to 0, context Flight).
  - [ ] Save disabled with the reason when `CanSaveNow` is false.
  - [ ] Fits at 1280×720 logical with text scale 130 % (E7).
- **Tests:** `Assets/Game/Tests/PlayMode/UI/Screens/PauseViewTests.cs`: `EscTwice_ResumesAndRestoresFlight`, `SaveBlocked_ShowsReason`, `MainMenu_RequiresConfirm`, `Layout_TextScale130_1280x720_NoIssues`.

### HUD-007 — Flight HUD view and layout
- **Milestone:** M3
- **Tier:** standard — layout and direct-update path fully given in §4.4 and §5.3
- **Depends on:** `HUD-002`
- **Do:** `FlightHudView` UXML with the zones of §4.4, `screens/hud.uss`, pooled 5 weapon slots, 4 ability slots, 64 radar blip elements, direct-update handler (§5.3), `.hud-narrow` switch.
- **Files:** `Assets/Game/UI/Views/FlightHudView.cs`, `Assets/Game/UI/Views/Uxml/FlightHudView.uxml`, `Assets/Game/UI/Theme/screens/hud.uss`
- **Acceptance criteria:**
  - [ ] With maximum-length content (names 40 chars, values 9,999) no two `.hud-region`s overlap and nothing leaves the viewport at all five logical sizes (R7).
  - [ ] All HUD text passes `ContrastAudit` against `.hud-plate`.
  - [ ] Changing every view model value 600 times allocates 0 B in the view handler.
- **Tests:** `Assets/Game/Tests/PlayMode/UI/Screens/FlightHudViewTests.cs`: `Layout_MaxContent_AllLogicalSizes_NoOverlap`, `Contrast_AllHudText_Passes`, `DirectUpdate_600Changes_ZeroAlloc`, `NoShip_ShipZonesHidden`, `Radar_64Blips_PooledNoNewElements`.

### HUD-008 — HudSnapshotSystem
- **Milestone:** M3
- **Tier:** frontier — F2/F4: Burst jobs over world entities feeding a 0 B/frame hot path
- **Depends on:** `HUD-001`, `PWR-001`, `WPN-001`, `ABL-001`, `SYS-001`, `GAL-001`, `FLT-001`, `VOX-001`, `TST-001`
- **Do:** Implement §5.1 with `HudWeaponGatherJob`, `HudRadarGatherJob` and the radar sort job. Profiler markers per §8.
- **Files:** `Assets/Game/Presentation/Systems/HudSnapshotSystem.cs`, `Assets/Game/Presentation/Jobs/{HudWeaponGatherJob,HudRadarGatherJob,HudRadarSortJob}.cs`
- **Acceptance criteria:**
  - [ ] Snapshot equals hand-computed values in a test world (ship stats, 7 weapons in 3 groups, 2 abilities, target, interaction).
  - [ ] Radar: 100 contacts → nearest 64 sorted by distance; contacts beyond 2,000 m flagged and clamped.
  - [ ] Radar refreshes at 10 Hz (`RadarVersion` increments 10 ± 1 times per simulated second at 144 FPS).
  - [ ] No main-thread iteration over entities.
- **Tests:** `Assets/Game/Tests/EditMode/Presentation/HudSnapshotSystemTests.cs` (`EcsTestWorld`): `NoPlayer_FlagsNone`, `ShipValues_Copied`, `WeaponGroups_Aggregated`, `GroupsAbove5_Ignored`, `Radar_Nearest64_Sorted`, `Radar_BeyondRange_Clamped`, `Radar_RefreshRate10Hz`, `Hulk_FlagSet`.

### HUD-009 — HudSyncSystem
- **Milestone:** M3
- **Tier:** frontier — F4: owns the 0 B/frame HUD gate
- **Depends on:** `HUD-008`, `HUD-007`
- **Do:** Implement §5.2 including `Bind`, quantisation, `IHudNames` cache built from `ContentDatabase` and SPEC-30/31 name providers, `IHudRadarSource` implementation, `PlayerHulkStateChanged`.
- **Files:** `Assets/Game/Presentation/Systems/HudSyncSystem.cs`, `Assets/Game/Presentation/Presenters/{IHudNames,HudNames}.cs`
- **Acceptance criteria:**
  - [ ] A view model property raises `propertyChanged` only when its quantised value changes.
  - [ ] 0 B GC per frame with all values changing every frame (600 frames).
  - [ ] `PlayerHulkStateChanged(true)` raised exactly once per hulk transition.
- **Tests:** `Assets/Game/Tests/EditMode/Presentation/HudSyncSystemTests.cs`: `Unchanged_NoNotifications`, `Quantisation_SmallChangeIgnored`, `TargetNameChange_SingleStringAssign`, `HulkTransition_EventOnce`, `AllChanging_ZeroAlloc`. `Assets/Game/Tests/PlayMode/UI/HudPerformanceTests.cs`: `CombatValuesEveryFrame_ZeroGc`, `Snapshot_30Ships`, `Sync_AllChanged`.

### HUD-010 — HUD presenters and screen hotkeys
- **Milestone:** M3
- **Tier:** standard — behaviour tables in §5.4 and §5.5
- **Depends on:** `HUD-009`, `QST-001`, `ECO-001`, `INP-001`
- **Do:** `HudObjectivePresenter`, `HudNotificationPresenter`, `ScreenHotkeyRouter`, `HulkPresenter`.
- **Files:** `Assets/Game/Presentation/Presenters/{HudObjectivePresenter,HudNotificationPresenter,ScreenHotkeyRouter}.cs`, `Assets/Game/App/Presenters/HulkPresenter.cs`
- **Acceptance criteria:**
  - [ ] 12 `ItemAcquired` events for the same item within 2.5 s → one toast "+12 <name>".
  - [ ] Hotkey I: HUD → Inventory; I again → closed; I then M → Inventory replaced by Galaxy map; I during Pause → ignored.
  - [ ] Tracked objective text updates on quest change only.
- **Tests:** `Assets/Game/Tests/EditMode/Presentation/ScreenHotkeyRouterTests.cs`: `FromHud_Opens`, `SameKey_Closes`, `OtherKey_Switches`, `ModalOpen_Ignored`. `Assets/Game/Tests/EditMode/Presentation/HudNotificationPresenterTests.cs`: `TwelveItems_OneToast`. `Assets/Game/Tests/EditMode/Presentation/HudObjectivePresenterTests.cs`: `QuestChange_UpdatesText`.

### HUD-011 — Inventory screen
- **Milestone:** M3
- **Tier:** standard — view on an existing view model with a fixed pool
- **Depends on:** `HUD-002`, `SPEC-50`
- **Do:** `InventoryView` (200 pooled `Slot`s, drag-move with a `PointerManipulator` that captures the pointer and drops on the slot under the pointer), `InventoryPresenter`.
- **Files:** `Assets/Game/UI/Views/InventoryView.cs`, `Assets/Game/UI/Views/Uxml/InventoryView.uxml`, `Assets/Game/UI/Theme/screens/inventory.uss`, `Assets/Game/Presentation/Presenters/InventoryPresenter.cs`
- **Acceptance criteria:**
  - [ ] Every slot within `Capacity` is reachable by scrolling; slot index `Capacity - 1` is displayed (06-D1).
  - [ ] Sort and Stack All sit inside the panel header (R31); no `OutsidePanel` issue.
  - [ ] Opening the screen changes nothing in the inventory (R12 side effect).
- **Tests:** `Assets/Game/Tests/PlayMode/UI/Screens/InventoryViewTests.cs`: `LastSlot_Displayed`, `HeaderButtons_InsidePanel`, `Open_NoModelChange`, `DragMove_CallsMove`, `Layout_AllLogicalSizes_NoIssues`.

### HUD-012 — Merchant screen
- **Milestone:** M3
- **Tier:** standard — view on an existing view model
- **Depends on:** `HUD-002`, `SPEC-50`
- **Do:** `MerchantView` (TabBar, two ListViews, quantity slider), `MerchantPresenter`.
- **Files:** `Assets/Game/UI/Views/MerchantView.cs`, `Assets/Game/UI/Views/Uxml/{MerchantView,MerchantRow}.uxml`, `Assets/Game/UI/Theme/screens/merchant.uss`, `Assets/Game/Presentation/Presenters/MerchantPresenter.cs`
- **Acceptance criteria:**
  - [ ] Ten categories at logical 1663×1247 stay inside the panel (R12).
  - [ ] Buy of quantity 1 from a stack of 5 leaves 4 in stock (06-A13 via SPEC-50).
  - [ ] Failed buy shows the reason and changes no wallet value.
- **Tests:** `Assets/Game/Tests/PlayMode/UI/Screens/MerchantViewTests.cs`: `TenTabs_4x3_NoOverflow`, `BuyOne_StockDecrementsByOne`, `BuyFails_ReasonShown_WalletUnchanged`, `Layout_AllLogicalSizes_NoIssues`.

### HUD-013 — Dialog host screen
- **Milestone:** M3
- **Tier:** standard — view on an existing view model; SPEC-41 drives it
- **Depends on:** `HUD-002`
- **Do:** `DialogView` per §5.6 with 9 pooled option buttons calling `IDialogCommands.Choose(n)`, text scroll reset on `LineVersion` change.
- **Files:** `Assets/Game/UI/Views/DialogView.cs`, `Assets/Game/UI/Views/Uxml/DialogView.uxml`, `Assets/Game/UI/Theme/screens/dialog.uss`
- **Acceptance criteria:**
  - [ ] 9 options plus a 1,500-character line at 1280×720 logical: every option reachable, nothing outside the viewport (R11).
  - [ ] HUD hidden while the dialog is open.
  - [ ] Portrait never overlaps the text (separate flex column).
- **Tests:** `Assets/Game/Tests/PlayMode/UI/Screens/DialogViewTests.cs`: `NineOptionsLongLine_1280x720_NoIssues`, `NewLine_ScrollResetToTop`, `HudHiddenWhileOpen`, `Portrait_DoesNotOverlapText`, `OptionClick_CallsChoose`.

### HUD-014 — Ship log screen
- **Milestone:** M3
- **Tier:** standard — view on an existing view model
- **Depends on:** `HUD-002`, `SPEC-42`
- **Do:** `ShipLogView` (three tabs), `ShipLogPresenter`.
- **Files:** `Assets/Game/UI/Views/ShipLogView.cs`, `Assets/Game/UI/Views/Uxml/{ShipLogView,QuestRow,ClueRow,RumorRow}.uxml`, `Assets/Game/UI/Theme/screens/ship-log.uss`, `Assets/Game/Presentation/Presenters/ShipLogPresenter.cs`
- **Acceptance criteria:**
  - [ ] Selecting a quest shows its objectives with done states.
  - [ ] "Show on map" opens the galaxy map with the rumor's star system selected.
  - [ ] Layout audit clean with 64 quests and 256 clues.
- **Tests:** `Assets/Game/Tests/PlayMode/UI/Screens/ShipLogViewTests.cs`: `SelectQuest_ShowsObjectives`, `RumorShowOnMap_OpensGalaxyMapSelected`, `Layout_ManyItems_AllLogicalSizes_NoIssues`.

### HUD-015 — MapCanvas control
- **Milestone:** M3
- **Tier:** frontier — F4: owns the map frame budget (tessellation caching and translate-only panning)
- **Depends on:** `HUD-001`, `SPEC-62`
- **Do:** Implement `MapCanvas` per §5.7 and `screens/maps.uss` with `--maps-color-0..7`.
- **Files:** `Assets/Game/UI/Controls/MapCanvas.cs`, `Assets/Game/UI/Theme/screens/maps.uss`
- **Acceptance criteria:**
  - [ ] Panning never re-tessellates; zoom re-tessellates only beyond ±10 %.
  - [ ] Click within 12 px selects the nearest point; drag ≥ 4 px never selects.
  - [ ] Performance gates of §8 for maps pass.
- **Tests:** `Assets/Game/Tests/PlayMode/UI/MapCanvasTests.cs`: `Pan_NoTessellation`, `SmallZoom_NoTessellation`, `LargeZoom_Tessellates`, `Click_SelectsNearestWithin12px`, `Drag_DoesNotSelect`. `Assets/Game/Tests/PlayMode/UI/MapCanvasPerformanceTests.cs`: `Pan_1024Stars_Under1ms`, `Tessellate_Under6ms`.

### HUD-016 — Galaxy map and star-system map screens
- **Milestone:** M3
- **Tier:** standard — views on existing view models using `MapCanvas`
- **Depends on:** `HUD-015`, `SPEC-30`, `SPEC-31`
- **Do:** `GalaxyMapView`, `StarSystemMapView`, `GalaxyMapPresenter`, `StarSystemMapPresenter` (system map uses XZ of `double3` star-system positions as `double2`).
- **Files:** `Assets/Game/UI/Views/{GalaxyMapView,StarSystemMapView}.cs`, `Assets/Game/UI/Views/Uxml/{GalaxyMapView,StarSystemMapView}.uxml`, `Assets/Game/Presentation/Presenters/{GalaxyMapPresenter,StarSystemMapPresenter}.cs`
- **Acceptance criteria:**
  - [ ] Opening centres on the current star system (galaxy) or the player (system).
  - [ ] Set destination disabled with a reason for unknown or gated targets.
  - [ ] Layout audit clean at all logical sizes.
- **Tests:** `Assets/Game/Tests/PlayMode/UI/Screens/MapViewsTests.cs`: `GalaxyOpen_CentredOnCurrent`, `SystemOpen_CentredOnPlayer`, `GatedTarget_DestinationDisabledWithReason`, `Layout_AllLogicalSizes_NoIssues`.

### HUD-017 — Fleet screen
- **Milestone:** M3
- **Tier:** standard — view on an existing view model
- **Depends on:** `HUD-002`, `EDT-001`
- **Do:** `FleetView`, `FleetPresenter` (stats rows from SPEC-22 `BlockStatMath`, §10 Q15 for edit).
- **Files:** `Assets/Game/UI/Views/FleetView.cs`, `Assets/Game/UI/Views/Uxml/{FleetView,FleetShipRow,StatRow}.uxml`, `Assets/Game/UI/Theme/screens/fleet.uss`, `Assets/Game/App/Presenters/FleetPresenter.cs`
- **Acceptance criteria:**
  - [ ] Ship name shows the display name, never a raw content id; long names ellipsize (R31).
  - [ ] Stat rows never overlap headers (fixed row height, no absolute positioning) (R31).
  - [ ] Edit disabled with a reason when not docked.
- **Tests:** `Assets/Game/Tests/PlayMode/UI/Screens/FleetViewTests.cs`: `Name_DisplayNameEllipsized`, `Layout_StatsNoOverlap_AllLogicalSizes`, `NotDocked_EditDisabledWithReason`.

### HUD-018 — Ship editor host screen
- **Milestone:** M3
- **Tier:** standard — host layout given in §5.6; panel contents are SPEC-51's
- **Depends on:** `HUD-002`, `EDT-001`
- **Do:** `ShipEditorView` with four regions; `OnBuild` calls `ShipEditorPanels.Mount(regions, vm, commands)` (SPEC-51); `OnBack` forwards to `commands.OnBackPressed()`.
- **Files:** `Assets/Game/UI/Views/ShipEditorView.cs`, `Assets/Game/UI/Views/Uxml/ShipEditorView.uxml`, `Assets/Game/UI/Theme/screens/ship-editor.uss`
- **Acceptance criteria:**
  - [ ] Root install via `SetRoot` hides `FlightHudView` and shows only the editor root (R13).
  - [ ] Centre area is not pickable (world picking works between panels).
  - [ ] Regions do not overlap at all logical sizes.
- **Tests:** `Assets/Game/Tests/PlayMode/UI/Screens/ShipEditorViewTests.cs`: `SetRoot_OnlyEditorRootVisible`, `Centre_NotPickable`, `Regions_NoOverlap_AllLogicalSizes`, `Back_ForwardsToCommands`.

### HUD-019 — Hulk and loading screens
- **Milestone:** M3
- **Tier:** standard — views on existing view models
- **Depends on:** `HUD-010`
- **Do:** `HulkView`, `LoadingView`, `LoadingPresenter` (§5.4).
- **Files:** `Assets/Game/UI/Views/{HulkView,LoadingView}.cs`, `Assets/Game/UI/Views/Uxml/{HulkView,LoadingView}.uxml`, `Assets/Game/UI/Theme/screens/{hulk,loading}.uss`, `Assets/Game/App/Presenters/LoadingPresenter.cs`
- **Acceptance criteria:**
  - [ ] Esc cannot dismiss either screen.
  - [ ] Load-last hidden when no save exists.
  - [ ] Loading view blocks input (context `None`) and hides the HUD.
- **Tests:** `Assets/Game/Tests/PlayMode/UI/Screens/HulkLoadingViewTests.cs`: `Hulk_EscIgnored`, `Hulk_NoSave_LoadHidden`, `Loading_ContextNone_HudHidden`.

### HUD-020 — Full-screen layout regression suite
- **Milestone:** M3
- **Tier:** standard — runs already-specified audits over every screen
- **Depends on:** `HUD-003`, `HUD-004`, `HUD-005`, `HUD-006`, `HUD-007`, `HUD-011`, `HUD-012`, `HUD-013`, `HUD-014`, `HUD-016`, `HUD-017`, `HUD-018`, `HUD-019`
- **Do:** One parameterised PlayMode test over every screen of §4.1 × the five logical sizes × text scale 100 %/130 %, filling each view model with worst-case content (40-character names, 9,999 values, full lists), running `UILayoutAudit` and `ContrastAudit`, and saving a PNG of each failing case to `Temp/ui-layout/`.
- **Files:** `Assets/Game/Tests/PlayMode/UI/Screens/AllScreensLayoutTests.cs`, `Assets/Game/Tests/PlayMode/UI/Screens/WorstCaseViewModels.cs`
- **Acceptance criteria:**
  - [ ] Zero layout and contrast issues for every combination (R4, R7, R11, R12, R31).
  - [ ] Runs in under 60 s.
- **Tests:** `AllScreensLayoutTests.EveryScreen_EverySize_NoLayoutIssues`, `AllScreensLayoutTests.EveryScreen_NoContrastIssues`.

## 10. Open questions

| # | Question | Default if unanswered | Decider |
|---|---|---|---|
| 1 | SPEC-22: hull, shield, energy fields | `ShipStats.CellCount`, `ShipStats.CellCountMax` (cells at spawn/commit) for hull; `ShieldState.Hp`, `ShipStats.ShieldCapacity`, `ShieldState.Status` (byte: 0 none, 1 online, 2 recharging, 3 offline); `ShipPower.Stored`, `ShipStats.PowerStorage` | Tech lead (SPEC-22) |
| 2 | SPEC-21: weapon state readable per functional weapon entity | `WeaponState { byte Group; float CooldownRemainingTicks; float CooldownTicks; byte Firing; }` alongside `FunctionalBlock` | Tech lead (SPEC-21) |
| 3 | SPEC-24: ability slots on the ship | Buffer `AbilitySlot { ContentHash Ability; float CooldownRemainingTicks; float CooldownTicks; byte Charges; byte Active; }`, at most 4 shown | Tech lead (SPEC-24) |
| 4 | SPEC-21/23: player's current target and its name key | `ShipTarget { Entity Value; }` on the player ship; the target grid root carries SPEC-10's blueprint `ContentHash` (`GridBlueprint { ContentHash Value; }`) | Tech lead (SPEC-21) |
| 5 | SPEC-31: interaction singleton and body names | `InteractionTarget { Entity Entity; ulong BodyKey; byte Verb; float Distance; byte InRange; }`; `IBodyNames.GetName(ulong bodyKey)` managed; verbs 0 none, 1 dock, 2 talk, 3 scan, 4 jump, 5 salvage | Tech lead (SPEC-31) |
| 6 | SPEC-30: current star system and names | Singleton `CurrentStarSystem { StarSystemId Id; }`; `IStarSystemNames.GetName(StarSystemId)` | Tech lead (SPEC-30) |
| 7 | SPEC-33: relation between two allegiances (Burst-callable) | Same `Team` → ally; `Team == 0` on either side → neutral; otherwise enemy | Tech lead (SPEC-33) |
| 8 | SPEC-31: marker component on bodies for the radar | `BodyMarker { byte Role; }` on body entities (stations → kind 1, planets/moons/stars → kind 2, points of interest → kind 3) | Tech lead (SPEC-31) |
| 9 | SPEC-50: inventory, wallet and merchant APIs used by presenters | `InventoryModel { int Capacity; ItemStack Get(int slot); event Action<int> SlotChanged; event Action Reset; bool TryMove(...); void Sort(); void StackAll(); }`, `Wallet { int Balance; event Action Changed; }`, `MerchantSession { IReadOnlyList<MerchantStock> Stock; bool TryBuy(ContentId item, int qty, out string reason); bool TrySell(int slot, int qty, out string reason); }`; `ItemDefinition { DisplayName, Description, Icon, BasePrice, Category }` | Tech lead (SPEC-50) |
| 10 | SPEC-42: quest log API and events | `QuestLog { IReadOnlyList<QuestView> Active, Completed; QuestView Tracked; void SetTracked(ContentId); event Action Changed; }`, clues and rumors as read-only lists with `Changed` | Tech lead (SPEC-42) |
| 11 | SPEC-03: screen hotkey actions | `OpenInventory` I, `OpenShipLog` L, `OpenGalaxyMap` M, `OpenSystemMap` N, `OpenFleet` F, present in both Flight and Menu maps | Tech lead (SPEC-03) |
| 12 | SPEC-01: who calls `SetRoot` and builds `UiComposition` | `GameRoot` builds `UiComposition` right after step 9; `SceneFlow` calls `SetRoot(MainMenu/FlightHud/ShipEditor)` when each scene is ready and shows `LoadingView` during transitions | Tech lead (SPEC-01) |
| 13 | SPEC-41: dialog number keys | SPEC-41 handles Dialog-map actions `Choice1..Choice9` and `Continue` and calls the same `IDialogCommands` implementation; `DialogView` handles clicks only | Tech lead (SPEC-41) |
| 14 | SPEC-31/32: waypoint inside a star system | Not in M3: `system-map-set-waypoint` hidden (`CanSetWaypoint = false`) | Owner |
| 15 | When is "Edit ship" allowed from the Fleet screen? | M3: disabled with reason "Dock at a station to edit" unless the player is docked (SPEC-31 docked flag); M4 adds in-world edit | Owner |
| 16 | Ship and save thumbnails | None in M3 (text only); revisit after SPEC-80 approval | Owner |
| 17 | Should Dialog pause the simulation? | Yes (`PausesSimulation = true`) | Owner |
| 18 | Clue graph visualisation | M3 shows a grouped list; a graph view is a later task | Owner |

## 11. References

- Audit `06-ui-shipeditor.md` §1.1 (17 views), §2 B/D/E bugs, §5.2; `LEDGER.md` R4, R7, R8, R11, R12, R13, R31; screenshots `02-newgame.png`, `04-after-intro.png`, `05-flying.png`, `07-ark-dialog.png`, `09-trade.png`, `10-hangar.png`, `22-inventory.png`, `23-fleet.png`.
- `Import/Legacy/ui/*.uxml` (old layouts, reference only; their styles are not reused).
- `docs/design/DESIGN-SYSTEM.md` §5.8 (HUD zones Z-BL, Z-BC, Z-TR, Z-L; final look after approval).
- Synthesis §6j.
