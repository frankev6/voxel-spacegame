# SPEC-50 — Economy: inventory, wallet, loot, merchants and salvage

| | |
|---|---|
| **Status** | Draft |
| **Area code** | ECO (task IDs are `ECO-001`, `ECO-002`, …) |
| **Module(s)** | `Game.Economy` (folder `Assets/Game/Economy/`: inventory, wallet, loot, merchants, save participant); `Game.Data` (`Assets/Game/Data/Definitions/Economy/`, `Assets/Game/Data/Economy/` for `ICargo`, economy fields of `ItemDefinition`); `Game.Core` (event payloads); `Game.Galaxy` (`Assets/Game/Galaxy/Salvage/`: salvage marking and salvage service); `Game.App` (merchant directory and launcher, cargo capacity sync, wiring) |
| **Depends on specs** | SPEC-04 (`ItemDefinition`, `BlockDefinition`, `ContentCatalog`, `MissingItem`, validation); SPEC-40 (`ILogicEconomy`, `OpenMerchant`); SPEC-02 (`RngStreams`, `StableHash`, `EventBus<T>`, `ISaveParticipant`, `INewGameInitializer`, `ISessionModel`, `SessionReset`); SPEC-05 (`INVT` chunk); SPEC-10 (`VoxelStructure.Instances`, `BlockInstance`, `GridSpawnApi.RequestDespawn`); SPEC-12 (`GridMass`); SPEC-13 (`Hulk`, `HulkInfo`); SPEC-31 (`Interactable`, `InteractionKind.Salvage`, `BodyInteracted`, `InteractionTarget`, `DockingService`); SPEC-33 (`FactionService`, `TerritoryService`, `EncounterDirectorService` for trade relations); SPEC-01 (`FleetModel`, construction, new game order) |
| **Depended on by** | SPEC-51 (`InventoryModel.CountOf/CanApply/TryApplyAtomic/Changed`); SPEC-61 (inventory and merchant presenters, notifications); SPEC-40 (`EconomyService` implements `ILogicEconomy`; `ItemAcquired`); SPEC-32 (fuel through `ILogicEconomy`); SPEC-33 (`LootTableDefinition`, `GiveLoot`); SPEC-70 (`give`, `inv`); SPEC-72 (MIG-013 loot tables); SPEC-05 (`INVT`) |
| **Milestone** | M3 |

> Read [01-architecture.md](../01-architecture.md) (§3, §5, §8), [02-conventions.md](../02-conventions.md), [SPEC-04](SPEC-04-content-and-blocks.md) §3 decisions 6 and 11 and §4.4, and [SPEC-40](SPEC-40-narrative-core.md) §4.3 (`ILogicEconomy`) before this spec. This spec may not contradict them.

## 1. Purpose

This spec owns everything the player carries and trades.

- **Inventory.** The `InventoryModel` is a fixed array of 200 slots whose usable **capacity** comes from the flagship's cargo blocks. Items that do not fit, or that are left over when capacity shrinks, sit in visible **overflow** slots instead of vanishing. Every change is atomic: a set of item deltas applies completely or not at all, which is what makes the ship editor's commit safe (R15). Item counts are O(1), so the HUD and narrative never scan slots.
- **Wallet.** Holds credits.
- **Loot tables.** Seeded rolls.
- **Merchants.** Authored stock that restocks on game time. Every buy is all-or-nothing, so buying 1 of 5 leaves 4 (06-A13). Prices respond to the player's relation with the local faction.
- **Salvage.** Turns hulks into block items and scrap.

`EconomyService` exposes all of this to the narrative through SPEC-40's `ILogicEconomy`.

The old prototype had these problems:
- `Player.FuelCount` scanned all 60 slots on every HUD event;
- merchants sold whole stacks;
- the editor could duplicate items;
- the toolbar and fuel were never saved.

Each has a test here.

## 2. Scope

**In scope:**
- Economy fields on `ItemDefinition`; `LootTableDefinition`, `MerchantDefinition`, `EconomyConfigDefinition` and their validators.
- `InventoryModel` (slots, capacity, overflow, atomic deltas, counts, events), `Wallet`, `ICargo`.
- `LootRoller`, `EconomyService` (`ILogicEconomy`, new game, `ItemAcquired`).
- `MerchantService` and `MerchantSession` (stock, prices, restock, buy, sell).
- `INVT` save participant.
- Salvage: `SalvageMarkSystem` (makes hulks interactable) and `SalvageService` (yields and transfer).
- App glue: `MerchantDirectory`, `MerchantLauncher`, `CargoCapacitySync`.
- M3 economy content: narrative items, `resource_scrap`, `loot_salvage_module`, one outfitter merchant, `config_economy`.

**Out of scope (and who owns it instead):**
- Inventory and merchant screens, toasts: SPEC-61 (presenters call §6).
- The ship editor's bill of materials: SPEC-51 (it calls `CanApply`/`TryApplyAtomic`).
- Fuel cost rules: SPEC-32.
- Encounter defeat loot calls: SPEC-33.
- Item and block base fields: SPEC-04. Legacy item values: SPEC-72.
- Module, officer and paint effects: inert items in v1 (SPEC-72 Q4).

## 3. Decisions (binding)

1. **The inventory has 200 slots; capacity is a prefix.**
   - Slots `0..Capacity−1` are cargo; slots `Capacity..199` are overflow.
   - `Capacity = clamp(baseCargoSlots + floor(Σ CargoCapacity of intact cargo blocks of the flagship / unitsPerSlot), baseCargoSlots, 200)`, with defaults 20 and 10. So one `block_cargo_small` (50 units) adds 5 slots, answering SPEC-04 Q8.
   - Capacity follows the flagship's committed design, recomputed on new game, after load and after an editor commit, not on battle damage.

   Reason: one slot model for UI (SPEC-61: 200 pooled slots), editor and saves; capacity changes never delete items.
2. **Two kinds of add:**
   - **Player-driven adds** (buy, salvage, editor refunds, dev `give`) use cargo slots only. They are all-or-nothing, except salvage, which takes what fits.
   - **Gives** (narrative rewards, loot, starting items) never fail. They fill cargo, then overflow slots. Anything beyond slot 199 is jettisoned with a notice and a warning.

   Reason: SPEC-40 requires gives to never fail. The player decides what to keep, and capacity still means something.
3. **Every mutation goes through `InventoryModel` methods and is atomic.**
   - `CanApply` simulates a delta set on a scratch copy.
   - `TryApplyAtomic` re-runs the same simulation and commits the scratch only if it succeeds.
   - Removal order is fixed: overflow first, highest slot first.

   Reason: R15 (the editor duplicated items); deterministic results.
4. **Counts are O(1).** An `int[]` indexed by the `ItemDefinition` family index (blocks included, SPEC-04 decision 6) is updated with every slot write. `CountOf` counts cargo and overflow. Reason: 04-persistence linear scans; SPEC-40 `item_count`.
5. **A block's item is the `BlockDefinition` itself** (SPEC-04 decision 6). There is no separate link field. Reason: one ID per block everywhere (editor, salvage, merchants).
6. **Credits are `long`** in a `Wallet` with saturating adds and all-or-nothing spends. Reason: SPEC-40 `ILogicEconomy` uses `long`.
7. **Randomness:**
   - loot: `RngStreams.For(StableHash.Combine(campaignSeed, salt), RngPurpose.Loot, 0)`;
   - salvage: `RngStreams.For(StableHash.Combine(hulkPersistentId, HulkInfo.Tick), RngPurpose.Salvage, 0)`. `Salvage = 25` is appended to SPEC-02's enum.

   Draws always happen for every entry, so content edits move only their own rows. Merchants use no randomness in v1.
8. **Merchants are global per definition.** Their stock state is saved. They restock to the authored stock when `restockHours` of `UniversalTime` have passed since the last restock, checked when a session opens. A merchant is placed by body template or by authored body (`MerchantDirectory`). Its trade relation is that of the place's owner faction, or the encounter's faction for a travelling merchant. A faction that is Hostile to the player refuses to trade. Reason: deterministic, cheap, no per-frame work.
9. **Salvage is an interaction.**
   - Non-player, non-body hulks get `Interactable { Kind = Salvage }` from `SalvageMarkSystem`. This is one batched structural change per frame, only when new hulks exist.
   - Pressing Interact computes the hulk's yields once, moves what fits into cargo, and despawns the hulk when nothing is left.
   - Hulks over `maxSalvageCells` (20,000) cannot be salvaged in M3.
   - **QA-001 (owner playtest 2026-10-14):**
     - production reach is 40 m (`config_economy.salvageDistanceM`), measured from the player's hull radius;
     - every intact `SalvageMagnet` block module (`block_salvage_magnet_small`, +20 m, sold at ARK Station Market) adds reach, capped at `salvageReachBonusCapM` (60 m), through `SalvageReach`/`SalvageReachSystem` and `InteractionQueryJob.SalvageBonusM`;
     - a completed salvage adds `SalvageCollected { Collector, Elapsed }` and removes the wreck's `Interactable`. `SalvagePullSystem` (GameInitGroup, before `GridSpawnSystem`):
       - streams up to 1,500 exposed cells as fire-and-forget debris (`FxEventFlags.Collected`, life `FxCollect.Seconds` = 0.5 s);
       - sends them in staggered waves over `FxCollect.StaggerSeconds` (0.7 s), nearest to the collector first, with 20% per-cell jitter;
       - aims each cube at its own point on the collector's hull;
       - requests the normal despawn after the last wave. Without a collector, salvage despawns at once;
     - salvageable wrecks carry a salvage-coloured world tag (SPEC-61 world overlay);
     - salvage never opens the inventory by itself.

   Reason: core loop step 4; persistent world wrecks (station hulks) stay untouched.
10. **Module boundaries.**
    - `Game.Economy` is pure managed code (no Entities, architecture §3.1). Time and seeds come in as delegates.
    - Salvage lives in `Game.Galaxy`, which reaches cargo only through `Game.Data.Economy.ICargo`.
    - Merchant placement and relations are resolved in `Game.App`.
11. **Events** (`Game.Core.Events`, managed, SPEC-02 §5.6 rule 4):
    - `ItemAcquired`: for gives and adds from narrative, loot, purchase, salvage and dev; not for editor refunds or starting items.
    - `EconomyNotice`: player-facing messages.
12. **Folders and namespaces:**
    - `Assets/Game/Economy/{Inventory,Loot,Merchants,Save}/` → `Game.Economy`;
    - `Assets/Game/Galaxy/Salvage/` → `Game.Galaxy.Salvage`;
    - `Assets/Game/App/Economy/` → `Game.App.Economy`.

