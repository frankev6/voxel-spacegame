# SPEC-61 — Screens and HUD

| | |
|---|---|
| **Status** | Draft |
| **Area code** | HUD (task IDs are `HUD-001`, `HUD-002`, …) |
| **Module(s)** | `Game.UI` (views, view models, UXML, screen USS), `Game.Presentation` (`HudSnapshotSystem`, `HudSyncSystem`, presenters for economy/narrative/galaxy screens, hotkey router), `Game.App` (presenters that need `SceneFlow`, `SaveService`, `SettingsService`, `GameSession`: folder `Assets/Game/App/Presenters/`, because only `Game.App` may reference those services) |
| **Depends on specs** | SPEC-60 (framework), SPEC-62 (`MainCameraState`, `MapViewTransform`), SPEC-01 (scene flow, session, settings, root screens), SPEC-03 (actions), SPEC-05 (save slots), SPEC-10/13 (`VoxelGrid`, `Hulk`), SPEC-20 (`PlayerShip`), SPEC-21 (weapon state, target), SPEC-22 (`ShipStats`, `ShieldState`, `ShipPower`), SPEC-24 (abilities), SPEC-30/31 (galaxy, bodies, interaction, docking), SPEC-32 (gate info, destination, route, travel notices), SPEC-33 (relations, faction colours and name prefixes, hail), SPEC-41 (dialog presenter), SPEC-42 (`ShipLogQuery`, markers), SPEC-50 (inventory, wallet, merchant session and launcher, economy notices) |
| **Depended on by** | SPEC-41 (writes `DialogViewModel`), SPEC-51 (mounts panels into `ShipEditorView`, reuses `StatRowViewModel`), SPEC-01 (`IRootScreenSet` for `SetRoot`; `ConfirmDialog` for defeat), SPEC-31 (dock screen), SPEC-50 (`MerchantPresenter.Show`), SPEC-70 (harness element names) |
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
5. **Presenter placement:** presenters that need only module models live in `Game.Presentation/Presenters/` (Inventory, Merchant, ShipLog, GalaxyMap, StarSystemMap, HUD objective, HUD notifications, hotkeys); presenters that need `SceneFlow`, `SaveService`, `SettingsService`, `GameSession` or App launchers live in `Game.App/Presenters/` (MainMenu, SaveSlots, Settings, Pause, Fleet, Dock, Loading). New folder `Presenters/` in both modules: reason, one presenter per screen and neither `Services/` nor `Views/` describes them.
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
| `dock` | `DockView` | Screens | Menu | no | yes | yes | no | `DockViewModel` | `IDockCommands` | `DockPresenter` (App) |
| `loading` | `LoadingView` | Modals | None | yes | yes | yes | **yes** (installed by `SceneFlow` during transitions, SPEC-01 §5.4.1) | `LoadingViewModel` | — | `LoadingPresenter` (App) |

Back behaviour: `FlightHudView.OnBack()` pushes `PauseView` and returns true. `MainMenuView.OnBack()` returns true (nothing happens). `ShipEditorView.OnBack()` returns `commands.OnBackPressed()`. `LoadingView.OnBack()` returns true (cannot be dismissed). `DockView.OnBack()` calls `commands.Undock()` and returns true. The defeat dialog is SPEC-01's `ConfirmDialog` (§5.9 there); this spec has no defeat screen. `SettingsView.OnBack()` cancels a listening rebind first (returns true), otherwise false (pops). All others return false (pop).

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
                          int HullPercent; float HullFraction /* SPEC-22 MassFraction */; int ShieldValue; int ShieldMax; float ShieldFraction; int ShieldStatus /* 0 none,1 online,2 recharging,3 offline */;
                          int EnergyValue; int EnergyMax; float EnergyFraction; int SpeedMps; bool Boosting;
                          bool HasTarget; string TargetName; int TargetRelation /* 0 neutral,1 ally,2 enemy */; float TargetHullFraction; float TargetShieldFraction; int TargetDistanceM;
                          bool HasInteraction; string InteractionVerbText; string InteractionTargetName; string InteractionKeyText; bool InteractionInRange;
                          string InteractionDetailText /* gate: fuel or locked message; "" otherwise */; bool InteractionBlocked /* locked or unaffordable gate */;
                          string StarSystemName; string ZoneName; bool HasObjective; string ObjectiveTitle; string ObjectiveText;
                          bool HasRoute; string RouteText /* "Route: 3 jumps, 4 fuel" or RouteBlockedReason */; bool RouteBlocked;
                          List<HudWeaponGroupViewModel> WeaponGroups (3, filled once with 3 items); List<HudAbilityViewModel> Abilities (4, filled once with 4 items);
                          int RadarVersion; float RadarRangeM = 2000; float RadarYawDeg; }
HudWeaponGroupViewModel { int GroupNumber; int WeaponCount; float ReadyFraction; bool Firing; string KeyText; }
HudAbilityViewModel     { bool HasAbility; string Name; UnityEngine.Sprite Icon; float CooldownFraction; int Charges; bool Ready; bool Active; string KeyText; }

InventoryViewModel      { int Capacity; int UsedSlots; int OverflowCount; List<InventorySlotViewModel> Slots (200, filled once with 200 items); int SelectedIndex = -1;
                          string SelectedName; string SelectedDescription; int SelectedCount; int SelectedValue; long Wallet; string WalletText; }
InventorySlotViewModel  { int Index; string ItemId; UnityEngine.Sprite Icon; int Count; bool IsEmpty = true; bool IsSelected;
                          bool InCapacity /* = Index < InventoryModel.Capacity; false slots are overflow (SPEC-50) and get .slot--overflow */; }

MerchantViewModel       { string MerchantName; long Wallet; string WalletText; bool Refused; string RefusalReason; List<string> Categories (16); int SelectedCategory;
                          List<MerchantRowViewModel> Stock (128); List<MerchantRowViewModel> Cargo (200); int SelectedStockIndex = -1; int SelectedCargoIndex = -1;
                          int Quantity = 1; int MaxQuantity; int BuyTotal; int SellTotal; bool CanBuy; bool CanSell; string BuyBlockedReason; string SellBlockedReason;
                          string DetailName; string DetailDescription; }
MerchantRowViewModel    { string ItemId; string Name; UnityEngine.Sprite Icon; int Price; int Quantity; bool IsSelected; }

DialogViewModel         { string SpeakerName; UnityEngine.Sprite Portrait; bool HasPortrait; string LineText; int LineVersion;
                          List<DialogOptionViewModel> Options (9); bool CanContinue; bool IsWaitingForChoice; }
DialogOptionViewModel   { int Number /* 1..9 */; string Text; bool Enabled; string DisabledReason; }

ShipLogViewModel        { int SelectedTab /* 0 quests,1 clues,2 rumors */; bool ShowCompleted; List<QuestRowViewModel> Quests (64); int SelectedQuest = -1;
                          string QuestTitle; string QuestStageText; string QuestDescription; List<ObjectiveRowViewModel> Objectives (16); List<string> QuestLog (32);
                          List<ClueRowViewModel> Clues (256); int SelectedClue = -1; string ClueBody; List<MysteryRowViewModel> Mysteries (16);
                          List<JournalRowViewModel> Journal (64); List<RumorRowViewModel> Rumors (64); }
MysteryRowViewModel     { string Title; int Known; int Required; bool Solved; string SolvedText; }
JournalRowViewModel     { string Text; int Value; }
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

DockViewModel           { string StationName; bool CanTrade; string TradeBlockedReason; bool CanEditShip; string EditBlockedReason; }
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
public struct MapPoint  { public Unity.Mathematics.double2 Pos; public string Name; public float RadiusPx; public MapPointFlags Flags; public byte ColorIndex;
                          public string ColorToken; /* e.g. SPEC-33 FactionDefinition.mapColorToken; null/"" = use ColorIndex */ }
