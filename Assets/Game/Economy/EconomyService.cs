using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Events;
using Game.Core.Flow;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Services;

namespace Game.Economy
{
    /// <summary>SPEC-50 §5.2: stable inventory/wallet/merchant models, narrative economy routing and seeded loot grants.</summary>
    public sealed class EconomyService : ILogicEconomy, INewGameInitializer, ISessionModel
    {
        readonly ContentCatalog catalog;
        readonly EconomyConfigDefinition config;
        readonly Func<ulong> campaignSeed;
        readonly List<(ItemDefinition item, int count)> rolled = new List<(ItemDefinition item, int count)>();
        bool givingLoot;
        public EconomyService(ContentCatalog catalog, EconomyConfigDefinition config, Func<ulong> campaignSeed, Func<double> universalTime)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.config = config != null ? config : throw new ArgumentNullException(nameof(config));
            this.campaignSeed = campaignSeed ?? throw new ArgumentNullException(nameof(campaignSeed));
            if (universalTime == null) throw new ArgumentNullException(nameof(universalTime));
            Inventory = new InventoryModel(catalog); Wallet = new Wallet();
            Merchants = new MerchantService(Inventory, Wallet, catalog, config, universalTime);
        }
        public InventoryModel Inventory { get; }
        public Wallet Wallet { get; }
        public MerchantService Merchants { get; }
        public int NewGameOrder => 50;
        public int GetItemCount(int itemIndex) => Inventory.CountOf(itemIndex);
        public long GetCredits() => Wallet.Credits;
        T Definition<T>(int index) where T : ContentDefinition => index > 0 && index <= ushort.MaxValue ? catalog.GetByIndex<T>((ushort)index) : null;
        public void GiveItems(int itemIndex, int count)
        {
            var item = Definition<ItemDefinition>(itemIndex);
            if (item == null) { GameLog.ErrorOnce("Economy.InvalidItem:" + itemIndex, LogCategories.Economy, $"Unknown item index {itemIndex}."); return; }
            Inventory.Give(item, count, AcquireSource.Narrative);
        }
        public bool TryTakeItems(int itemIndex, int count) => Inventory.TryTake(Definition<ItemDefinition>(itemIndex), count);
        public void GiveCredits(long amount) => Wallet.Add(amount);
        public bool TryTakeCredits(long amount) => Wallet.TrySpend(amount);
        public void GiveLoot(int lootTableIndex, int picks, ulong salt)
        {
            var table = Definition<LootTableDefinition>(lootTableIndex);
            if (table == null) { GameLog.ErrorOnce("Economy.InvalidLoot:" + lootTableIndex, LogCategories.Economy, $"Unknown loot table index {lootTableIndex}."); return; }
            // Inventory notifications allow read-only refreshes (Q23), never a nested grant that could overwrite this buffer.
            if (givingLoot) throw new InvalidOperationException("Loot grants cannot be nested.");
            givingLoot = true;
            try
            {
                LootRoller.Roll(table, picks, StableHash.Combine(campaignSeed(), salt), rolled);
                foreach (var result in rolled) Inventory.Give(result.item, result.count, AcquireSource.Loot);
            }
            finally { givingLoot = false; }
        }
        public event Action<int> ItemCountChanged { add => Inventory.ItemCountChanged += value; remove => Inventory.ItemCountChanged -= value; }
        public event Action CreditsChanged { add => Wallet.Changed += value; remove => Wallet.Changed -= value; }
        public void InitializeNewGame(in NewGameArgs args)
        {
            Inventory.ResetToEmpty(); Merchants.ResetToEmpty(); Wallet.Set(config.StartCredits);
            foreach (var entry in config.StartItems) Inventory.Give(entry.item, entry.min, AcquireSource.NewGame);
        }
        public void ResetToEmpty() { Inventory.ResetToEmpty(); Merchants.ResetToEmpty(); Wallet.Set(0); }
    }
}