## 4. Data model

All types are **public contract** unless marked *internal*.

### 4.1 Definitions (`Game.Data.Definitions`)

**`ItemDefinition` economy fields.** ECO-002 adds them below SPEC-04's marked line:

```csharp
        [UnityEngine.SerializeField] private bool tradeable = true;     // merchants never buy items with tradeable == false
        public bool Tradeable => tradeable && Category != ItemCategory.Quest;
```

**Folder `Assets/Game/Data/Definitions/Economy/`.** Property rule as in SPEC-30 §4.3.

```csharp
[System.Flags] public enum ItemCategoryMask : ushort
{ None = 0, Block = 1 << 0, Resource = 1 << 1, Consumable = 1 << 2, Module = 1 << 3, Officer = 1 << 4, Paint = 1 << 5, Quest = 1 << 6, Misc = 1 << 7 }
// bit i == ItemCategory value i (test)

[System.Serializable] public struct LootEntry
{
    public ItemDefinition item;
    public int min, max;            // 1 ≤ min ≤ max ≤ 10000
    public float chancePercent;     // 0 < chance ≤ 100
}

[ContentIdPrefix("loot_")]
[UnityEngine.CreateAssetMenu(menuName = "Game/Content/Loot Table", fileName = "loot_new")]
public sealed class LootTableDefinition : ContentDefinition
{
    [SerializeField] private System.Collections.Generic.List<LootEntry> entries = new();
    [SerializeField, UnityEngine.Range(0, 20)] private int defaultPicks;   // 0 = every entry rolls its own chance
}

[System.Serializable] public struct MerchantStockEntry
{
    public ItemDefinition item;
    public int count;               // −1 = unlimited
    public float priceFactor;       // multiplies the item's base price; 0 is read as 1
}

[System.Serializable] public struct AuthoredBodyRef { public AuthoredStarSystemDefinition starSystem; public string bodyKey; }

[ContentIdPrefix("merchant_")]
[UnityEngine.CreateAssetMenu(menuName = "Game/Content/Merchant", fileName = "merchant_new")]
public sealed class MerchantDefinition : ContentDefinition
{
    [SerializeField] private string displayName;
    [SerializeField] private System.Collections.Generic.List<MerchantStockEntry> stock = new();
    [SerializeField] private ItemCategoryMask buys = ItemCategoryMask.Block | ItemCategoryMask.Resource | ItemCategoryMask.Module;
    [SerializeField, UnityEngine.Min(0.1f)] private float sellPriceFactor = 1.0f;   // player buys at base × this
    [SerializeField, UnityEngine.Range(0f, 1f)] private float buyPriceFactor = 0.5f; // player sells at base × this
    [SerializeField, UnityEngine.Min(0)] private float restockHours = 24f;          // game hours; 0 = never restocks
    [SerializeField] private System.Collections.Generic.List<BodyTemplateDefinition> bodyTemplates = new();   // every body of these templates
    [SerializeField] private System.Collections.Generic.List<AuthoredBodyRef> authoredBodies = new();          // specific authored bodies
}

[ContentIdPrefix("config_")]
public sealed class EconomyConfigDefinition : ContentDefinition       // single asset config_economy
{
    // Inventory
    [SerializeField] private int baseCargoSlots = 20;
    [SerializeField] private float unitsPerSlot = 10f;
    // New game
    [SerializeField] private long startCredits = 2000;
    [SerializeField] private System.Collections.Generic.List<LootEntry> startItems = new();   // min used as the count; chance ignored
    // Trade
    [SerializeField] private float relationPriceSlope = 0.002f;       // per relation point, clamped to ±50 points (±10 %)
    // Salvage
    [SerializeField] private float salvageFunctionalChance = 0.5f;
    [SerializeField] private float salvageStructuralChance = 0.1f;
    [SerializeField] private int salvageMaxStructuralItems = 50;
    [SerializeField] private ItemDefinition scrapItem;                // resource_scrap
    [SerializeField] private float scrapPerCell = 0.02f;
    [SerializeField] private int salvageMaxScrap = 500;
    [SerializeField] private int maxSalvageCells = 20000;
    [SerializeField] private float salvageDistanceM = 120f;
    [SerializeField] private AudioCueDefinition salvageCue;           // cue_ui_success placeholder
}
```

Validation codes:

| Code | Rule |
|---|---|
| ECO-E001 [E] | Loot entries: item set; `1 ≤ min ≤ max ≤ 10000`; `0 < chancePercent ≤ 100`; `defaultPicks` 0..20 |
| ECO-E002 [E] | Merchant stock: item set; `count == −1 \|\| count ≥ 1`; `priceFactor ≥ 0`; no duplicate items |
| ECO-E003 [E] | Merchant `authoredBodies`: star system set and `bodyKey` names one of its authored bodies |
| ECO-E004 [E] | A body template or authored body is claimed by at most one merchant |
| ECO-E005 [E] | Exactly one `EconomyConfigDefinition` (`config_economy`). Its values: `1 ≤ baseCargoSlots ≤ 200`; `unitsPerSlot > 0`; `startCredits ≥ 0`; `startItems` valid as loot entries; chances in [0, 1]; `scrapItem` and `salvageCue` set; positive caps and distances |
| ECO-W001 [W] | A `Quest` item marked `tradeable` (ignored: quest items never trade) |
| ECO-W002 [W] | A merchant stock item with `BasePrice == 0` (sold for 1 credit) |

### 4.2 Event payloads (`Assets/Game/Core/Events/`, namespace `Game.Core.Events`)

```csharp
namespace Game.Core.Events
{
    public enum AcquireSource : byte { Narrative = 0, Loot = 1, Purchase = 2, Salvage = 3, EditorRefund = 4, Dev = 5, NewGame = 6 }  // add at end only

    /// <summary>Managed. Published for Narrative, Loot, Purchase, Salvage and Dev sources only (decision 11).</summary>
    public readonly struct ItemAcquired
    {
        public readonly ContentHash Item; public readonly int ItemIndex; public readonly int Count; public readonly AcquireSource Source;
        public ItemAcquired(ContentHash item, int itemIndex, int count, AcquireSource source) { Item = item; ItemIndex = itemIndex; Count = count; Source = source; }
    }

    public enum EconomyNoticeKind : byte { CargoFull = 0, Jettisoned = 1, SalvageDone = 2, SalvagePartial = 3, SalvageRefused = 4, TradeRefused = 5 }

    /// <summary>Managed, player-facing (SPEC-61 notifications).</summary>
    public readonly struct EconomyNotice
    {
        public readonly EconomyNoticeKind Kind; public readonly string Text;
        public EconomyNotice(EconomyNoticeKind kind, string text) { Kind = kind; Text = text ?? ""; }
    }
}
```

### 4.3 Inventory and wallet (`Assets/Game/Economy/Inventory/`, namespace `Game.Economy`)

```csharp
namespace Game.Economy
{
    using Game.Core; using Game.Data.Definitions; using Game.Core.Events;

    /// <summary>One slot. Item == null means empty. For a Missing placeholder, OriginalId keeps the unknown ID for re-save.</summary>
    public struct ItemStack
    {
        public ItemDefinition Item;
        public int Count;                 // 1..Item.MaxStack when Item != null
        public ContentId OriginalId;      // default unless Item == catalog.MissingItem
        public bool IsEmpty => Item == null;
    }

    public readonly struct ItemDelta { public readonly ContentId Item; public readonly int Count;   // + add, − remove; 0 ignored
        public ItemDelta(ContentId item, int count) { Item = item; Count = count; } }
    public readonly struct ItemShortfall { public readonly ContentId Item; public readonly int Missing;
        public ItemShortfall(ContentId item, int missing) { Item = item; Missing = missing; } }

    /// <summary>The player's cargo. Implements SPEC-50 §5.1. Main thread only.</summary>
    public sealed class InventoryModel : Game.Data.Economy.ICargo
    {
        public const int MaxSlots = 200;
        public InventoryModel(Game.Data.Services.ContentCatalog catalog);
        public int Capacity { get; }                                   // decision 1
        public int UsedSlots { get; }                                  // non-empty slots in 0..199
        public ItemStack Get(int slot);                                // ArgumentOutOfRange outside 0..199
        public int CountOf(ContentId item);                            // cargo + overflow; 0 for unknown
        public int CountOf(int itemIndex);                             // ICargo / ILogicEconomy
        public void SetCapacity(int capacity);                         // clamps to [1, 200]; never moves items; raises Reset

        // Player-driven, all-or-nothing into cargo slots (decision 2)
        public bool TryAdd(ContentId item, int count, out string reason);                     // source Dev (SPEC-70)
        public bool TryAdd(ItemDefinition item, int count, AcquireSource source, out string reason);
        public bool CanAddAll(ItemDefinition item, int count);
        public int AddUpTo(int itemIndex, int count, AcquireSource source);                  // ICargo: as many as fit in cargo; returns added
        // Gives: never fail (decision 2)
        public void Give(ItemDefinition item, int count, AcquireSource source);
        // Removal
        public bool TryTake(ItemDefinition item, int count);                                 // all-or-nothing, removal order of §5.1
        public bool TryTakeFromSlot(int slot, int count);                                    // merchant sell
        // Atomic delta sets (SPEC-51)
        public bool CanApply(System.ReadOnlySpan<ItemDelta> deltas, System.Collections.Generic.List<ItemShortfall> shortfalls, out bool cargoFull);
        public bool TryApplyAtomic(System.ReadOnlySpan<ItemDelta> deltas);                   // additions use AcquireSource.EditorRefund
        // Organisation (SPEC-61)
        public bool TryMove(int fromSlot, int toSlot);
        public void Sort();
        public void StackAll();
        // Session
        public void ResetToEmpty();                                    // all slots empty, counts 0, capacity unchanged; raises Reset
        internal void RestoreSlot(int slot, ItemDefinition item, int count, ContentId originalId); // INVT restore only

        public event System.Action<int> SlotChanged;                   // slot index
        public event System.Action Reset;                              // bulk change: restore, new game, sort, capacity change
        public event System.Action Changed;                            // after any change (SPEC-51 BOM refresh)
        public event System.Action<int> ItemCountChanged;              // item family index (ILogicEconomy)
    }

    /// <summary>Credits. Main thread only.</summary>
    public sealed class Wallet
    {
        public long Credits { get; }
        public void Add(long amount);                  // amount ≥ 0; saturates at MaxCredits; negative → ArgumentOutOfRange
        public bool TrySpend(long amount);             // amount ≥ 0; false and no change when Credits < amount
        public void Set(long credits);                 // restore and new game only; clamps to [0, MaxCredits]
        public const long MaxCredits = 1_000_000_000_000L;
        public event System.Action Changed;
    }
}

namespace Game.Data.Economy   // Assets/Game/Data/Economy/ICargo.cs
{
    /// <summary>Narrow cargo access for modules below Game.Economy (salvage in Game.Galaxy). Implemented by InventoryModel.</summary>
    public interface ICargo
    {
        int Capacity { get; }
        int CountOf(int itemIndex);
        int AddUpTo(int itemIndex, int count, Game.Core.Events.AcquireSource source);
    }
}
```