[System.Flags] public enum MapPointFlags : byte { None = 0, Known = 1, Visited = 2, Current = 4, Destination = 8, Selected = 16, Interactable = 32 }
public struct MapLine   { public int A, B; public bool Gated; public bool Known; }   // indices into Points
public struct MapRing   { public Unity.Mathematics.double2 Center; public double Radius; }
public struct MapMarker { public Unity.Mathematics.double2 Pos; public MapMarkerKind Kind; public float HeadingDeg; public string Label; }
public enum MapMarkerKind : byte { Player = 0, Destination = 1, Rumor = 2, Waypoint = 3 }
```

`ColorIndex` maps to custom properties `--maps-color-0` … `--maps-color-7` declared on the class `.maps-canvas` in `Theme/screens/maps.uss` (values are `var(--color-…)` tokens only); the canvas reads them from its `customStyle` on `CustomStyleResolvedEvent`. A non-empty `ColorToken` (faction owner colours, SPEC-33 `mapColorToken`, SPEC-80 tokens `--color-faction-*`) wins over `ColorIndex`; the canvas resolves each distinct token once per `CustomStyleResolvedEvent` into a `Dictionary<string, Color>` (≤ 64 entries; an unknown token logs `WarnOnce` and uses `--maps-color-0`). Screen-scoped classes of this spec: `.hud-narrow`, `.hud-blip`, `.hud-blip--ship`, `.hud-blip--station`, `.hud-blip--body`, `.hud-blip--poi`, `.hud-blip--beyond`, `.maps-canvas`, `.maps-label`, `.dialog-option`, `.inventory-grid-row`, `.merchant-row`, `.fleet-row`, `.ship-log-row` (SPEC-60 decision 12).

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
public interface IShipLogCommands        { void SelectTab(int tab); void SetShowCompleted(bool on); void SelectQuest(int index); void ToggleTrack(int index); void SelectClue(int index); void ShowRumorOnMap(int index); }
public interface IGalaxyMapCommands      { void Select(int pointIndex); void SetDestination(); void ClearDestination(); void FocusCurrent(); }
public interface IStarSystemMapCommands  { void Select(int pointIndex); void SetWaypoint(); void FocusPlayer(); }   // SetWaypoint = "Cruise here" (M4)
public interface IFleetCommands          { void Select(int index); void Edit(); void SetActive(); }
public interface IDockCommands           { void Trade(); void EditShip(); void Undock(); }
```

Close buttons (`<key>-close`) call `UIScreen.Close()` inside the view; they are not commands.

### 4.4 HUD zones (`FlightHudView`, `Theme/screens/hud.uss`)

| Zone | Region name | Anchor class | Contents (element names) |
|---|---|---|---|
| Z-TL | `hud-region-location` | `.anchor-top-left` | `hud-star-system` (label), `hud-zone` (label) |
| Z-TC | `hud-region-objective` | `.anchor-top-center` | `hud-objective-title`, `hud-objective-text` (max width 560 px, wraps, max 3 lines then ellipsis), `hud-route` (one line, `.text-muted`; `.is-warning` when `RouteBlocked`) |
| Z-TR | `hud-region-radar` | `.anchor-top-right` | `hud-radar` (240 × 240 px square), `hud-radar-range` (label) |
| Z-R | `hud-region-target` | `.anchor-right-center` | `hud-target-name`, `hud-target-relation` (marker), `hud-target-hull` (Meter), `hud-target-shield` (Meter), `hud-target-distance` (width 280 px) |
| Z-C | `hud-region-prompt` | `.anchor-center` (offset 18 % of height below centre) | `hud-interaction-key` (KeyHint), `hud-interaction-verb`, `hud-interaction-target`, `hud-interaction-detail` (second line; `.is-warning` when `InteractionBlocked`) |
| Z-BL | `hud-region-status` | `.anchor-bottom-left` | `hud-hull-meter`, `hud-shield-meter`, `hud-energy-meter` (Meters, width 320 px), `hud-speed`, `hud-shield-status` |
| Z-BC | `hud-region-weapons` | `.anchor-bottom-center` | `hud-weapon-group-1` … `hud-weapon-group-3` (Slots; key text from `Flight/FirePrimary`, `FireSecondary`, `FireTertiary`), `hud-ability-1` … `hud-ability-4` (Slots; keys 1–4) |
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
│                               [F] Dock  Ark Station                 ▬▬ hull  │
│                                                                     ▬▬ shld  │
│ Z-BL HULL  ▬▬▬▬▬▬▬ 96%            Z-BC [LMB][RMB][SPC]  (1)(2)(3)(4)         │
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
    public float Hull01;                                // ShipStats.MassFraction (SPEC-22 hull proxy, §10 Q1)
    public float ShieldHp, ShieldMax; public byte ShieldStatus;   // ShieldState.Hp/MaxHp; status: MaxHp == 0 → 0 none; Active → 1 online;
                                                        // Flags & Collapsed → 2 recharging; else 3 offline
    public float Energy, EnergyMax;                     // ShipPower.Stored / Capacity
    public float SpeedMps;
    public Unity.Collections.FixedList64Bytes<HudWeaponGroupState> WeaponGroups;  // exactly 3, index = fire-group bit (0 primary, 1 secondary, 2 tertiary)
    public Unity.Collections.FixedList128Bytes<HudAbilityState> Abilities;        // up to 4
    public Game.Core.ContentHash TargetNameHash;        // the target root's GridSource.Blueprint (SPEC-10); 0 for bodies/structures
    public byte TargetFaction;                          // Allegiance.Faction of the target (255 = none); the name gets SPEC-33 shipNamePrefix
    public byte TargetRelation; public float TargetHull01, TargetShield01, TargetDistanceM;
    public byte InteractionKind;                        // SPEC-31 InteractionTarget.Kind (InteractionKind: Dock, Inspect, LaneGate, Hail, Salvage)
    public float InteractionDistanceM; public byte InteractionInRange;
    public ulong InteractionBodyKey;                    // InteractionTarget.Body.LocalKey (0 for non-body targets)
    public ulong InteractionGridPersistentId;           // InteractionTarget.GridPersistentId (hulks, encounter ships)
    public uint StarSystemIdValue;                      // SPEC-31 CurrentStarSystemInfo.Id.Value (0 while Changing)
    public uint RadarVersion;                           // +1 on every radar refresh (10 Hz)
    public uint FrameIndex;
}
[System.Flags] public enum HudFlags : byte { None = 0, HasShip = 1, IsHulk = 2, HasTarget = 4, HasInteraction = 8, Boosting = 16 }
public struct HudWeaponGroupState { public byte Group; public byte WeaponCount; public byte Firing; public byte Pad; public float Ready01; }   // 8 bytes; Group = bit index 0..2
public struct HudAbilityState { public Game.Core.ContentHash Ability; public float Cooldown01; public byte Charges; public byte Ready; public byte Active; public byte Pad; } // 16 bytes