### 4.4 Loot, merchants and the economy service (`Game.Economy`)

```csharp
namespace Game.Economy
{
    public static class LootRoller   // §5.3; pure
    {
        public static void Roll(Game.Data.Definitions.LootTableDefinition table, int picks, ulong seed,
                                System.Collections.Generic.List<(Game.Data.Definitions.ItemDefinition item, int count)> into);
    }

    public static class PriceMath    // §5.4; pure
    {
        public static int BuyPrice(int basePrice, float entryFactor, float merchantSellFactor, int relation, float slope);   // ≥ 1
        public static int SellPrice(int basePrice, float merchantBuyFactor, int relation, float slope);                    // ≥ 0
    }

    public sealed class MerchantStockState
    {
        public Game.Data.Definitions.ItemDefinition Item;
        public int Count;                 // −1 unlimited
        public float PriceFactor;
        public bool Buyback;              // added by player sales; removed at restock
        public Game.Core.ContentId OriginalId;   // Missing placeholder support
    }

    /// <summary>One open trade. Created by MerchantService.OpenSession; invalid after Close.</summary>
    public sealed class MerchantSession
    {
        public Game.Data.Definitions.MerchantDefinition Merchant { get; }
        public bool Refused { get; }                    // hostile owner: no trades
        public string RefusalReason { get; }            // "" unless Refused
        public int Relation { get; }
        public System.Collections.Generic.IReadOnlyList<MerchantStockState> Stock { get; }
        public int BuyPriceOf(int stockIndex);
        public int SellPriceOf(Game.Data.Definitions.ItemDefinition item);  // 0 when the merchant does not buy it
        public bool CanBuy(int stockIndex, int quantity, out string reason);
        public bool TryBuy(int stockIndex, int quantity, out string reason);
        public bool CanSell(int slot, int quantity, out string reason);
        public bool TrySell(int slot, int quantity, out string reason);
        public event System.Action Changed;             // stock, wallet or cargo changed through this session
        public void Close();                            // idempotent
    }

    /// <summary>Merchant stock states and sessions. Implements SPEC-50 §5.4.</summary>
    public sealed class MerchantService
    {
        public MerchantService(InventoryModel inventory, Wallet wallet, Game.Data.Services.ContentCatalog catalog,
                               Game.Data.Definitions.EconomyConfigDefinition config, System.Func<double> universalTime);
        public MerchantSession OpenSession(Game.Data.Definitions.MerchantDefinition merchant, int relation, bool refuse, string refusalReason);
        public MerchantSession Current { get; }         // null when none is open
        public void ResetToEmpty();                     // forgets every stock state
        internal void RestoreState(/* INVT */);
    }

    /// <summary>ILogicEconomy facade, new-game initializer and session model. Implements SPEC-50 §5.2.</summary>
    public sealed class EconomyService : Game.Data.Logic.ILogicEconomy, Game.Core.Flow.INewGameInitializer, Game.Core.Flow.ISessionModel
    {
        public EconomyService(Game.Data.Services.ContentCatalog catalog, Game.Data.Definitions.EconomyConfigDefinition config,
                              System.Func<ulong> campaignSeed, System.Func<double> universalTime);
        public InventoryModel Inventory { get; }
        public Wallet Wallet { get; }
        public MerchantService Merchants { get; }
        public int NewGameOrder => 50;
        // ILogicEconomy (SPEC-40 §4.3)
        public int GetItemCount(int itemIndex);
        public long GetCredits();
        public void GiveItems(int itemIndex, int count);                 // Inventory.Give(…, Narrative)
        public bool TryTakeItems(int itemIndex, int count);
        public void GiveCredits(long amount);
        public bool TryTakeCredits(long amount);
        public void GiveLoot(int lootTableIndex, int picks, ulong salt); // §5.3, source Loot
        public event System.Action<int> ItemCountChanged;
        public event System.Action CreditsChanged;
        // Lifecycle
        public void InitializeNewGame(in Game.Core.Flow.NewGameArgs args);   // §5.2
        public void ResetToEmpty();
    }

    /// <summary>INVT (SPEC-05 order 60, version 1). §5.5.</summary>
    public sealed class InventorySaveParticipant : Game.Core.Save.ISaveParticipant { public InventorySaveParticipant(EconomyService economy, Game.Data.Services.ContentCatalog catalog); }
}
```

### 4.5 Salvage (`Assets/Game/Galaxy/Salvage/`, namespace `Game.Galaxy.Salvage`)

```csharp
namespace Game.Galaxy.Salvage
{
    /// <summary>Structural, WorldGroup (§5.6). Adds Interactable(Salvage) to new salvageable hulks in one batch per frame.</summary>
    public partial class SalvageMarkSystem : Unity.Entities.SystemBase { }

    public struct SalvageYield { public int ItemIndex; public int Count; }

    /// <summary>Handles Salvage interactions. Implements SPEC-50 §5.7. Main thread.</summary>
    public sealed class SalvageService : System.IDisposable
    {
        public SalvageService(Unity.Entities.World world, Game.Data.Services.ContentCatalog catalog, Game.Data.Definitions.EconomyConfigDefinition config,
                              Game.Core.Audio.IAudioService audio);
        public bool TrySalvage(Unity.Entities.Entity hulkRoot, out string message);   // also called by the BodyInteracted handler
        public static void ComputeYields(Unity.Entities.DynamicBuffer<Game.Voxel.BlockInstance> instances, uint solidCells, ulong seed,
                                         Game.Data.Services.ContentCatalog catalog, Game.Data.Definitions.EconomyConfigDefinition config,
                                         System.Collections.Generic.List<SalvageYield> into);   // pure, deterministic
        public int PendingWrecks { get; }             // hulks with leftover yields
        public void Dispose();                        // unsubscribes
    }
}
```

*Internal:* `SalvageLedger`, a `Dictionary<ulong, List<SalvageYield>>` keyed by the hulk's `PersistentId`. It is cleared on scene exit, and an entry is dropped when its hulk no longer exists (checked on access).

### 4.6 App glue (`Assets/Game/App/Economy/`, namespace `Game.App.Economy`)

```csharp
namespace Game.App.Economy
{
    /// <summary>Which merchant trades at a body (decision 8). Built once from the catalog.</summary>
    public sealed class MerchantDirectory
    {
        public MerchantDirectory(Game.Data.Services.ContentCatalog catalog, Game.Galaxy.Services.GalaxyService galaxy);
        public Game.Data.Definitions.MerchantDefinition FindForBody(Game.Galaxy.Data.BodyId body);   // authored body first, then template; null if none
    }

    /// <summary>Opens the merchant screen with the right relation. Used by the dock screen and SPEC-40 OpenMerchant.</summary>
    public sealed class MerchantLauncher
    {
        public MerchantLauncher(Game.Economy.EconomyService economy, MerchantDirectory directory, Game.Galaxy.Services.FactionService factions,
                                Game.Galaxy.Services.TerritoryService territory, Game.Galaxy.Services.EncounterDirectorService encounters,
                                Game.Galaxy.Services.StarSystemLoader loader, Game.UI.Services.UINavigator navigator);
        public bool TryOpenForBody(Game.Galaxy.Data.BodyId body, System.Action onClosed);          // false when no merchant
        public void Open(Game.Data.Definitions.MerchantDefinition merchant, System.Action onClosed, ulong encounterMemberPersistentId = 0);
    }

    /// <summary>Recomputes InventoryModel.Capacity from the flagship structure (decision 1).</summary>
    public sealed class CargoCapacitySync : Game.Core.Flow.INewGameInitializer, System.IDisposable
    {
        public CargoCapacitySync(Game.Economy.InventoryModel inventory, Game.App.Session.FleetModel fleet, Game.Data.Services.ContentCatalog catalog,
                                 Game.Data.Definitions.EconomyConfigDefinition config);
        public int NewGameOrder => 40;                                    // before EconomyService (50)
        public void Recompute();                                          // also on SessionReset and FleetModel.Changed
        public static int ComputeCapacity(Game.Voxel.VoxelStructure flagship, Game.Data.Services.ContentCatalog catalog,
                                          Game.Data.Definitions.EconomyConfigDefinition config);   // pure
        public void InitializeNewGame(in Game.Core.Flow.NewGameArgs args);
        public void Dispose();
    }
}
```

## 5. Systems, services and flow

### 5.1 `InventoryModel` algorithms

**State:**
- `ItemStack[] slots` (200);
- `int[] counts`, indexed by `catalog.IndexOf<ItemDefinition>` (size = family count + 1);
- `int capacity`;
- a reusable scratch copy of `slots` and `counts` for simulations.

All writes go through `WriteSlot(i, stack)`, which:
1. updates `counts` by the difference;
2. raises `SlotChanged(i)` and `ItemCountChanged(index)` for every item whose count changed.

`Changed` is raised once per public mutating call, at its end. During `Reset`-raising operations (`SetCapacity`, `Sort`, restore, `ResetToEmpty`), the per-slot events are suppressed and `Reset` plus one `ItemCountChanged` per touched item are raised instead.

**Fill rules**, for one item `I` with `count` units, applied to an array:
1. **Top up stacks:** for `s` in `0..limit−1` ascending, if `slots[s].Item == I && Count < MaxStack`: add `min(room, remaining)`.
2. **Use empty slots:** for `s` in `0..limit−1` ascending, if `slots[s]` is empty: put `min(MaxStack, remaining)`.

`limit` is `Capacity` for player-driven adds, and 200 for the overflow phase of `Give` (which runs steps 1–2 with `limit = Capacity` first, then again over `Capacity..199`).

**Removal rule**, for one item `I` with `count` units:
1. visit slots from 199 down to 0 (overflow first, highest first);
2. take `min(slots[s].Count, remaining)` from each slot holding `I`;
3. a stack that reaches 0 becomes empty.

**Operations:**

| Operation | Rule |
|---|---|
| `TryAdd(item, n, source, out reason)` | `n ≤ 0` → false "Invalid count". Simulate the fill with `limit = Capacity` on scratch. Leftover > 0 → false, "Cargo full". Otherwise copy scratch into the real slots through `WriteSlot`. Publish `ItemAcquired` per decision 11 |
| `TryAdd(ContentId, n, out reason)` | Resolve with `catalog.TryGet<ItemDefinition>`; unknown → false "Unknown item"; then source `Dev` |
| `CanAddAll(item, n)` | Scratch simulation only |
| `AddUpTo(itemIndex, n, source)` | Fill with `limit = Capacity` and stop when full. Returns the added count; publishes `ItemAcquired` for it when it is > 0 |
| `Give(item, n, source)` | Fill with `limit = Capacity`, then over overflow slots. Any rest is jettisoned: `EconomyNotice(Jettisoned, "Hold full: {n} × {name} jettisoned.")` and `GameLog.Warn`. Publishes `ItemAcquired` for the stored amount |
| `TryTake(item, n)` | `CountOf(item) < n` → false. Otherwise the removal rule |
| `TryTakeFromSlot(slot, n)` | The slot's count `< n` → false. Otherwise reduce that slot only |
| `CanApply(deltas, shortfalls, out cargoFull)` | See below |
| `TryApplyAtomic(deltas)` | Same simulation on scratch. On success, write every changed slot through `WriteSlot` and return true; otherwise return false with no change. Additions publish nothing (`EditorRefund`) |
| `TryMove(from, to)` | Both in 0..199, `from` non-empty, `to < Capacity` or `to == from`. **Same item:** merge up to `MaxStack` and leave the rest in `from`. **Different item or empty:** swap, which is allowed even when `from ≥ Capacity` (the displaced stack goes to overflow). Invalid → false |
| `Sort()` | See below |
| `StackAll()` | For each item, in ascending order of the first slot that holds it: merge later partial stacks of that item into earlier ones (cargo slots first), keeping slot order |
| `SetCapacity(c)` | `c = clamp(c, 1, 200)`; if it changed: set it, raise `Reset`, `Changed` |

**`CanApply(deltas, shortfalls, out cargoFull)`:**
1. Clear `shortfalls`; `cargoFull = false`.
2. Resolve each `ItemDelta.Item`. An unknown id is reported as a shortfall with `Missing = |Count|` when it is a removal; unknown additions are ignored with a `WarnOnce`.
3. Sum the deltas per item.
4. Removals: for each item with a negative net delta, if `CountOf < −net`, add a shortfall `(item, −net − have)`.
5. If any shortfall exists, return false.
6. On scratch: apply all removals (removal rule), then all additions (fill rule, `limit = Capacity`) in delta order. Any leftover → `cargoFull = true`; return false.
7. Return true.

**`Sort()`:**
1. Collect all non-empty stacks and merge them per item (order `(Category, DisplayName ordinal, Id ordinal)`).
2. Clear all slots.
3. Write the merged amounts back in that order using the fill rule, with `limit = 200`. Items that no longer fit in cargo continue into overflow; nothing is lost because the total slots needed never grow.

Every operation is O(200) slots plus O(distinct items) at worst and allocates nothing after warm-up: scratch arrays are kept, and `deltas` is a span.

### 5.2 `EconomyService`

**`InitializeNewGame(args)`**, order 50, after `CargoCapacitySync` (40):
1. `Inventory.ResetToEmpty()`; `Merchants.ResetToEmpty()`.
2. `Wallet.Set(config.StartCredits)`.
3. For each `startItems` entry: `Inventory.Give(item, entry.min, AcquireSource.NewGame)`. No `ItemAcquired` event is published.

**`ResetToEmpty()`**: the same as step 1 plus `Wallet.Set(0)`.

**`ILogicEconomy` members:**
- `GetItemCount(i)`: `Inventory.CountOf(i)`.
- `GiveItems(i, n)`: `def = catalog.GetByIndex<ItemDefinition>((ushort)i)`; null → `GameLog.ErrorOnce`. Otherwise `Inventory.Give(def, n, Narrative)`.
- `TryTakeItems`: `Inventory.TryTake`.
- `GiveCredits(a)`: `Wallet.Add`.
- `TryTakeCredits(a)`: `Wallet.TrySpend`.
- `GiveLoot(t, picks, salt)`:
  1. `table = catalog.GetByIndex<LootTableDefinition>((ushort)t)`;
  2. `LootRoller.Roll(table, picks, StableHash.Combine(campaignSeed(), salt), list)`;
  3. `Inventory.Give(item, count, Loot)` for each result in order.
- `ItemCountChanged` and `CreditsChanged` forward the inventory and wallet events.

### 5.3 `LootRoller.Roll`

`rng = RngStreams.For(seed, RngPurpose.Loot, 0)`; `p = picks > 0 ? picks : table.DefaultPicks`.
- **Independent** (`p == 0`): for each entry in list order, always draw `u = rng.NextFloat()` and `n = rng.NextInt(min, max + 1)`. If `u × 100 < chancePercent`, append `(item, n)`.
- **Picks** (`p > 0`): `total = Σ chancePercent`. For `k` in `0..p−1`:
  - draw `u`;
  - choose the first entry whose cumulative chance `> u × total`;
  - draw `n = rng.NextInt(min, max + 1)`;
  - append `(item, n)`.

  Entries may repeat.
- An empty table or `total == 0` yields nothing (`WarnOnce`).
- Results with the same item are merged in first-appearance order.

### 5.4 Merchants

**Prices**, with `r = clamp(relation, −50, 50)`:
- `PriceMath.BuyPrice = max(1, (int)Math.Round(basePrice × (entryFactor == 0 ? 1 : entryFactor) × merchantSellFactor × (1 − slope × r), MidpointRounding.AwayFromZero))`;
- `SellPrice = max(0, (int)Math.Floor(basePrice × merchantBuyFactor × (1 + slope × r)))`.

With the default slope 0.002, +50 relation gives −10 % on purchases and +10 % on sales.

**`OpenSession(m, relation, refuse, reason)`**:
1. Close `Current` if one is open.
2. Get or create the state for `m`: create from the authored stock with `LastRestock = universalTime()`.
3. **Restock check:** if `restockHours > 0 && universalTime() − LastRestock ≥ restockHours × 3600`:
   - remove `Buyback` entries;
   - reset every authored entry's `Count` to its authored value;
   - add missing authored entries in authored order;
   - `LastRestock = universalTime()`.
4. Create the session with `Refused = refuse` and `Current = session`.

**`CanBuy(i, q)`**: checks in order; the first failing check gives the reason:
1. `Refused` → `RefusalReason`.
2. `i` out of range → "Not in stock".
3. `q < 1` → "Invalid quantity".
4. `Count != −1 && q > Count` → "Only {Count} in stock".
5. `total = (long)BuyPriceOf(i) × q`; `Wallet.Credits < total` → "Not enough credits: {total} needed".
6. `!Inventory.CanAddAll(item, q)` → "Cargo full".

**`TryBuy(i, q)`**: run `CanBuy`, then:
1. `Wallet.TrySpend(total)`;
2. `Inventory.TryAdd(item, q, Purchase, out _)`. This cannot fail after `CanAddAll` on the same frame. If it does fail, refund the credits and `GameLog.Error`.
3. If `Count != −1`: `Count −= q`, and remove the entry when it reaches 0 and is `Buyback`.
4. Raise `Changed`.

**`CanSell(slot, q)`**:
1. `Refused` → its reason.
2. Empty slot → "Nothing to sell".
3. `!item.Tradeable` → "Cannot sell this item".
4. `buys` does not contain the item's category → "{merchant} does not buy {category}".
5. `q < 1 || q > slot count` → "Invalid quantity".
6. `SellPriceOf(item) == 0` → "No value".

**`TrySell(slot, q)`**: run `CanSell`, then:
1. `Inventory.TryTakeFromSlot(slot, q)`;
2. `Wallet.Add(price × q)`;
3. merge into the stock: an existing entry with the same item and `Count != −1` gets `Count += q`; else, if no entry has the item, append `Buyback` `{ item, q, factor 1 }` (an unlimited entry stays unlimited);
4. raise `Changed`.

### 5.5 `INVT` participant (order 60, version 1)

**Capture:**
1. `WriteInt64(Credits)`.
2. Slots: `WriteVarUInt32(nonEmpty)`, then per slot in ascending index: `WriteUInt16(slot)`, `WriteContentId(id)`, `WriteInt32(count)`. `id` is `OriginalId` for Missing placeholders, else `Item.Id`.
3. Merchants: `WriteVarUInt32(count)`, sorted by merchant id ordinal. Per merchant:
   - `WriteContentId(merchantId)`;
   - `WriteDouble(LastRestock)`;
   - `WriteVarUInt32(entries)`, then per entry in list order: `WriteContentId(itemId)`, `WriteInt32(Count)`, `WriteSingle(PriceFactor)`, `WriteBool(Buyback)`.

Capacity is not saved (derived, decision 1).

**Restore(reader, fromVersion):**
- `fromVersion == 0` → `ResetToEmpty` and stop.
- Otherwise:
  1. `ResetToEmpty`; read in the same order.
  2. Items resolve with `catalog.ResolveItemOrMissing(id, "INVT")`; Missing keeps `OriginalId`. A count is clamped to `[1, MaxStack]` (the excess is dropped with `GameLog.Warn`).
  3. A slot index ≥ 200 is dropped with a warning.
  4. An unknown merchant is kept raw and written back unchanged; unknown stock items become Missing entries.

The round trip is byte-identical.

### 5.6 `SalvageMarkSystem` (structural; `WorldGroup`, after `InteractionQuerySystem`)

- `OnCreate`: `RequireForUpdate<GameplayActive>`, `<WorldSceneActive>`, `<EconomyConfigRef>`. `EconomyConfigRef { float SalvageDistanceM; }` is a singleton created by SPEC-01 BootSingletons from `config_economy` (item 85).

  Build `newHulks = WithAll<Hulk, VoxelGrid, PersistentId>().WithNone<Interactable, PlayerShip, BodyGridOwner, GridSpawning>()`.
- `OnUpdate`:
  1. If `newHulks.IsEmpty`, return. This is the common case and costs no structural work.
  2. `EntityManager.AddComponent<Interactable>(newHulks)` (one batch).
  3. A Burst `IJobEntity` over the same entities (`WithAll<Hulk>`, `Interactable` with `Kind == None`) sets `Interactable { Kind = Salvage, TemplateIndex = 0xFFFF, DistanceM = SalvageDistanceM, Body = default }`. Complete it in the same update; the structural change already synchronised.