/// Buffer on the snapshot entity. [InternalBufferCapacity(0)]; at most 64 elements.
public struct HudRadarBlip : Unity.Entities.IBufferElementData
{
    public Unity.Mathematics.float2 OffsetXZ;          // metres relative to the player ship, world axes
    public byte Kind;                                  // 0 ship (grid root, Kind Ship/Hulk), 1 station (Kind Station), 2 celestial body (SPEC-31 StarSystemBody with SpatialBody), 3 interactable point (Interactable without a grid)
    public byte Relation;                              // 0 neutral, 1 ally, 2 enemy: FactionRelations.Get(player.Faction, other.Faction) (SPEC-21; 255 → Neutral)
    public byte Flags;                                 // 1 = targeted, 2 = beyond range (clamped to edge), 4 = route waypoint (next gate)
    public byte Pad;
}
```

Memory: snapshot ≈ 260 bytes + 64 × 12 bytes of blips. Constant regardless of world size.

## 5. Systems, services and flow

### 5.1 `HudSnapshotSystem`

- `ISystem`, `[BurstCompile]`, `[UpdateInGroup(typeof(PresentationBridgeGroup))]`, `[UpdateAfter(typeof(CameraTargetSyncSystem))]`, `[UpdateBefore(typeof(HudSyncSystem))]`. `OnCreate`: create the snapshot entity (with `HudSnapshot` and the blip buffer), `RequireForUpdate<GameplayActive>()`.
- Reads: player ship (`PlayerShip`, `ShipStats`, `ShieldState`, `ShipPower`, `PhysicsVelocity`, `ShipCommand`, `Allegiance`, `Hulk`, SPEC-21 `ShipTarget`), weapon functional entities (`FunctionalBlock` + SPEC-21 `WeaponState`), SPEC-24 `AbilitySlot` buffer, SPEC-31 `InteractionTarget` and `CurrentStarSystemInfo` singletons, SPEC-21 `FactionRelations`, grid roots with `VoxelGrid`+`Allegiance`+`LocalToWorld`, SPEC-31 body entities (`StarSystemBody`, `SpatialBody`). Writes only `HudSnapshot` and `HudRadarBlip`.

Per frame:
1. Find the single `PlayerShip` entity with `SystemAPI.TryGetSingletonEntity<PlayerShip>`. None → write `Flags = None`, keep the radar buffer empty, increment nothing else, return.
2. Fill ship fields from components (§4.5 comments). `SpeedMps = length(PhysicsVelocity.Linear)`. `Boosting` from `ShipCommand.Flags` (SPEC-20 boost bit). `IsHulk` when the ship has `Hulk`.
3. Weapon groups: schedule `HudWeaponGatherJob` (`IJobChunk`, single-threaded `Schedule`, Burst) over entities with `FunctionalBlock` and `WeaponState`, filter `FunctionalBlock.Grid == player`; for each set bit `g` (0..2) of `WeaponState.Group`: `WeaponCount++`, `Ready01 = min(Ready01, 1 − CooldownRemainingTicks / max(1, CooldownTicks))`, `Firing |= WeaponState.Firing`. Bits above 2 are ignored. Cost: O(weapon entities in the world) in Burst; no main-thread loop.
4. Abilities: copy up to 4 entries of `AbilitySlot` (`Ability`, `Cooldown01 = CooldownRemainingTicks / max(1, CooldownTicks)`, `Charges`, `Ready = CooldownRemainingTicks <= 0 && Charges > 0`, `Active`).
5. Target: if `ShipTarget.Value` exists: `TargetFaction = Allegiance.Faction` (255 if absent), relation = `FactionRelations.Get(player.Faction, TargetFaction)` mapped Friendly → 1, Hostile → 2, else 0; hull from `ShipStats.MassFraction`, shield from `ShieldState`; distance between `LocalToWorld` positions; `TargetNameHash = GridSource.Blueprint`; else clear `HasTarget`.
6. Interaction: copy `InteractionTarget` (`Target != Entity.Null` → `HasInteraction`; `Kind`, `DistanceM`, `InRange`, `Body.LocalKey`, `GridPersistentId`).
7. Current star system: `StarSystemIdValue = CurrentStarSystemInfo.Changing == 1 ? 0 : Id.Value`.
8. Radar, only when `elapsedUnscaled - lastRadar ≥ 0.1 s` (10 Hz): schedule `HudRadarGatherJob` (`IJobChunk`, `Schedule`, Burst) over grid roots (`VoxelGrid` + `Allegiance` + `LocalToWorld`, `WithNone<PlayerShip>`) and SPEC-31 body entities (`StarSystemBody` + `SpatialBody` + `LocalToWorld`, kind 2; `Interactable` without a grid, kind 3): compute `offset = (pos - playerPos).xz`; keep the 64 nearest using a fixed-size max-heap in the job (`NativeArray` of 64, `Allocator.TempJob`); entries beyond `RadarRangeM` (2,000 m) are clamped to the range circle with flag 2. Then a second single-threaded Burst job writes the buffer sorted by distance (stable tie-break by `Entity.Index`). `RadarVersion++`.
9. `FrameIndex = (uint)frameCount`. Dependencies are left on `state.Dependency`; `HudSyncSystem` completes them.

### 5.2 `HudSyncSystem`

- Managed `SystemBase` listed in the architecture pipeline (`PresentationBridgeGroup`), `[UpdateAfter(typeof(HudSnapshotSystem))]`, `[UpdateBefore(typeof(ManagedEventDrainSystem))]`, `RequireForUpdate<HudSnapshot>()` and `RequireForUpdate<GameplayActive>()`.
- `public void Bind(HudViewModel vm, IHudNames names, Func<string, string> bindingDisplay)` called once by `Game.App`; unbound → return. `IHudNames` (Presentation) resolves: blueprint `ContentHash` → display name, prefixed with the target faction's `FactionDefinition.shipNamePrefix` + space when it is not empty (SPEC-33); `(StarSystemValue, BodyKey)` → `GalaxyService.GetBodyName`; star system id → `GalaxyService.GetStarSystemName`; gate prompts through SPEC-32 `TravelService.TryGetGateInfo` (`TargetName`, `FuelCost`/`FuelHeld`, `LockedMessage`; never the body name "Gate to …"); encounter ships (`Kind == Hail`) → the encounter's display name via SPEC-33 `TryGetInstanceByMember`. All strings are cached per key (no allocation per lookup).

Per frame (marker `HudSyncSystem.Apply`):
1. `CompleteDependency()`; read `HudSnapshot` and the blip buffer once (one `GetSingleton`, one `GetSingletonBuffer`, `AsNativeArray`).
2. Compare with the previous snapshot copy stored in the system (`HudSnapshot last`). Write only changed fields, with quantisation: fractions rounded to 1/500 before comparing, ints exact, `SpeedMps` → `(int)round`, distance → `(int)round` metres, cooldowns and ready fractions rounded to 1/100.
3. Strings: `TargetName`, `InteractionTargetName`, `InteractionDetailText`, `StarSystemName` are assigned only when their key changed (lookup returns a cached instance). `InteractionVerbText` from a fixed table indexed by `InteractionKind`: Dock "Dock", Inspect "Inspect", LaneGate "Jump", Hail "Hail", Salvage "Salvage". For `LaneGate`: `InteractionTargetName = GateInfo.TargetName`, `InteractionDetailText = Open ? "Fuel " + FuelCost : LockedMessage`, `InteractionBlocked = !Open || !CanAfford` (cache key: `(gateKey, TravelService.Revision)`).
3b. Route: when `TravelService.Destination` or `Revision` changed: `HasRoute = RouteStatus != None`; `RouteText = RouteStatus == Ok ? "Route: " + Route.Count + " jumps, " + RouteFuelCost + " fuel" : RouteBlockedReason`; `RouteBlocked = RouteStatus is Blocked or Unreachable`. The next gate (`TryGetNextHop`) is shown on the radar as a waypoint blip (kind 3, flag 4) at `GatePositionM − FrameOrigin`, clamped to the edge like other contacts.
4. Weapon groups and abilities: write the 5 / 4 fixed item view models in place.
5. Radar: if `RadarVersion` changed, copy blips into a reusable `HudRadarBlip[64]` array owned by the system and set `vm.RadarVersion` (the view reads the array through `IHudRadarSource`, implemented by the system, §6); set `vm.RadarYawDeg` from `MainCameraState` yaw when it changed by ≥ 0.5°.
6. No allocation in steps 1–5 (gate §8). The defeat flow is SPEC-01's (`ShipDestroyed`), not this system's.

### 5.3 `FlightHudView` per-frame update path

`FlightHudView` subscribes to `HudViewModel.propertyChanged` in `OnBuild`. In the handler it switches on the args' property name (compared by reference with the static args instances) and updates elements directly: `Meter.SetValue`, `Slot.SetCooldown`, `Label.text = NumberStringCache.Get(...)`, class toggles. Strings that change rarely (names, objective) use UXML `DataBinding`. Radar: on `RadarVersion` change, the view positions its 64 pooled blip elements (`style.translate` in px = offset / range × 120, rotated by `-RadarYawDeg`), sets kind/relation classes (`.marker--ally/--enemy/--neutral` + `.hud-blip--ship/--station/--body/--poi`, `.hud-blip--beyond` for flag 2), and hides unused ones with `.is-hidden`. No element is created after `OnBuild`.

### 5.4 Presenters

All presenters are plain classes constructed by `Game.App` (`UiComposition`, HUD-002) with their models, view model and services, implementing the screen's command interface. They subscribe in a `Bind()` method and unsubscribe in `Unbind()`; `SessionReset` → `Unbind(); Bind(newModels)` and refresh the view model in place.

| Presenter | Inputs (owning spec) | Refresh trigger | Command behaviour |
|---|---|---|---|
| `MainMenuPresenter` | `SaveService` slots (SPEC-05), `IGameFlow` (SPEC-01/02) | screen `OnPush` | `NewGame` → `TextPrompt` (title "New game", label "Commander name", max 24, validator: 1–24 chars of letters, digits, space, `-`, `'`) → `IGameFlow.NewGameAsync(new NewGameArgs(name, 0, false, ContentId.None))`. `Continue` → `IGameFlow.LoadGameAsync(<most recent slot id>)`. `OpenLab` → SPEC-51 `ShipEditorLauncher.OpenLab()`. `Quit` → confirm → `IGameFlow.QuitToDesktop()` |
| `SaveSlotsPresenter` | `SaveService` (SPEC-05) | `OnPush(SaveSlotsMode)`, `GameSaved` | `LoadSelected` (in game) → confirm "Unsaved progress since the last save will be lost." [Load (danger) / Cancel]. `SaveToSelected` on an existing slot → confirm overwrite. `SaveNew` → `TextPrompt` (max 32). `DeleteSelected` → confirm [Delete (danger) / Cancel] (06-B12). Errors → `Notification { Kind = Danger }` and a `ConfirmDialog` with the message |
| `SettingsPresenter` | `SettingsService` (SPEC-01), `InputService` rebinding (SPEC-03) | `OnPush`, settings changed | Each `Set*` writes the setting immediately (persisted by `SettingsService`). `StartRebind(row)` → SPEC-03 rebind with Esc cancel; row shows "Press a key…" |
| `PausePresenter` | `IGameFlow.GetSaveBlock()` (SPEC-02) | `OnPush` | `Save` → push `SaveSlotsView` (Save), disabled with the block reason text when `GetSaveBlock() != None`. `Load` → push `SaveSlotsView` (Load). `MainMenu` → confirm → `IGameFlow.QuitToMainMenuAsync(saveFirst: true)`. `Quit` → confirm → `IGameFlow.QuitToDesktop()` |
| `InventoryPresenter` | `InventoryModel`, `Wallet`, `ItemDefinition` (SPEC-50 §4.3–§4.4) | `SlotChanged`, `Reset`, `Wallet.Changed` | `Select`, `Move` (`TryMove`), `Sort`, `StackAll`. `InCapacity = index < Capacity` (overflow slots shown with `.slot--overflow`); `WalletText` = credits formatted with thousands separators (`long`) |
| `MerchantPresenter` | SPEC-50 `MerchantSession` | `Show(session, onClosed)` (called by SPEC-50 `MerchantLauncher`); `session.Changed` | Binds the view model to the session and pushes `merchant`. `Refused` → all buy/sell disabled with `RefusalReason`. `Buy` → `session.TryBuy(stockIndex, Quantity, out reason)`; `Sell` → `session.TrySell(cargoSlot, Quantity, out reason)`; `CanBuy`/`CanSell` drive the button states and `BuyBlockedReason`/`SellBlockedReason`. On pop: `session.Close()`, then `onClosed?.Invoke()` |
| `ShipLogPresenter` | SPEC-42 `ShipLogQuery` | `ShipLogQuery.Changed`, `OnPush` | Quests: `GetQuests(ShowCompleted)`, objectives `GetObjectives`, log `GetLog`; Clues tab: `GetClues(rumors: false)`, `GetMysteries`, `GetJournal` (journal rows under the clues); Rumors tab: `GetClues(rumors: true)`. `SelectClue` → `MarkClueSeen`. `ToggleTrack` → `SetTracked`. `ShowRumorOnMap` → push `GalaxyMapView` with the rumor's star system selected |
| `GalaxyMapPresenter` | SPEC-30 `GalaxyMapModel` (entries, `OwnerColorToken` → `MapPoint.ColorToken`), SPEC-32 `TravelService` (destination, route), SPEC-42 `ShipLogQuery.Markers` (Rumor → `MapMarkerKind.Rumor`) | `OnPush`, `StarSystemEntered`, `FactChanged`, `GalaxyMapModel.Revision` | `CanSetDestination` from `TravelService.CanSetDestination(id, out reason)` (`DestinationBlockedReason` = reason). `SetDestination` → `TravelService.SetDestination`; `ClearDestination` → `TravelService.ClearDestination`. The route (`TravelService.Route`) is drawn as highlighted lines; `Destination` gets `MapPointFlags.Destination` |
| `StarSystemMapPresenter` | bodies and orbits at current `UniversalTime` (SPEC-31), player pose, SPEC-42 markers (Objective → `MapMarkerKind.Waypoint`; `BodyTag` resolved in the loaded star system) | `OnPush` | M3: `CanSetWaypoint = false` (button hidden). M4: `SetWaypoint` = "Cruise here": `TravelService.CanCruise/RequestCruise(body position, body radius)` (SPEC-32) |
| `FleetPresenter` | fleet in `GameSession` (SPEC-01), stats (SPEC-22 `BlockStatMath`) | `OnPush`, fleet change | `Edit` → SPEC-51 launcher (only when docked, §10 Q15) |
| `DockPresenter` | SPEC-31 `DockingService` + `EventBus<DockStateChanged>`, SPEC-50 `MerchantLauncher`/`MerchantDirectory`, SPEC-51 `ShipEditorLauncher`, `GalaxyService` names | `DockStateChanged(true)` → push `dock`; `(false)` → pop it if open | `StationName = GetBodyName(DockedBody)`. `Trade` → `MerchantLauncher.TryOpenForBody(docking.DockedBody, onClosed: null)`; `CanTrade` = `MerchantDirectory.FindForBody(DockedBody) != null`, else reason "No market here". `EditShip` → `ShipEditorLauncher.EditPlayerShip()`. `Undock` → `docking.Undock()` (the event pops the screen) |
| `LoadingPresenter` | `SceneFlow.Progress` (SPEC-01), `SceneTransitionStarted`/`Finished` | events; progress polled in `UIRoot.Tick` while visible | `StatusText` "Loading…"; `Progress = SceneFlow.Progress` |
| `HudObjectivePresenter` | SPEC-42 `ShipLogQuery.TryGetTracked` | `ShipLogQuery.Changed` | writes `HasObjective`, `ObjectiveTitle` (quest title), `ObjectiveText` (current objective text with progress) |
| `HudNotificationPresenter` | `ItemAcquired`, `GameSaved`, SPEC-32 `TravelNotice`, SPEC-50 `EconomyNotice`, SPEC-40 `InteractionUnhandled` (EventBus) | events | `ItemAcquired` → `Notification { Kind = Positive, Label = item name, Amount = Count, CoalesceKey = cached "item:" + id }`; `GameSaved` → Info "Game saved"; `TravelNotice`: `JumpRefused`, `JumpAborted`, `RouteBlocked` → Warning, `DestinationReached`, `RouteRestored` → Info, label = `Text`; `EconomyNotice`: `CargoFull`, `Jettisoned`, `SalvageRefused`, `TradeRefused` → Warning, `SalvageDone`, `SalvagePartial` → Positive, label = `Text`; `InteractionUnhandled` → Info "Nothing of interest" |