Hulks that SPEC-31 spawns for destroyed bodies carry `BodyGridOwner`, so they are never salvageable in M3.

### 5.7 `SalvageService`

**`BodyInteracted` handler**, subscribed in the constructor:
1. Act only when `e.Kind == (byte)InteractionKind.Salvage`.
2. Read `InteractionTarget` (`CompleteDependencyBeforeRO`). If `Target` exists and its `PersistentId == e.GridPersistentId`, call `TrySalvage(Target, out msg)`.
3. Otherwise scan `WithAll<Hulk, PersistentId>` for that id (O(hulks)) and call `TrySalvage` if found.

**`TrySalvage(root)`**:
1. **Checks**, each refusing with `EconomyNotice(SalvageRefused, …)`:
   - the entity exists and has `Hulk`, without `PlayerShip` or `BodyGridOwner` → otherwise "Nothing to salvage.";
   - `GridMass.Data.CellCount > maxSalvageCells` → "Too large to salvage.";
   - no `ICargo` registered → "No cargo hold.".
2. **Yields** (first salvage of this `PersistentId`):
   - `em.CompleteDependencyBeforeRO<BlockInstance>()`;
   - `seed = StableHash.Combine(pid, HulkInfo.Tick)`;
   - `ComputeYields(buffer, CellCount, seed, …, list)`;
   - store `list` in the ledger.
3. **Transfer:** for each yield in order, `added = cargo.AddUpTo(ItemIndex, Count, Salvage)`, then `Count −= added`.
4. **Result:**
   - every count is 0: `GridSpawnApi.RequestDespawn(em, root)` (SPEC-10), remove the ledger entry, `audio.PlayCue(salvageCue)`, `EconomyNotice(SalvageDone, "Salvaged {n} items.")`;
   - otherwise keep the entry and publish `EconomyNotice(SalvagePartial, "Cargo full: salvage left on the wreck.")`. The player can return.

**`ComputeYields`** (pure). `rng = RngStreams.For(seed, RngPurpose.Salvage, 0)`; `structuralTaken = 0`.
1. For each instance `i` in buffer order with `Block != 0`:
   1. `def = catalog.GetBlock(Block)`; always draw `u = rng.NextFloat()`.
   2. `intact = (Flags & Destroyed) == 0 && CellsAlive == def.Cells.Length`.
   3. Skip unless `intact`.
   4. `functional = def.Kind == BlockKind.Functional`; `chance = functional ? salvageFunctionalChance : salvageStructuralChance`.
   5. If `u < chance` and (`functional || structuralTaken < salvageMaxStructuralItems`): add `(IndexOf<ItemDefinition>(def), 1)`; if not functional, `structuralTaken++`.
2. Scrap: `scrap = min(salvageMaxScrap, (int)floor(solidCells × scrapPerCell))`; add `(IndexOf(scrapItem), scrap)` when it is > 0.
3. Merge equal items in first-appearance order.

Palette (single-cell) blocks give only scrap, which keeps the cost O(instances) and never O(cells).

### 5.8 App glue

**`MerchantDirectory.FindForBody(body)`**:
1. If the body's star system is authored and the body is authored (`galaxy` resolves its authored key): return the merchant whose `authoredBodies` contains `(starSystem, key)`.
2. Otherwise: `template = galaxy.GetBodyTemplate(content.Bodies[i].TemplateIndex)`, and return the merchant whose `bodyTemplates` contains `template`.
3. Otherwise null.

Both maps are dictionaries built once.

**`MerchantLauncher.Open(m, onClosed, member)`**:
1. **Relation and refusal**, in this order:
   - `member != 0` (a travelling merchant hailed in space): `encounters.TryGetInstanceByMember(member, out info)` → its faction;
   - else the faction from `territory.GetOwner(loader.Loaded)`;
   - else no faction: relation 0, not refused.

   With a faction: `relation = factions.GetRelation(f)`, `refuse = factions.GetPlayerStance(f) == Hostile`, reason "They refuse to trade with you.".
2. `session = economy.Merchants.OpenSession(m, relation, refuse, reason)`.
3. `Services.Get<MerchantPresenter>().Show(session, onClosed)` (SPEC-61 §6): it pushes `MerchantView`, closes the session when the screen pops, then calls `onClosed`.

**`TryOpenForBody(body, onClosed)`**: `m = directory.FindForBody(body)`; return false when null, otherwise `Open`.

SPEC-40's `OpenMerchant(merchantIndex, scope, onClosed)` is routed to `Open(catalog.GetByIndex<MerchantDefinition>(i), onClosed, member: 0)` (ECO-008).

**`CargoCapacitySync`**:
- `ComputeCapacity(structure)`: `units = Σ` over `structure.Instances` with `Block != 0`, `(Flags & Destroyed) == 0`, a `CargoModule` (SPEC-04 §4.6: `catalog.GetBlock(Block).TryGetModule(out CargoModule m)`) of `m.Params.Capacity`. The result is `clamp(baseCargoSlots + floor(units / unitsPerSlot), baseCargoSlots, 200)`.
- `Recompute()`: `inventory.SetCapacity(fleet.Flagship != null ? ComputeCapacity(fleet.Flagship.Structure, …) : baseCargoSlots)`.

  It is called:
  - by `InitializeNewGame` (order 40);
  - on `EventBus<SessionReset>`;
  - on SPEC-01's `FleetModel.Changed` (raised after every add, remove, structure replacement — the SPEC-51 commit — and reset). The constructor subscribes; `Dispose` unsubscribes.

## 6. Public API

| Call | Pre-conditions | Post-conditions / errors |
|---|---|---|
| `InventoryModel.CountOf` | — | O(1) |
| `InventoryModel.TryAdd` / `CanAddAll` / `AddUpTo` | Main thread | Cargo slots only (decision 2) |
| `InventoryModel.Give` | Main thread | Never fails; overflow, then jettison with a notice |
| `InventoryModel.CanApply` / `TryApplyAtomic` | Main thread | Atomic (decision 3); `TryApplyAtomic == false` → no slot changed |
| `InventoryModel.TryMove` / `Sort` / `StackAll` | Main thread | Count of every item unchanged |
| `Wallet.Add` / `TrySpend` | `amount ≥ 0` | Saturating / all-or-nothing |
| `MerchantSession.TryBuy` / `TrySell` | Session open | All-or-nothing; reasons per §5.4 |
| `EconomyService` as `ILogicEconomy` | Main thread | SPEC-40 §4.3 semantics |
| `SalvageService.TrySalvage` | Main thread, World scene | §5.7 |
| `MerchantLauncher.Open` / `TryOpenForBody` | Main thread, World scene | Merchant screen pushed |
| `LootRoller.Roll`, `PriceMath.*`, `ComputeYields`, `ComputeCapacity` | — | Pure, deterministic |

### Editor rollback checkpoint (ADR-0096)

`InventoryModel.Checkpoint CaptureCheckpoint()` captures the bounded slots and capacity. `RestoreCheckpoint(Checkpoint)` validates the originating owner and restores slots, counts, capacity and normal notifications synchronously. These APIs support exact rejected editor transactions; they are not a save format.

## 7. Edge cases and failure behaviour

| Situation | Required behaviour |
|---|---|
| Editor commit needs 4 hull, the player has 3 (R15) | `CanApply` → shortfall `(block_hull, 1)`; `TryApplyAtomic` changes nothing |
| Editor commit refunds 10 blocks into a full hold | `cargoFull`; nothing changes (SPEC-51 shows "Cargo full") |
| Buy 1 from a stack of 5 (06-A13) | Stock 4, wallet −price, cargo +1 |
| Buy with exactly enough credits but no cargo room | Refused "Cargo full"; wallet unchanged |
| Narrative gives 50 items into a full hold | Overflow slots used; items beyond slot 199 jettisoned with a notice |
| Capacity drops after a load (cargo block lost) | Items stay; slots ≥ capacity show as overflow; cannot move items into overflow |
| Load a save containing a removed item | Missing placeholder in that slot with the original ID; saved again unchanged |
| Sell a quest item | Refused "Cannot sell this item" |
| Hostile faction's station merchant | Session opens refused with the reason; buy and sell disabled |
| Salvage a 30,000-cell hulk | Refused "Too large to salvage." |
| Salvage with a nearly full hold | Takes what fits; the hulk stays with the rest; a later salvage continues; the same totals as one salvage into an empty hold |
| The hulk is destroyed further before the second salvage | Ledger kept (yields fixed at the first salvage) |
| Player's split-off section becomes a hulk | Salvageable (not `PlayerShip`) |
| Station destroyed (body hulk) | Not salvageable in M3 (`BodyGridOwner`) |
| `GiveLoot` with an empty table | Nothing; one warning |
| Merchant restock while its session is open | Not checked until the next `OpenSession` |

## 8. Performance budget

| Item | Target | Test (task) |
|---|---|---|
| `CountOf` | O(1), 0 B | `InventoryModelTests.CountOf_NoAlloc` (ECO-003) |
| `TryApplyAtomic` with 40 deltas on a full 200-slot hold | ≤ 0.05 ms, 0 B after warm-up | `InventoryModelPerfTests.Apply40` (ECO-003) |
| `SalvageMarkSystem` with no new hulks | ≤ 0.005 ms (query empty check) | `SalvageTests.Mark_NoHulks_Cost` (ECO-007) |
| `ComputeYields` for a 20,000-cell hulk with 2,000 instances | ≤ 0.5 ms main thread | `SalvageTests.Yields_2000Instances` (ECO-007) |
| `INVT` capture with 200 stacks and 5 merchants | ≤ 0.2 ms | `InventorySaveTests.Capture_Time` (ECO-006) |

**Scale notes.**
- The inventory is bounded at 200 slots, and counts are an array over the item family (hundreds of entries).
- Merchants do work only when a session opens.
- Salvage is O(instances) once per hulk. Palette cells only contribute through the cached `GridMass.Data.CellCount`.
- The marking system does structural work only in frames where new hulks appear, as one batch.
- Rejected:
  - scanning slots for counts;
  - per-frame restock;
  - per-cell salvage;
  - `AddComponent` per entity.

## 9. Tasks

### ECO-001 — Public contract types
- **Milestone:** M3
- **Tier:** standard — transcribes the exact code of §4 (bodies throw `NotImplementedException`)
- **Depends on:** `CORE-001`, `DATA-001`, `NAR-001`, `VOX-001`, `DMG-001`, `SYS-001`, `FLT-001`
- **Do:**
  1. Create every type of §4 in the folders of decision 12.
  2. Make sure `RngPurpose.Salvage = 25` exists (SPEC-02 §4.2 lists it; add it if CORE-001 predates it, append-only).
  3. Add the `tradeable` field below SPEC-04's marked line.
- **Files:**
  - `Assets/Game/Data/Definitions/Items/ItemDefinition.cs` (below the line);
  - `Assets/Game/Data/Definitions/Economy/{ItemCategoryMask,LootEntry,LootTableDefinition,MerchantStockEntry,AuthoredBodyRef,MerchantDefinition,EconomyConfigDefinition}.cs`;
  - `Assets/Game/Data/Economy/ICargo.cs`;
  - `Assets/Game/Core/Events/{AcquireSource,ItemAcquired,EconomyNoticeKind,EconomyNotice}.cs`;
  - `Assets/Game/Economy/Inventory/{ItemStack,ItemDelta,ItemShortfall,InventoryModel,Wallet}.cs`;
  - `Assets/Game/Economy/Loot/LootRoller.cs`;
  - `Assets/Game/Economy/Merchants/{PriceMath,MerchantStockState,MerchantSession,MerchantService}.cs`;
  - `Assets/Game/Economy/{EconomyService}.cs`;
  - `Assets/Game/Economy/Save/InventorySaveParticipant.cs`;
  - `Assets/Game/Galaxy/Salvage/{SalvageMarkSystem,SalvageYield,SalvageService,EconomyConfigRef}.cs`;
  - `Assets/Game/App/Economy/{MerchantDirectory,MerchantLauncher,CargoCapacitySync}.cs`;
  - `Assets/Game/Core/…/RngPurpose.cs` (append).
- **Acceptance criteria:**
  - [ ] Compiles. `Game.Economy` references only Core and Data (asmdef check).
  - [ ] Bit `i` of `ItemCategoryMask` equals `1 << (int)(ItemCategory)i`.
- **Tests:** `EconomyContractTests.EconomyAsmdef_ReferencesCoreDataOnly`, `.CategoryMask_MatchesEnum`.

### ECO-002 — Definitions, config and validators
- **Milestone:** M3
- **Tier:** standard — rule table in §4.1
- **Depends on:** `ECO-001`, `SPEC-04`
- **Do:**
  1. Implement the properties and `Validate` methods, and `EconomyContentValidator` (ECO-E001…E005, W001, W002).
  2. Create `config_economy.asset` with the §4.1 defaults:
     - `startItems`: `resource_fuel` 50, `block_hull` 40, `block_hull_angled` 20;
     - `scrapItem` `resource_scrap` (created in ECO-009);
     - `salvageCue` `cue_ui_success`.
- **Files:** `Assets/Game/Data/Definitions/Economy/*.cs`; `Assets/Game/Editor/Content/Validators/EconomyContentValidator.cs`; `Assets/Game/Content/Config/config_economy.asset`; `Assets/Game/Tests/EditMode/Economy/EconomyValidationTests.cs`
- **Acceptance criteria:**
  - [ ] Each code has a failing and a passing fixture.
  - [ ] `Tradeable` is false for every `Quest` item.
- **Tests:** `EconomyValidationTests.<Code>_Fails`, `.<Code>_Passes`, `.QuestItems_NotTradeable`.

### ECO-003 — InventoryModel and Wallet
- **Milestone:** M3
- **Tier:** frontier — F3: atomic multi-item updates whose violation silently duplicates or deletes items (R15)
- **Depends on:** `ECO-001`
- **Do:** Implement `InventoryModel` (§5.1, every operation of the table) and `Wallet`.
- **Files:** `Assets/Game/Economy/Inventory/{InventoryModel,Wallet,ItemStack}.cs`; `Assets/Game/Tests/EditMode/Economy/{InventoryModelTests,InventoryModelPerfTests,WalletTests}.cs`
- **Acceptance criteria:**
  - [ ] **Property test:** 10,000 random seeded operations (add, give, take, apply, move, sort, stack, capacity changes) keep these invariants after every step:
    - `counts` equals a recount of the slots;
    - no stack exceeds `MaxStack` or is ≤ 0 while non-empty;
    - `TryMove`, `Sort` and `StackAll` never change a count;
    - a failed `TryApplyAtomic`, `TryAdd` or `TryTake` changes nothing (snapshot compare).
  - [ ] R15 shortfall and cargo-full cases of §7 behave as listed.
  - [ ] Gives overflow, then jettison with exactly one notice.
  - [ ] `CountOf_NoAlloc` and `Apply40` meet §8.
- **Tests:** `InventoryModelTests.RandomOps_Invariants`, `.CanApply_Shortfall`, `.CanApply_CargoFull`, `.ApplyAtomic_NoPartial`, `.Give_OverflowThenJettison`, `.Capacity_DropKeepsItems`, `.Move_IntoOverflowRefused`, `.Sort_KeepsCounts`, `.Events_PerSlotAndCounts`, `.CountOf_NoAlloc`; `InventoryModelPerfTests.Apply40`; `WalletTests.Saturates`, `.Spend_AllOrNothing`.

### ECO-004 — Loot and EconomyService
- **Milestone:** M3
- **Tier:** standard — fixed draw order and routing (§5.2, §5.3)
- **Depends on:** `ECO-003`, `ECO-005`, `NAR-001`
- **Do:** Implement `LootRoller` and `EconomyService`, including `ILogicEconomy`, new game, reset and `ItemAcquired` publishing per decision 11.
- **Files:** `Assets/Game/Economy/Loot/LootRoller.cs`; `Assets/Game/Economy/EconomyService.cs`; `Assets/Game/Tests/EditMode/Economy/{LootRollerTests,EconomyServiceTests}.cs`
- **Acceptance criteria:**
  - [ ] The same seed gives identical results.
  - [ ] Adding a new last entry to a table does not change the earlier entries' results in independent mode.
  - [ ] Picks mode follows the chance weights within ±2 % over 100,000 seeded rolls.
  - [ ] New game gives the start credits and items without `ItemAcquired`.
  - [ ] Narrative gives and loot publish `ItemAcquired` with the right source.
  - [ ] `TryTakeItems` is all-or-nothing.
- **Tests:** `LootRollerTests.Deterministic`, `.AppendEntry_StableEarlier`, `.Picks_Distribution`, `.EmptyTable_Warns`; `EconomyServiceTests.NewGame_Start`, `.Give_PublishesAcquired`, `.TakeItems_AllOrNothing`, `.Loot_UsesCampaignSeed`.

### ECO-005 — Merchants
- **Milestone:** M3
- **Tier:** standard — formulas and checks in §5.4
- **Depends on:** `ECO-003`
- **Do:** Implement `PriceMath`, `MerchantService` (states, restock) and `MerchantSession` (§5.4).
- **Files:** `Assets/Game/Economy/Merchants/*.cs`; `Assets/Game/Tests/EditMode/Economy/MerchantTests.cs`
- **Acceptance criteria:**
  - [ ] **Buying** (06-A13):
    - buying 1 of a limited 5 leaves 4;
    - "Not enough credits" and "Cargo full" change nothing;
    - an unlimited entry stays unlimited.
  - [ ] **Selling:**
    - adds credits and a buyback entry, which is removed at restock;
    - quest items and categories the merchant does not buy are refused.
  - [ ] **Prices:**
    - relation +50 gives exactly 10 % off a 100-credit item (90);
    - −50 gives 110;
    - `BuyPrice ≥ 1` always.
  - [ ] **Restock:** happens only after `restockHours` of game time.
  - [ ] **Refusal:** a refused session blocks all trades with the reason.
- **Tests:** `MerchantTests.BuyOne_StockDecrements`, `.Buy_Failures_NoChange`, `.Unlimited_Stays`, `.Sell_Buyback_RemovedAtRestock`, `.Sell_Refusals`, `.Prices_Relation`, `.Restock_GameTime`, `.Refused_BlocksTrades`.

### ECO-006 — INVT participant
- **Milestone:** M3
- **Tier:** frontier — F3: binary save format with Missing-item preservation
- **Depends on:** `ECO-004`, `ECO-005`, `PERS-001`
- **Do:** Implement `InventorySaveParticipant` (§5.5) and the SPEC-05 registry row `INVT`.
- **Files:** `Assets/Game/Economy/Save/InventorySaveParticipant.cs`; `Assets/Game/Tests/EditMode/Economy/InventorySaveTests.cs`
- **Acceptance criteria:**
  - [ ] Round trip byte-identical with 200 stacks, overflow slots, a Missing item, two merchants (one unknown) and buyback entries.
  - [ ] Restore into a non-empty model fully replaces it.
  - [ ] Capture time meets §8.
- **Tests:** `InventorySaveTests.RoundTrip_ByteIdentical`, `.MissingItem_Preserved`, `.UnknownMerchant_Preserved`, `.Restore_Replaces`, `.Capture_Time`.

### ECO-007 — Salvage
- **Milestone:** M3
- **Tier:** frontier — F1/F5: a structural system outside the fixed step, plus ledger state across interactions and despawns
- **Depends on:** `ECO-003`, `DMG-012`, `SYS-008`, `PHY-013`, `VOX-014` (ADR-0104)
- **Do:** Implement `SalvageMarkSystem` (§5.6), `SalvageService` (§5.7) and the `EconomyConfigRef` boot singleton factory (`EconomyConfigRefFactory.Create(def)`). Add a ledger note to SPEC-01's BootSingletons.
- **Files:** `Assets/Game/Galaxy/Salvage/{SalvageMarkSystem,SalvageService,SalvageLedger,EconomyConfigRef,EconomyConfigRefFactory}.cs`; `Assets/Game/Tests/PlayMode/Galaxy/SalvageTests.cs`
- **Acceptance criteria:**
  - [ ] A new non-player hulk gets `Interactable(Salvage)` within one frame; body hulks and the player's root never do.
  - [ ] Salvaging a hulk with 3 intact thrusters and 5,000 cells:
    - yields thruster items according to the seeded chance and 100 scrap;
    - the same seed gives the same yields;
    - the hulk despawns when everything fits.
  - [ ] With 2 free slots: a partial transfer, then a second salvage after making room completes it. The totals equal a single salvage into an empty hold.
  - [ ] A 30,000-cell hulk is refused.
  - [ ] Perf rows of §8 met.
- **Tests:** `SalvageTests.Mark_NewHulksOnly`, `.Mark_NoHulks_Cost`, `.Yields_Deterministic`, `.Yields_2000Instances`, `.Partial_ThenComplete`, `.TooLarge_Refused`, `.Despawn_WhenEmpty`.