### 5.5 `ScreenHotkeyRouter` (Presentation, `Assets/Game/Presentation/Presenters/ScreenHotkeyRouter.cs`)

Actions (SPEC-03 §4.2): `OpenInventory` (I), `OpenShipLog` (L), `OpenGalaxyMap` (M), `OpenSystemMap` (N), `OpenFleet` (K; F is Interact) in **both** Flight and Menu maps. On `performed` of action → target screen T:
1. `top = navigator.Top`. If `top == T` → `navigator.Pop(T)`; return.
2. If `top` is `FlightHudView` → `navigator.Push(T)`; return.
3. If `top` is one of the five hotkey screens → `navigator.Pop(top); navigator.Push(T)`; return.
4. Otherwise (pause, settings, confirm, merchant, dock, dialog, loading, editor) → ignore.

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
| `ship-log` | Panel: `ship-log-tabs` (Quests, Clues, Rumors); Quests: `ship-log-show-completed` (Toggle), `ship-log-quest-list` (ListView 420 px) + detail (`ship-log-quest-title`, `-stage`, `-description`, `ship-log-objectives` list with done class, `ship-log-quest-log`); Clues: `ship-log-mysteries` (title, known/required, solved text), `ship-log-clue-list` grouped by mystery title + `ship-log-clue-body`, then `ship-log-journal` (ListView); Rumors: `ship-log-rumor-list` rows with `Show on map` buttons `ship-log-rumor-map-<index>`; header `ship-log-close` |
| `galaxy-map` | Full panel: `galaxy-map-canvas` (`MapCanvas`, `.grow`) and right column 360 px: `galaxy-map-selected-name`, `galaxy-map-selected-info`, `galaxy-map-set-destination` (primary, disabled with reason), `galaxy-map-clear-destination`, `galaxy-map-focus-current`, `galaxy-map-zoom-in`, `galaxy-map-zoom-out`, legend; header `galaxy-map-close` |
| `system-map` | Same layout with `system-map-*` names and `system-map-set-waypoint` (label "Cruise here", hidden in M3) |
| `fleet` | Panel: left `fleet-ship-list` (ListView 420 px; rows: name `.text-ellipsis`, hull Meter, active tag); right: `fleet-detail-name`, `fleet-detail-class`, `fleet-stats` (ListView of `StatRowViewModel`, two columns label/value, row height fixed), footer `fleet-edit`, `fleet-set-active`; header `fleet-close` |
| `ship-editor` | Root host: regions `ship-editor-region-top` (full width, 64 px), `ship-editor-region-left` (360 px), `ship-editor-region-right` (380 px), `ship-editor-region-bottom` (full width, 48 px), all `.hud-region .hud-plate`, center empty (world shows through, `picking-mode Ignore`). Panels mounted by SPEC-51 |
| `dock` | Panel 560 px wide centred: header `dock-station-name`; body buttons `dock-trade` (primary; disabled with `dock-trade-blocked` reason), `dock-edit-ship` (disabled with reason), `dock-undock` (`.stack-m`); story dialogs started by docking (SPEC-42) open above it |
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

    /// <summary>The root screens SceneFlow installs (SPEC-01 §4.3 SetRootScreens). Implemented by UiComposition.</summary>
    public interface IRootScreenSet { UIScreen MainMenu { get; } UIScreen FlightHud { get; } UIScreen ShipEditor { get; } UIScreen Loading { get; } }
}

namespace Game.Presentation.Presenters
{
    /// <summary>Registered in Services by UiComposition; SPEC-50 MerchantLauncher calls Show (step 3 of its Open).</summary>
    public sealed class MerchantPresenter : Game.UI.Views.IMerchantCommands
    { public void Show(Game.Economy.MerchantSession session, System.Action onClosed); /* + IMerchantCommands */ }
}