### ECO-008 — App glue and wiring
- **Milestone:** M3
- **Tier:** standard — lookups and calls specified in §5.8
- **Depends on:** `ECO-011`, `ECO-004`, `ECO-005`, `ECO-007`, `ENC-004`, `ENC-005`, `ENC-007`, `FND-005`, `FND-009`, `HUD-012` (ADR-0104)
- **Do:**
  1. Implement `MerchantLauncher` (§5.8); use the directory/capacity owners implemented in ECO-011.
  2. Wire `LogicWorldAdapter.OpenMerchant` and remove it from `UnwiredMembers`.
  3. Implement SPEC-01 §5.2 rows 8.4 (economy, `INVT`, `CargoCapacitySync`, `SalvageService`, `MerchantDirectory`) and 10.1 (`MerchantLauncher`, which needs the UI navigator) exactly as written there, with the registrations listed in those rows.
- **Files:** `Assets/Game/App/Economy/{MerchantDirectory,MerchantLauncher,CargoCapacitySync}.cs`; `Assets/Game/App/Services/LogicWorldAdapter.cs`; `Assets/Game/App/Boot/GameComposition.cs`; `Assets/Game/Tests/EditMode/App/EconomyWiringTests.cs`
- **Acceptance criteria:**
  - [ ] A Hostile owner's merchant opens refused.
  - [ ] `open_merchant` calls `onClosed` after the screen pops.
- **Tests:** `EconomyWiringTests.Launcher_HostileRefused`, `.OpenMerchant_CallsOnClosed`.

### ECO-009 — M3 economy content
- **Milestone:** M3
- **Tier:** standard — content authoring from the tables below
- **Depends on:** `ECO-002`, `SPEC-72`
- **Do:** Create the following assets. Placeholder icons: SPEC-72's sheet sprite `spritetest2_transparent_0` unless a legacy icon exists.
  1. **Narrative items** (SPEC-40 §4.13 and the legacy flag table): category `Quest`, rarity `Rare`, `maxStack 1`, price 0, except where noted.

     | ID | Display name | Notes |
     |---|---|---|
     | `item_ai_core_data` | AI Core Data | |
     | `item_ancient_data_log` | Ancient Data Log | |
     | `item_blueprint_glass_beam` | Glass Beam Blueprint | |
     | `item_blueprint_vanguard_plating` | Vanguard Plating Blueprint | |
     | `item_genetic_stimulants` | Genetic Stimulants | Resource, stack 99, price 150 |
     | `item_mining_beam_mk2` | Mining Beam Mk II | |
     | `item_psycho_gas` | Psycho Gas | Resource, stack 99, price 80 |
     | `item_reality_anchor` | Reality Anchor | |
     | `item_research_data` | Research Data | Resource, stack 999, price 40 |
     | `item_scrap_cannon` | Scrap Cannon | |
     | `item_seed_bank` | Seed Bank | |
     | `item_stealth_patch` | Stealth Patch | |
     | `item_terraforming_seed` | Terraforming Seed | |
     | `item_vanguard_thruster` | Vanguard Thruster | |
  2. **`resource_scrap`** ("Scrap", Resource, Common, price 2, stack 999).
  3. **`loot_salvage_module`**, picks 1: `module_armor_plating` 30, `module_coolant_injector` 30, `module_micro_plating` 30, `module_shield_efficiency` 20, each 1–1.
  4. **`merchant_station_market`** ("ARK Station Market"; the ID SPEC-40's converter emits for the station hub's TRADE):
     - at authored body `starsys_ark_start/ark_station` (SPEC-42 §4.12);
     - stock: `resource_fuel` unlimited, `block_hull` 200, `block_hull_angled` 100, `block_thruster_small` 10, `block_cargo_small` 6, `block_shield_small` 4, `block_reactor_small` 2, `weapon_plasma_turret_small` 4, `weapon_small_beam_turret` 2;
     - buys Block, Resource, Module; `restockHours` 24.
  5. **`merchant_shady_trader`** ("Shady Trader"; emitted for `event_merchant_shady`'s TRADE):
     - no location (opened only from that dialog through `open_merchant`);
     - stock: `resource_fuel` 40, `module_coolant_injector` 2, `module_micro_plating` 2, `item_psycho_gas` 3;
     - buys Resource, Module, Misc; `sellPriceFactor` 1.3, `buyPriceFactor` 0.6, `restockHours` 48.
- **Files:** `Assets/Game/Content/Items/*.asset`; `Assets/Game/Content/Loot/loot_salvage_module.asset`; `Assets/Game/Content/Merchants/{merchant_station_market,merchant_shady_trader}.asset`
- **Acceptance criteria:**
  - [ ] Zero ECO errors.
  - [ ] Every item, loot table and merchant ID that SPEC-40's converted content uses (`give_item`, `take_item`, `has_item`, `give_loot`, `open_merchant`) exists.
- **Tests:** `EconomyContentSmokeTests.NarrativeItemsExist`, `.ContentValidates`.

### ECO-010 — Economy integration scenarios
- **Milestone:** M3
- **Tier:** standard — PlayMode tests of specified behaviour
- **Depends on:** `ECO-006`, `ECO-008`, `ECO-009`, `TST-001`
- **Do:** Add a PlayMode suite on the M3 content:
  - **Trade:** dock at `ark_station`, open the station market through `TryOpenForBody`, buy 3 hull, sell 10 scrap, close.
  - **Persistence:** save and load, then the wallet, cargo and merchant stock are identical.
  - **Salvage:** destroy a raider's core, salvage it, and `inv("resource_scrap") > 0`.
  - **Capacity:** commit a design with 1 extra cargo block through SPEC-51's commit path; capacity +5.
  - **R15 (SPEC-51 + SPEC-50):** a cancelled edit leaves every count unchanged.
- **Files:** `Assets/Game/Tests/PlayMode/Economy/EconomyIntegrationTests.cs`
- **Acceptance criteria:**
  - [ ] All five scenarios pass.
- **Tests:** `EconomyIntegrationTests.Trade_BuySell`, `.SaveLoad_Identical`, `.Salvage_Raider`, `.Commit_CapacityGrows`, `.R15_CancelledEdit_NoChange`.

### ECO-011 — Merchant directory and fleet cargo capacity
- **Milestone:** M3
- **Tier:** standard — existing lookup and capacity algorithms in §5.8
- **Depends on:** `ECO-004`, `ECO-005`, `GAL-008`, `FND-010`
- **Do:** Extract the independently usable MerchantDirectory and CargoCapacitySync owners from ECO-008. Follow §4 contracts and §5.8 exactly; register through the existing composition. No faction/encounter stand-ins.
- **Files:** `Assets/Game/App/Economy/{MerchantDirectory,CargoCapacitySync}.cs`, `Assets/Game/App/Boot/GameComposition.cs`, `Assets/Game/Tests/EditMode/App/EconomyWiringTests.cs`
- **Acceptance criteria:**
  - [ ] Authored body merchant wins over template; no match returns null.
  - [ ] Two cargo-small blocks produce capacity 30; replacing flagship structure with one extra block adds five slots via the real fleet event.
  - [ ] Initialization and disposal use the prescribed owner lifecycle.
- **Tests:** Existing required cases transferred from ECO-008: `EconomyWiringTests.Capacity_FollowsFleetChanged`, `.Directory_AuthoredFirst`, `.Capacity_TwoCargoBlocks`. No additional cases.

## 10. Open questions

| # | Question | Default if unanswered | Decider |
|---|---|---|---|
| 1 | Cargo unit and base capacity | 20 base slots; 10 units per slot; `block_cargo_small` +5 | Owner |
| 2 | Starting credits and cargo | 2,000 credits; 50 fuel, 40 hull, 20 angled hull | Owner |
| 3 | Salvage pacing: instant or channelled | Instant on Interact (v1) | Owner |
| 4 | Do battle losses of cargo blocks shrink capacity immediately? | No: capacity follows the committed design (decision 1) | Owner |
| 5 | Should modules, officers and paint do anything? | Inert in v1 (SPEC-72 Q4) | Owner |
| 6 | Requested changes to other specs | `_INTEGRATION-TODO.md` items 76–85 | Tech lead |
| 7 | ECO-001's MerchantLauncher constructor names FactionService, TerritoryService and EncounterDirectorService before ENC-001 creates them; voxel type paths in §4.5/4.6 omit the Model namespace | Stage only that exact constructor until ENC-001/ECO-008; include MerchantLauncher and its stated Open/TryOpenForBody shells now, without substitute services or new dependencies. Preserve Game.Voxel.Model.BlockInstance and Game.Voxel.Model.VoxelStructure as the canonical owning types in salvage/capacity signatures. | Tech lead |
| 8 | The economy asmdef already contains shared Unity.Collections/Unity.Mathematics references; definition getters, INVT interface members and internal restore parameters are abbreviated | Preserve the existing asmdef and enforce the Core/Data-only game-module boundary as AssemblyRulesTests does, also forbidding Entities in Economy. Include trivial read-only definition getters (lists as IReadOnlyList), INVT Key/Version = INVT/1 and required capture/restore shells. Keep RestoreState parameterless only as the declared internal placeholder; ECO-006 supplies its concrete restore shape. | Tech lead |
| 9 | ItemAcquired is unmanaged but has only managed publishers; the abstract SalvageMarkSystem shell would either fail compilation or run a throwing OnUpdate | Classify ItemAcquired as managed-only in the existing bridge test exclusion list, retaining its invariant guard and every existing queue registration. Include an OnUpdate shell with DisableAutoCreation until ECO-007 supplies the structural implementation, group/order and runtime gates; do not auto-run unfinished behavior. SalvageLedger and EconomyConfigRefFactory remain ECO-007 implementation details, not invented state in ECO-001. | Tech lead |
| 20 | ECO-002 requires config_economy's scrapItem, but canonical resource_scrap is not created until dependent ECO-009 | **Resolved:** bring forward only resource_scrap in ECO-002's lead-run generation job: Scrap, Resource/Common, price 2, stack 999, existing migration-sheet sprite spritetest2_transparent_0 per ECO-009's icon rule. Preflight canonical references and candidate validation before writes; preserve existing assets/GUIDs, never substitute items or weaken ECO-E005. Report this as partial ECO-009 content; all other narrative/loot/merchant content remains ECO-009. | Tech lead — resolved 2026-10-13 |
| 21 | Finite values, merchant scalar bounds and startItems' ignored chance are implicit in the rule table/annotations | Reject nonfinite floats under their existing owning codes. ECO-E002 also enforces annotated sellPriceFactor >= 0.1, buyPriceFactor in [0,1] and restockHours >= 0. ECO-E005 requires finite nonnegative relationPriceSlope/scrapPerCell, finite [0,1] salvage chances, positive units/caps/distance and the stated credit/slot bounds. startItems still validate as LootEntry rows; generation writes min=max=count and chancePercent=100 even though new game uses only min. Empty lists are valid; malformed null row lists fail. Preserve every stated default. | Tech lead |
| 22 | Where should ECO-W001 read the hidden authored quest flag, and how should duplicate placement scopes behave? | EconomyContentValidator (Order 500, normal TypeCache discovery) reads the raw serialized tradeable flag for ECO-W001; no new ItemDefinition API or base-field changes. The existing getter always prevents quest trade. Definition methods own local E001/E002/E003/E005 and W002; the database validator owns config count/ID, E004 and W001. Repeated placement by the same merchant is not a multi-owner violation. Authored-body and template maps are separate, so the intended authored override of a template merchant remains valid. | Tech lead |

| 23 | Inventory constructor capacity, invalid no-op inputs, event re-entry, missing-item stack identity and integer overflow are unspecified | **Resolved ECO-003:** start at the documented default capacity20 until CargoCapacitySync sets it. Unknown/nonpositive gives add nothing; invalid/nonpositive take/add requests fail without mutation. Commit all slot/count state before notifications, with one Changed at the end; reject mutations from notification callbacks while allowing read-only CanApply/CanAddAll refreshes. Unchanged ordinary calls emit nothing; explicit Sort/ResetToEmpty still emit Reset/Changed. Merge Missing placeholders only when OriginalId also matches, preserving separate removed content IDs through sorting and moves. Accumulate delta totals in long; keep each item total representable by the public int count contract (excess is Cargo full for atomic adds, or jettisoned for Give); cap an unrepresentable shortfall report at int.MaxValue. RestoreSlot validates stack size and count bounds before changing state. Wallet fires Changed only when its balance changes. | Tech lead |

| 24 | ECO-005 leaves closed-session behavior, notification re-entry, price/count overflow and invalid clock/catalog inputs implicit. | Keep all public contracts and §5.4 check ordering. Closed sessions reject trades with "Session closed"; Close is idempotent and clears Current only for that exact session. Opening a session closes its predecessor; stock is keyed by the canonical merchant family index and restocked in authored order from one finite game-clock sample. Reject unknown merchant definitions/nonfinite time before closing Current; a backwards clock never restocks. PriceMath uses the stated authored float expressions, AwayFromZero for buys and Floor for sales; reject nonfinite/negative float arguments and saturate nonrepresentable prices at int.MaxValue before conversion. Multiply quantities in long. Reject a sale that would overflow a finite stock count with "Stock full" before any mutation. Follow the existing wallet-spend/cargo-add/refund and cargo-remove/wallet-add sequences; guard nested merchant trades from model/session callbacks with "Trade in progress", while retaining read-only preflight refreshes under Q23. Opening/resetting during a trade is invalid; Close remains safe. Session Changed fires once after the completed trade; no external-model subscriptions. Keep the existing ECO-006 restore placeholder for its owning task. Exactly the eight required MerchantTests, zero extra tests/optimization passes; lead focused Unity validation pending. | Tech lead — ECO-005 default; ledger mirror pending. |

| 25 | ECO-004 waits on the entire narrative spec despite using only its economy interface | **Resolved:** under ADR-0068, require ECO-003 inventory/wallet, ECO-005 merchants and NAR-001 ILogicEconomy. Loot/service routing uses existing Core/Data utilities and needs no narrative runtime. All functional scope and eight required tests remain. | Tech lead |

| 26 | ECO-004 does not state caller-list reuse, integer-index wrapping, malformed runtime rows or facade event ownership. | Roll clears the supplied list before each result, merges duplicate items with checked int totals in first-appearance order, and uses double cumulative weights for picks. Follow §5.3 exactly: nonpositive override uses DefaultPicks; independent rows always draw chance then inclusive quantity even on failure, while each weighted pick draws selection then quantity. Runtime row checks reject null items, invalid 1..10000 ranges and nonfinite/out-of-range chances; zero weights remain a supported no-result/WarnOnce case from §5.3, without weakening ECO-E001 content validation. Empty/zero-total warnings are once per table ID. Facade lookup rejects indices outside 1..65535 before ushort conversion: invalid gives log ErrorOnce, invalid takes return false, counts use the full int index and return 0 when absent. Borrow catalog/config and seed/time delegates, keep stable inventory/wallet/merchant instances across lifecycle resets, preserve CargoCapacitySync's capacity and use only startItems.min with NewGame source. Forward count/credit event add/remove directly to models; no owned subscriptions or new lifetime API. Guard nested loot grants before overwriting the reusable result list, consistent with Q23's read-only notification refreshes. Exactly eight required tests, zero extra tests/gameplay ticks/waits or optimization passes; weighted distribution retains 100,000 seeded one-pick rolls and 2% relative-frequency bounds. Lead focused compilation/acceptance and ledger mirror pending. | Tech lead — ECO-004 default. |

| 27 | INVT registry says capacity is saved, merchant restore shape is unspecified, and byte identity conflicts with clamp/drop repairs | **Resolved:** INVT v1 follows SPEC-50 §5.5: capacity is derived and excluded; correct SPEC-05 registry prose without changing bytes. Merchant state is keyed internally by ContentId, so unknown merchants retain restock time and ordered rows without opening/restocking a session. Unknown merchants preserve all original row IDs, including known item aliases. Missing placeholders cannot be bought or sold, preventing identity loss through ordinary item-only trade APIs. Preserve stock counts (including unlimited), price float bits and buyback flags; resolve missing items while retaining OriginalId. Resolve known merchant aliases to canonical IDs before duplicate detection/opening; unknown IDs remain unchanged. Byte identity applies to canonical valid payloads within current item stack limits; the specified inventory clamp/drop repairs intentionally change invalid or newly over-limit data. Reject known-merchant counts below -1 (unknown merchant rows stay raw), unsupported versions, duplicate slots/merchants, empty merchant IDs, impossible counts and trailing data as SaveFormatException; version zero resets models. No public API or chunk layout change. | Tech lead |

| 28 | ECO-011 lookup and capacity lifetime defaults | Keep the canonical APIs, current galaxy body identity and prescribed owner events; no launcher/faction substitutes. | Tech lead — delivery default. Directory builds ordinal first-wins authored-location and template maps once, verifies the current generated body, then uses GalaxyService.GetLocationKey for authored precedence and the current content template for fallback; absent galaxy, unknown body/system and no placement return null. Capacity sums each nonfree, nondestroyed block instance with CargoModule once, including multi-cell blocks, in double precision; floor slots and clamp before integer conversion. Borrow committed fleet/native content/config, validate finite capacity divisors/contributions, and treat null/disposed structures as the configured base. Constructor recomputes before subscribing, new-game order is40, FleetModel.Changed and SessionReset recompute synchronously, and idempotent disposal removes both subscriptions. Composition owns/disposes/registers the synchronizer and registers the directory beside the existing economy service, without UiComposition/playground/launcher edits. Exactly the three transferred EconomyWiringTests use tiny real structures/catalog/galaxy, actual cargo-small footprints, fleet replacement and direct reset events; no extra cases, Unity runs or performance tuning. Central task extraction/claim and Unity acceptance remain lead-owned. |

| 29 | Exact rejected editor transaction rollback | Inventory-owned opaque checkpoint restores slots and overflow even when normal capacity is full; inverse deltas are insufficient. ADR-0096; ledger Q452. | Tech lead | Resolved. |

| 30 | Exact marking batch, partial ledger lifetime and salvage rounding | Snapshot the new-hulk query before its structural AddComponent, then use one Burst IJob over precisely that snapshot and a ComponentLookup; this preserves the specified batched mark without touching unrelated existing interactions. Check complete required hulk state and refuse pending-despawn roots to prevent repeated harvest before owner cleanup. Ledger entries bind entity generations as well as persistent IDs; prune dead roots on access and clear only on successful World exit or SessionReset, not failed transitions. Block reentrant transfers. Scrap multiplication/floor uses float arithmetic as authored (5,000 × 0.02 yields100); merge items in first-appearance order with a dictionary for O(instances). Two-free-slot partial fixture uses three distinct intact thruster types, since identical items can stack into one slot. The two advisory fixtures live in SalvagePerfTests, excluded from default focused runs. | Tech lead — resolved; ledger Q479 |

| 31 | ECO-008 launcher lifetime, missing routes and actual dock entry | Preserve the exact constructor and §5.8 precedence: a supplied encounter member resolves only its real instance faction (unresolved member has no faction); body/direct opens use the owner of loader.Loaded. No faction means relation0 and no refusal; Hostile uses the exact prescribed refusal text. Resolve the registered real presenter/view before creating a session; presenter owns close-before-callback and replacement sessions. OpenMerchant resolves the one-based merchant index and forwards the continuation to the launcher; invalid definition or missing launcher logs once and completes the continuation rather than trapping a dialog. Register the existing inventory/ICargo/wallet/merchant owners, own/dispose SalvageService, keep one existing economy/directory/capacity/INVT instance and its live SimClock-backed time delegate (session.UniversalTime is only refreshed at writeback), then register the launcher after real UI/presenter construction. Retain HUD-019 delivery as a separate commit; bind its actual DockPresenter/view to completed merchant/editor launchers and update its existing availability assertions to require the real production presenter/launcher bindings, adding no HUD cases. Exactly Launcher_HostileRefused and OpenMerchant_CallsOnClosed extend EconomyWiringTests using real galaxy/faction/territory/loader/director/spawner/audio/input/UI/presenter owners and bounded direct calls; no substitute game owners, Unity runs or extra ECO tests. Test-owned territory is disposed before galaxy/factions/world when its accepted lifetime API is present; lead preserves its production Push registration when reconciling parallel composition changes. | Tech lead — worker defaults; focused Unity validation and central mirror remain lead-owned. |

## 11. References

- Audit (background only): `docs/reference/audit-2026-10/06-ui-shipeditor.md` (06-A13 merchant stack bug, 06-D1 last slot), `04-persistence-data.md` §12 (linear scans, unsaved fuel and toolbar), `LEDGER.md` R12, R15.
- SPEC-04 §3 decisions 6 and 11, §4.4, §4.10 (blocks, cargo capacity); SPEC-40 §4.3 (`ILogicEconomy`), §5.5 (`OpenMerchant`); SPEC-51 §5.7, §5.9 (BOM and commit); SPEC-72 §4.5 (legacy item values), §4.6 (loot tables).