namespace Game.App.Presenters
{
    /// Builds every view model, view and presenter once and registers the views. Called by GameRoot (SPEC-01) after step 9.
    public sealed class UiComposition : Game.UI.Views.IRootScreenSet, System.IDisposable
    {
        public UiComposition(Game.UI.Services.UIRoot ui /* + services, see HUD-002 */);
        public UIScreen MainMenu { get; }           // MainMenuView; IRootScreenSet (SceneFlow.SetRoot)
        public UIScreen FlightHud { get; }          // FlightHudView
        public UIScreen ShipEditor { get; }         // ShipEditorView
        public UIScreen Loading { get; }            // LoadingView
        public void Dispose();
    }
}
```

`HudRadarBlipView` is the `Game.UI` copy of a blip (the UI module cannot see Presentation components); `HudSyncSystem` converts `HudRadarBlip` → `HudRadarBlipView` into its reusable array.

## 7. Edge cases and failure behaviour

| Situation | Required behaviour |
|---|---|
| No player ship (intro, after despawn) | HUD shows only location/objective zones; ship zones get `.is-hidden` via `HasShip == false` |
| Player ship becomes a hulk | HUD meters stay at their last values; SPEC-01 `DefeatWatcher` shows the defeat `ConfirmDialog` ("Load last save" / "Main menu") |
| Two `PlayerShip` entities | Snapshot uses the one `TryGetSingletonEntity` fails on → treat as none and `GameLog.WarnOnce` |
| Weapon on fire-group bits above 2 | Ignored by the HUD |
| More than 64 radar contacts | Nearest 64 shown (stable by distance, then entity index) |
| Contact beyond 2,000 m | Clamped to the radar edge with the "beyond" style |
| Target destroyed between frames | `HasTarget` false next frame; no exception |
| Name lookup misses | Shows the content id (cached) and logs `WarnOnce` |
| Inventory with overflow items (slots ≥ `Capacity`) | Shown in the same grid with `.slot--overflow`; the header shows "used / capacity (+N overflow)" |
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
| Interacting with a locked gate | Prompt shows `LockedMessage` with `.is-warning`; pressing F lets SPEC-32 refuse with a `TravelNotice` toast (and a story beat may play, SPEC-42) |
| Docked when a story dialog starts | Dialog opens above the dock screen; closing it returns to the dock screen |

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
- **Files:** `Assets/Game/UI/ViewModels/*.cs` (one file per view model), `Assets/Game/UI/Data/{MapData,MapPoint,MapPointFlags,MapLine,MapRing,MapMarker,MapMarkerKind}.cs`, `Assets/Game/UI/Views/Commands/*.cs`, `Assets/Game/UI/Views/{MainMenuView,SaveSlotsView,SettingsView,PauseView,FlightHudView,InventoryView,MerchantView,DialogView,ShipLogView,GalaxyMapView,StarSystemMapView,FleetView,ShipEditorView,DockView,LoadingView,IHudRadarSource,HudRadarBlipView,IRootScreenSet}.cs`, `Assets/Game/Presentation/Components/{HudSnapshot,HudFlags,HudWeaponGroupState,HudAbilityState,HudRadarBlip}.cs`, `Assets/Game/App/Presenters/UiComposition.cs`
- **Acceptance criteria:**
  - [x] Compiles; every screen's flags equal §4.1 (checked by test).
  - [x] `HudSnapshot`, `HudRadarBlip` are unmanaged; `HudWeaponGroupState` is 8 bytes, `HudAbilityState` 16 bytes.
- **Tests:** `Assets/Game/Tests/EditMode/UI/ScreenTableTests.cs`: `EveryScreen_FlagsMatchSpecTable` (table literal in the test). `Assets/Game/Tests/EditMode/Presentation/HudSnapshotLayoutTests.cs`: `Sizes_AsSpecified`, `Unmanaged`.

### HUD-002 — UI composition, screen registration and root switching
- **Milestone:** M3
- **Tier:** frontier — F5: boot order, scene flow root switching, session rebind across App, UI and Presentation
- **Depends on:** `HUD-001`, `UI-001`, `UI-002`, `UI-003`, `UI-004`, `UI-005`, `UI-006`, `UI-007`, `UI-008`, `UI-009`, `UI-010`, `UI-011`, `FND-001`
- **Do:** Remove/unbind ADR-0047's temporary SceneNavigationBridgeSystem and replace its M1 root/pause/blocker registrations before enabling full scene-root switching; the navigator remains the sole input owner.  Implement `UiComposition`: create all view models, views (registered on `UIRoot`), and presenters with their services (§5.4); register screen UXML entries in `UIConfig` (`Prewarm` true for `hud`, `pause`, `loading`, `confirm`, `text-prompt`); bind `HudSyncSystem` and the hotkey router; `SessionReset` handling. Provide the three root screens to `SceneFlow` (SPEC-01 calls `SetRoot`, §10 Q12).
- **Sequencing:** [ADR-0072](../decisions/ADR-0072-hud-composition-sequencing.md): HUD-002 owns lifetime/registration/root/session infrastructure and preserves existing playable root behavior. Downstream screen/presenter/sync/hotkey tasks install their production bindings when implemented; their scope and acceptance are unchanged. Empty roots or fake service facades do not satisfy this task.
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
- **Depends on:** `HUD-001`, `UI-011`, `PERS-002`, `PERS-005`, `PERS-006`, `FND-009`
- **Do:** `SaveSlotsView` (ListView with `makeItem`/`bindItem`), `SaveSlotsPresenter` with confirmations.
- **Integration:** ADR-0072 Q23: construct against completed view-model, UI and save/flow contracts in parallel with HUD-002. Production registration/session rebind is completed in the shared integration batch; layout audit and all five tests remain required.
- **Files:** `Assets/Game/UI/Views/SaveSlotsView.cs`, `Assets/Game/UI/Views/Uxml/{SaveSlotsView,SaveSlotItem}.uxml`, `Assets/Game/UI/Theme/screens/save-slots.uss`, `Assets/Game/App/Presenters/SaveSlotsPresenter.cs`
- **Acceptance criteria:**
  - [ ] Delete requires confirmation; Esc on the confirm keeps the slot (06-B12).
  - [ ] Overwrite and in-game load require confirmation.
  - [ ] 64 slots scroll; layout audit clean.
- **Tests:** `Assets/Game/Tests/PlayMode/UI/Screens/SaveSlotsViewTests.cs`: `Delete_RequiresConfirm`, `Delete_EscKeepsSlot`, `Overwrite_RequiresConfirm`, `SaveError_ShowsDangerToast`, `Layout_64Slots_AllLogicalSizes_NoIssues`.

### HUD-005 — Settings screen
- **Milestone:** M3
- **Tier:** standard — view on an existing view model; rebinding through SPEC-03's API
- **Depends on:** `HUD-001`, `UI-011`, `UI-012`, `UI-013`, `FND-004`, `INP-003`
- **Sequencing:** ADR-0072 permits construction against completed services alongside HUD-002; real app registration, lifecycle and menu/pause navigation remain required before closure.
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
  - [ ] Snapshot equals hand-computed values in a test world (ship stats, 7 weapons on 3 fire-group bits, 2 abilities, target, interaction).
  - [ ] A target whose faction is hostile to the player's in `FactionRelations` gives `TargetRelation == 2`; faction 255 gives 0.
  - [ ] Radar: 100 contacts → nearest 64 sorted by distance; contacts beyond 2,000 m flagged and clamped.
  - [ ] Radar refreshes at 10 Hz (`RadarVersion` increments 10 ± 1 times per simulated second at 144 FPS).
  - [ ] No main-thread iteration over entities.
- **Tests:** `Assets/Game/Tests/EditMode/Presentation/HudSnapshotSystemTests.cs` (`EcsTestWorld`): `NoPlayer_FlagsNone`, `ShipValues_Copied`, `WeaponGroups_Aggregated`, `GroupBitsAbove2_Ignored`, `Target_RelationFromFactionRelations`, `Radar_Nearest64_Sorted`, `Radar_BeyondRange_Clamped`, `Radar_RefreshRate10Hz`, `Hulk_FlagSet`.

### HUD-009 — HudSyncSystem
- **Milestone:** M3
- **Tier:** frontier — F4: owns the 0 B/frame HUD gate
- **Depends on:** `HUD-008`, `HUD-007`, `TRV-001`, `ENC-001`
- **Do:** Implement §5.2 including `Bind`, quantisation, `IHudNames` cache (blueprint names with SPEC-33 prefixes, `GalaxyService` body and star-system names, SPEC-32 gate info, SPEC-33 encounter names), the route readout (step 3b), `IHudRadarSource` implementation.
- **Files:** `Assets/Game/Presentation/Systems/HudSyncSystem.cs`, `Assets/Game/Presentation/Presenters/{IHudNames,HudNames}.cs`
- **Acceptance criteria:**
  - [ ] A view model property raises `propertyChanged` only when its quantised value changes.
  - [ ] 0 B GC per frame with all values changing every frame (600 frames).
  - [ ] A gate interaction shows the target star system's name and "Fuel N", or the locked message with `InteractionBlocked`.
  - [ ] With a destination set, `RouteText` reads "Route: 2 jumps, 3 fuel" for a two-lane route costing 3.
- **Tests:** `Assets/Game/Tests/EditMode/Presentation/HudSyncSystemTests.cs`: `Unchanged_NoNotifications`, `Quantisation_SmallChangeIgnored`, `TargetNameChange_SingleStringAssign`, `GatePrompt_FromGateInfo`, `RouteText_Formatted`, `AllChanging_ZeroAlloc`. `Assets/Game/Tests/PlayMode/UI/HudPerformanceTests.cs`: `CombatValuesEveryFrame_ZeroGc`, `Snapshot_30Ships`, `Sync_AllChanged`.

### HUD-010 — HUD presenters and screen hotkeys
- **Milestone:** M3
- **Tier:** standard — behaviour tables in §5.4 and §5.5
- **Depends on:** `HUD-009`, `QST-001`, `ECO-001`, `INP-001`, `TRV-001`, `NAR-001`
- **Do:** `HudObjectivePresenter` (on `ShipLogQuery`), `HudNotificationPresenter` (all §5.4 events), `ScreenHotkeyRouter`.
- **Files:** `Assets/Game/Presentation/Presenters/{HudObjectivePresenter,HudNotificationPresenter,ScreenHotkeyRouter}.cs`
- **Acceptance criteria:**
  - [ ] 12 `ItemAcquired` events for the same item within 2.5 s → one toast "+12 <name>".
  - [ ] Hotkey I: HUD → Inventory; I again → closed; I then M → Inventory replaced by Galaxy map; I during Pause → ignored.
  - [ ] Tracked objective text updates on quest change only.
  - [ ] `TravelNotice(JumpRefused)` → one Warning toast with its text; `EconomyNotice(SalvageDone)` → one Positive toast; `InteractionUnhandled` → "Nothing of interest".
  - [ ] K opens and closes the fleet screen; F does not.
- **Tests:** `Assets/Game/Tests/EditMode/Presentation/ScreenHotkeyRouterTests.cs`: `FromHud_Opens`, `SameKey_Closes`, `OtherKey_Switches`, `ModalOpen_Ignored`. `Assets/Game/Tests/EditMode/Presentation/HudNotificationPresenterTests.cs`: `TwelveItems_OneToast`, `TravelAndEconomyNotices_Mapped`, `InteractionUnhandled_Info`; `ScreenHotkeyRouterTests.FleetOnK`. `Assets/Game/Tests/EditMode/Presentation/HudObjectivePresenterTests.cs`: `QuestChange_UpdatesText`.

### HUD-011 — Inventory screen
- **Milestone:** M3
- **Tier:** standard — view on an existing view model with a fixed pool
- **Depends on:** `HUD-001`, `UI-003`, `UI-004`, `UI-005`, `UI-006`, `UI-007`, `UI-008`, `UI-012`, `ECO-003`, `ECO-004`, `ECO-006`
- **Do:** `InventoryView` (200 pooled `Slot`s, drag-move with a `PointerManipulator` that captures the pointer and drops on the slot under the pointer), `InventoryPresenter`.
- **Files:** `Assets/Game/UI/Views/InventoryView.cs`, `Assets/Game/UI/Views/Uxml/InventoryView.uxml`, `Assets/Game/UI/Theme/screens/inventory.uss`, `Assets/Game/Presentation/Presenters/InventoryPresenter.cs`
- **Acceptance criteria:**
  - [ ] Every slot within `Capacity` is reachable by scrolling; slot index `Capacity - 1` is displayed (06-D1); overflow slots carry `.slot--overflow`.
  - [ ] Sort and Stack All sit inside the panel header (R31); no `OutsidePanel` issue.
  - [ ] Opening the screen changes nothing in the inventory (R12 side effect).
- **Tests:** `Assets/Game/Tests/PlayMode/UI/Screens/InventoryViewTests.cs`: `LastSlot_Displayed`, `HeaderButtons_InsidePanel`, `Open_NoModelChange`, `DragMove_CallsMove`, `Layout_AllLogicalSizes_NoIssues`.

### HUD-012 — Merchant screen
- **Milestone:** M3
- **Tier:** standard — view on an existing view model
- **Depends on:** `HUD-001`, `UI-003`, `UI-004`, `UI-005`, `UI-006`, `UI-007`, `UI-008`, `UI-012`, `ECO-003`, `ECO-004`, `ECO-005`
- **Do:** `MerchantView` (TabBar, two ListViews, quantity slider), `MerchantPresenter` with `Show(session, onClosed)` (§6), registered in `Services` by `UiComposition`.
- **Files:** `Assets/Game/UI/Views/MerchantView.cs`, `Assets/Game/UI/Views/Uxml/{MerchantView,MerchantRow}.uxml`, `Assets/Game/UI/Theme/screens/merchant.uss`, `Assets/Game/Presentation/Presenters/MerchantPresenter.cs`
- **Acceptance criteria:**
  - [ ] Ten categories at logical 1663×1247 stay inside the panel (R12).
  - [ ] Buy of quantity 1 from a stack of 5 leaves 4 in stock (06-A13 via SPEC-50).
  - [ ] Failed buy shows the reason and changes no wallet value.
  - [ ] A refused session (hostile owner) disables buy and sell with `RefusalReason`; popping the screen calls `session.Close()` then `onClosed` once.
- **Tests:** `Assets/Game/Tests/PlayMode/UI/Screens/MerchantViewTests.cs`: `TenTabs_4x3_NoOverflow`, `BuyOne_StockDecrementsByOne`, `BuyFails_ReasonShown_WalletUnchanged`, `Layout_AllLogicalSizes_NoIssues`.

### HUD-013 — Dialog host screen
- **Milestone:** M3
- **Tier:** standard — view on an existing view model; SPEC-41 drives it
- **Depends on:** `HUD-001`, `UI-003`, `UI-004`, `UI-005`, `UI-006`, `UI-007`, `UI-008`, `UI-012`, `DLG-001`
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
- **Do:** `ShipLogView` (three tabs; mysteries and journal under Clues), `ShipLogPresenter` on SPEC-42 `ShipLogQuery` (§5.4).
- **Files:** `Assets/Game/UI/Views/ShipLogView.cs`, `Assets/Game/UI/Views/Uxml/{ShipLogView,QuestRow,ClueRow,RumorRow}.uxml`, `Assets/Game/UI/Theme/screens/ship-log.uss`, `Assets/Game/Presentation/Presenters/ShipLogPresenter.cs`
- **Acceptance criteria:**
  - [ ] Selecting a quest shows its objectives with done states and its log lines.
  - [ ] Selecting a new clue clears its `IsNew` mark through `MarkClueSeen`.
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
- **Depends on:** `HUD-015`, `SPEC-30`, `SPEC-31`, `TRV-001`, `QST-001`
- **Do:** `GalaxyMapView`, `StarSystemMapView`, `GalaxyMapPresenter`, `StarSystemMapPresenter` (system map uses XZ of `double3` star-system positions as `double2`), per §5.4: destination and route through SPEC-32, owner colours through `MapPoint.ColorToken`, SPEC-42 markers.
- **Files:** `Assets/Game/UI/Views/{GalaxyMapView,StarSystemMapView}.cs`, `Assets/Game/UI/Views/Uxml/{GalaxyMapView,StarSystemMapView}.uxml`, `Assets/Game/Presentation/Presenters/{GalaxyMapPresenter,StarSystemMapPresenter}.cs`
- **Acceptance criteria:**
  - [ ] Opening centres on the current star system (galaxy) or the player (system).
  - [ ] Set destination disabled with `TravelService.CanSetDestination`'s reason; a set destination draws its route; Clear removes it.
  - [ ] A star system owned by `faction_vestari` is drawn with `--color-faction-vestari`.
  - [ ] A rumor marker appears on its target star system.
  - [ ] Layout audit clean at all logical sizes.
- **Tests:** `Assets/Game/Tests/PlayMode/UI/Screens/MapViewsTests.cs`: `GalaxyOpen_CentredOnCurrent`, `SystemOpen_CentredOnPlayer`, `GatedTarget_DestinationDisabledWithReason`, `Destination_RouteDrawnAndCleared`, `OwnerColour_FromToken`, `RumorMarker_Shown`, `Layout_AllLogicalSizes_NoIssues`.

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
- **Depends on:** `HUD-001`, `UI-003`, `UI-004`, `UI-005`, `UI-006`, `UI-007`, `UI-008`, `UI-012`, `EDT-001`, `EDT-021`
- **Do:** `ShipEditorView` with four regions; `OnBuild` calls `ShipEditorPanels.Mount(regions, vm, commands)` (SPEC-51); `OnBack` forwards to `commands.OnBackPressed()`.
- **Files:** `Assets/Game/UI/Views/ShipEditorView.cs`, `Assets/Game/UI/Views/Uxml/ShipEditorView.uxml`, `Assets/Game/UI/Theme/screens/ship-editor.uss`
- **Acceptance criteria:**
  - [ ] Root install via `SetRoot` hides `FlightHudView` and shows only the editor root (R13).
  - [ ] Centre area is not pickable (world picking works between panels).
  - [ ] Regions do not overlap at all logical sizes.
- **Tests:** `Assets/Game/Tests/PlayMode/UI/Screens/ShipEditorViewTests.cs`: `SetRoot_OnlyEditorRootVisible`, `Centre_NotPickable`, `Regions_NoOverlap_AllLogicalSizes`, `Back_ForwardsToCommands`.

### HUD-019 — Loading and dock screens
- **Milestone:** M3
- **Tier:** standard — views on existing view models; presenter steps in §5.4
- **Depends on:** `HUD-010`, `SYS-001`, `ECO-001`, `EDT-001`
- **Do:** `LoadingView`, `LoadingPresenter`, `DockView`, `DockPresenter` (§5.4; `DockStateChanged` drives push/pop).
- **Files:** `Assets/Game/UI/Views/{LoadingView,DockView}.cs`, `Assets/Game/UI/Views/Uxml/{LoadingView,DockView}.uxml`, `Assets/Game/UI/Theme/screens/{loading,dock}.uss`, `Assets/Game/App/Presenters/{LoadingPresenter,DockPresenter}.cs`
- **Acceptance criteria:**
  - [ ] Esc cannot dismiss the loading view; it blocks input (context `None`) and hides the HUD.
  - [ ] `DockStateChanged(true)` pushes the dock screen once; Esc or Undock calls `DockingService.Undock()` and the `(false)` event pops it.
  - [ ] Trade opens the merchant screen through `MerchantLauncher.TryOpenForBody`; with no merchant the button is disabled with "No market here".
- **Tests:** `Assets/Game/Tests/PlayMode/UI/Screens/LoadingDockViewTests.cs`: `Loading_EscIgnored_ContextNone_HudHidden`, `Dock_PushOnDock_PopOnUndock`, `Dock_Trade_OpensMerchant`, `Dock_NoMerchant_TradeDisabled`.

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
| 1 | SPEC-22: hull, shield, energy fields | **Answered:** hull = `ShipStats.MassFraction`; shield = `ShieldState.Hp/MaxHp/Active/Flags`; energy = `ShipPower.Stored/Capacity` (§4.5) | Tech lead (SPEC-22) |
| 2 | SPEC-21: weapon state readable per functional weapon entity | **Answered:** SPEC-21 `WeaponState` (`Group` bits, `Firing`, `CooldownRemainingTicks`, `CooldownTicks`) | Tech lead (SPEC-21) |
| 3 | SPEC-24: ability slots on the ship | **Answered:** SPEC-24 `AbilitySlot` buffer (fields marked "SPEC-61") | Tech lead (SPEC-24) |
| 4 | SPEC-21/23: player's current target and its name key | **Answered:** SPEC-21 `ShipTarget.Value`; name from `GridSource.Blueprint` (SPEC-10) with the SPEC-33 `shipNamePrefix` | Tech lead (SPEC-21) |
| 5 | SPEC-31: interaction singleton and body names | **Answered:** SPEC-31 `InteractionTarget` (§4.3); names via `GalaxyService.GetBodyName`; gate prompts via SPEC-32 `TryGetGateInfo`; verb from `InteractionKind` | Tech lead (SPEC-31) |
| 6 | SPEC-30: current star system and names | **Answered:** SPEC-31 `CurrentStarSystemInfo`; `GalaxyService.GetStarSystemName` | Tech lead (SPEC-30) |
| 7 | SPEC-33: relation between two allegiances (Burst-callable) | **Answered:** `FactionRelations.Get(a.Faction, b.Faction)` (SPEC-21; 255 → Neutral); colours from `FactionDefinition.mapColorToken`; names prefixed with `shipNamePrefix`; encounter ships show the Hail prompt | Tech lead (SPEC-33) |
| 8 | SPEC-31: marker component on bodies for the radar | **Answered:** SPEC-31 `StarSystemBody` + `SpatialBody` (kind 2) and `Interactable` without a grid (kind 3); no extra marker component | Tech lead (SPEC-31) |
| 9 | SPEC-50: inventory, wallet and merchant APIs used by presenters | **Answered:** SPEC-50 §4.3–§4.4 (`Wallet.Credits` is `long`; `MerchantSession.TryBuy/TrySell/CanBuy/CanSell/Refused`; `MerchantLauncher.TryOpenForBody`; `EconomyNotice`) | Tech lead (SPEC-50) |
| 10 | SPEC-42: quest log API and events | **Answered:** SPEC-42 `ShipLogQuery` and its `Markers` | Tech lead (SPEC-42) |
| 11 | SPEC-03: screen hotkey actions | **Answered:** `OpenInventory` I, `OpenShipLog` L, `OpenGalaxyMap` M, `OpenSystemMap` N, `OpenFleet` K (F is Interact), in Flight and Menu maps | Tech lead (SPEC-03) |
| 12 | SPEC-01: who calls `SetRoot` and builds `UiComposition` | **Answered:** SPEC-01 §5.2 step 10 builds `UiComposition` and calls `SceneFlow.SetRootScreens(composition)` (`IRootScreenSet`); `SceneFlow` calls `SetRoot` per scene and installs `Loading` during transitions | Tech lead (SPEC-01) |
| 13 | SPEC-41: dialog number keys | SPEC-41 handles Dialog-map actions `Choice1..Choice9` and `Continue` and calls the same `IDialogCommands` implementation; `DialogView` handles clicks only | Tech lead (SPEC-41) |
| 14 | SPEC-31/32: waypoint inside a star system | **Answered:** M3 hidden; M4 "Cruise here" through `TravelService.CanCruise/RequestCruise` | Owner |
| 15 | When is "Edit ship" allowed from the Fleet screen? | M3: disabled with reason "Dock at a station to edit" unless the player is docked (SPEC-31 docked flag); M4 adds in-world edit | Owner |
| 16 | Ship and save thumbnails | None in M3 (text only); revisit after SPEC-80 approval | Owner |
| 17 | Should Dialog pause the simulation? | Yes (`PausesSimulation = true`) | Owner |
| 18 | Clue graph visualisation | M3 shows a grouped list; a graph view is a later task | Owner |
| 19 | HUD panels for new ship systems (review X12): a hangar, tractor or cloak needs HUD state without editing `HudSyncSystem` | Add when the first such system is specified: an `IShipSystemPanel` registry in `Game.UI` (view model + UXML template + a Presentation-side sync callback registered at composition), drawn in a fixed HUD region in registration order | UI lead |
| 20 | HUD-001's exact ShipEditorView constructor needs SPEC-51 types before EDT-001, which depends on HUD-001 | **Resolved (2026-10-13, Tech lead):** bring forward only the canonical `Game.UI.ViewModels.ShipEditorViewModel : ViewModelBase` and `Game.UI.Views.IShipEditorCommands` type identities in their SPEC-51 folders as empty shells. Precise `TODO(EDT-001)` notes retain all editor view-model properties, nested models and command members for EDT-001. ShipEditorView keeps the exact constructor signature; no editor behavior or alternative signatures. Existing provisional screens/adapter remain installed until HUD-002. | Tech lead |

| 21 | HUD-008 byte weapon counts, repeatable unscaled-clock acceptance and overlapping radar categories | Resolved: saturate the public byte weapon count at255 rather than wrapping; production radar uses unscaled engine time, with an internal per-system clock seam for the required144-FPS simulated acceptance. Radar queries are disjoint: grids take precedence, then bodies, then nongrid interactables not already in the body pass (including StarSystemBody anchors lacking SpatialBody). The future route-service bridge in HUD-009 supplies waypoint identity; HUD-008 does not fabricate a gate. | Tech lead |

| 22 | HUD-002 waits for whole SPEC-60 and downstream systems that depend on HUD-002 | Resolved by ADR-0072: explicit completed UI foundations; composition/root/session infrastructure first while preserving current playable roots. Each downstream task owns its production hookup and full feature acceptance. UI audits/accessibility remain required, and actual travel gates remain. | Tech lead |

| 23 | Save/load screen construction waits on composition that needs its navigation | Resolved: HUD-004 depends on the completed HUD/UI/save/flow contracts explicitly, so construction can proceed alongside HUD-002. Final production registration, session rebind and layout acceptance remain mandatory in the shared integration batch; no fake navigation or early done claim. | Tech lead |

| 24 | HUD-004's new-save name prompt has no display-name argument on IGameFlow, and the persistent presenter/view lifecycle is unspecified | Use SaveService.SaveAsync(slot, Manual, displayName) for named new saves and confirmed overwrites (preserving the existing title), and IGameFlow.LoadGameAsync for loading. This uses existing owning APIs without changing a shared contract. SaveService still owns capture/writeback, blockers, queueing, atomic writes and completion errors. Presenter constructor takes UIRoot, the app-owned SaveSlotsViewModel, SaveService and IGameFlow; register SaveSlotsView(model,presenter), then Bind; Tick consumes each owned Awaitable once, Refresh updates profile/slot rows, ResetSession invalidates old confirmation/prompt intents and completion UI while preserving the model, Show(mode) opens the screen, and Unbind/Dispose removes subscriptions. Confirmations capture a SlotRef/profile and recheck current operation/session access after pop; list refresh preserves exact slot identity. Use an 880×720 logical-pixel panel capped at 90% of the viewport and 88px virtual rows, with appearance exclusively from existing NOCTURNE tokens; canonical five-size acceptance remains required. Source delivery includes the five required tests using real controlled save files and UI-012's canonical UITestPanel/UILayoutAudit; audit-helper and production HUD-002 integration remain lead-owned acceptance, with no early done claim. | Tech lead — HUD-004 implementation default; central mirror pending |

| 25 | Settings construction waits on composition that requires a settings destination | Resolved: construct HUD-005 against completed HUD-001, UI-011/012/013, FND-004 and INP-003; retain all five required cases and actual production registration/lifecycle/menu navigation before closure. Lead integrates persistent model/presenter in HUD-002. No no-op destination or early completion. | Tech lead |

| 26 | Camera orientation lacks a saved settings field | Resolved via ADR-0073: bind to Controls.CameraYawModeIndex (0 FollowHeading, 1 Free); persist through real service and apply through SettingsApplier. Required HUD-005 checks cover the path. | Tech lead |

| 27 | Required rebinding acceptance needs clock control in PlayMode | Resolved ADR-0074: test-only InputSystem.TestFramework reference; advance input clock directly and restore state, preserving real behavior/assertions. | Tech lead |

| 28 | HUD-005 concrete option and presenter lifecycle defaults | One virtual row per eligible noncomposite binding in Global/Flight/Editor/Dialog, excluding mouse position/delta/scroll; each row displays its own binding index. Resolution choice 0 preserves the current display; quality choice 0 preserves the project default. Settings edits use the existing Apply + Flush API for immediate persistence; accessibility also calls UIRoot.ApplyAccessibility immediately. Constructor takes UIRoot, the app-owned SettingsViewModel, SettingsService and InputService. Register SettingsView(model,presenter), then Bind; Show refreshes and opens, Refresh handles session rebind, Unbind/Dispose cancels its own listening operation and removes subscriptions; losing the top stack position also cancels listening so a covering modal retains normal Back handling. No presenter Tick is needed; SettingsService remains ticked by GameRoot. A 960×720 panel capped at 90% uses approved theme tokens. Lead owns production composition/menu/pause hooks and Unity acceptance before closure. | Tech lead — HUD-005 implementation default; central mirror pending |

| 33 | HUD-012 selection, quantity and merchant lifecycle details are unspecified | Use the existing MerchantSession type and owner preflight checks, preserving persistent view-model identity. | Tech lead — HUD-012 delivery default. Presenter constructor takes UIRoot, MerchantViewModel, InventoryModel and Wallet; App registers MerchantView(model,presenter) and the presenter, calls Bind, ResetSession on session reset and Dispose on shutdown. No Tick is required. Category All plus the present authored item categories filters both lists; private mappings retain canonical stock indices and cargo slots including overflow. Selecting one side clears the other and sets quantity 1; refresh preserves stock item identity and cargo slot/item identity. Quantity is bounded by available stock/cargo and int.MaxValue/unit-price so the fixed int total contract cannot overflow; unlimited stock retains the owner sentinel and displays infinity. Only the economy owner mutates trade state; failed trades retain its exact reason. View uses virtual rows and the existing horizontally scrolling TabBar, a 1600x920 panel capped at 92%/90%, and existing NOCTURNE tokens. Show retains deferred pushes from pop callbacks; ResetSession/pop/disposal detach subscriptions, close the exact session, then invoke onClosed once. Exactly four specified tests include real filtered buy and sell, refused session and callback ordering, a real deferred pop-to-merchant push, ten-tab layout stress without extending the eight-category enum, and full stock/cargo scrolling at all five sizes. UIConfig has the authored merchant asset; lead owns shared UiComposition registration and the playground/launcher opening path, Unity acceptance and central mirror. |

## 11. References

- Audit `06-ui-shipeditor.md` §1.1 (17 views), §2 B/D/E bugs, §5.2; `LEDGER.md` R4, R7, R8, R11, R12, R13, R31; screenshots `02-newgame.png`, `04-after-intro.png`, `05-flying.png`, `07-ark-dialog.png`, `09-trade.png`, `10-hangar.png`, `22-inventory.png`, `23-fleet.png`.
- `Import/Legacy/ui/*.uxml` (old layouts, reference only; their styles are not reused).
- `docs/design/DESIGN-SYSTEM.md` §5.8 (HUD zones Z-BL, Z-BC, Z-TR, Z-L; final look after approval).
- Synthesis §6j.

| 29 | Inventory screen construction waits on unrelated full economy and root-screen tasks | Under ADR-0068 use accepted HUD-001, UI-003/004/005/006/007/008/012 and ECO-003/004/006. Build against real InventoryModel and existing UI root; all five cases remain required. Production root navigation remains HUD-002/010 and is not falsely completed by this screen. | Tech lead — resolved; ledger Q425 |

| 30 | Merchant screen construction waits on whole economy/root-screen specs | Under ADR-0068 use accepted HUD-001, UI-003/004/005/006/007/008/012 and ECO-003/004/005. Implement the complete view/presenter against real MerchantSession; production registration remains required, and normal interaction/navigation remains its owning task. Preserve all four required cases and refusal/close semantics. | Tech lead — resolved; ledger Q427 |
| 31 | Inventory presenter construction, fixed-pool visibility and pointer-capture lifetimes are unspecified | Accepted [ADR-0094](../decisions/ADR-0094-inventory-presenter-construction.md) records task-owned public presenter construction/Bind/Unbind/Dispose without changing existing binding APIs or assemblies. Project actual ECO-003 InventoryModel/Wallet into the same 200 existing row identities; subscribe to SlotChanged/Reset/Wallet.Changed and release old session sources on rebind. InCapacity is index less than Capacity; show every in-capacity slot and every non-empty overflow slot, hiding unused out-of-capacity slots/rows. Build twenty wrapping rows of ten 72px Slots once, preserving pool identities. Detail values are selected stack count and per-item BasePrice; wallet uses invariant thousands separators. PointerManipulator captures the primary pointer on a non-empty source and picks the destination through the actual panel at release, independent of the captured event target; delegate writes to existing Move/TryMove. Pop, detach, capture loss, cancel, Back and source item/count change release the capture; invalid/outside/same-slot drops do not write. Sort/Stack All remain header actions. Lead owns production registration/navigation and SessionReset composition. Exactly five required cases use a two-item actual cargo fixture, one initial layout frame where needed and one scroll frame; no Unity run by this worker. | Tech lead | Accepted construction defaults; ledger Q431. |

| 32 | Editor host layout depends on umbrella HUD-002 | Under ADR-0068 use accepted view models, UI services and EDT-001/021 panels. Keep production editor composition in HUD-002; all four HUD-018 acceptance cases remain required. | Tech lead — resolved; ledger Q432 |

| 34 | Dialog host view waits on umbrella root composition | ADR-0068: use completed view-model, UI service/control and dialog-command contracts. Implement the complete view and five specified cases; actual narrative driver/content remains SPEC-41, never fake a story encounter. | Tech lead — resolved; ledger Q435 |

| 35 | Ship-editor host obtains the UI root for panel mounting while its public constructor must stay unchanged | Keep ShipEditorView(vm, commands) and existing public contracts unchanged. An internal read-only UINavigator.RootOwner accessor supplies the exact same UI root that built the screen; no global service lookup, additional service registration or public API. OnBuild mounts accepted EDT-021 fragments once into four authored hud-region/hud-plate regions: top 64px, bottom 48px, left 360px and right 380px. Root/host ignore picking, with no centre element; plates remain pickable. Host plates remove inherited padding because the mounted panels own their own padding; host tool rows are 24px to leave space for the narrow-width horizontal scrollbar. Root switching stays with existing SetRoot; Back forwards to existing commands. Reuse the accepted panel worst-case fixture in exactly four required cases, including real FlightHudView/root visibility and actual UIRoot pointer-gate checks with a controlled frame guard. Lead owns production composition; no Unity run by this worker. | Tech lead | HUD-018 implementation defaults; lead acceptance/central mirror pending. |
