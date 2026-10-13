namespace Game.Economy
{
    /// <summary>SPEC-50 contract shell; ECO-004 owns behavior.</summary>
    public sealed class EconomyService : Game.Data.Logic.ILogicEconomy, Game.Core.Flow.INewGameInitializer, Game.Core.Flow.ISessionModel
    {
        public EconomyService(Game.Data.Services.ContentCatalog catalog, Game.Data.Definitions.EconomyConfigDefinition config,
                              System.Func<ulong> campaignSeed, System.Func<double> universalTime) => throw new System.NotImplementedException("ECO-004");
        public InventoryModel Inventory => throw new System.NotImplementedException("ECO-004");
        public Wallet Wallet => throw new System.NotImplementedException("ECO-004");
        public MerchantService Merchants => throw new System.NotImplementedException("ECO-004");
        public int NewGameOrder => 50;
        // ILogicEconomy (SPEC-40 §4.3)
        public int GetItemCount(int itemIndex) => throw new System.NotImplementedException("ECO-004");
        public long GetCredits() => throw new System.NotImplementedException("ECO-004");
        public void GiveItems(int itemIndex, int count) => throw new System.NotImplementedException("ECO-004");                 // Inventory.Give(…, Narrative)
        public bool TryTakeItems(int itemIndex, int count) => throw new System.NotImplementedException("ECO-004");
        public void GiveCredits(long amount) => throw new System.NotImplementedException("ECO-004");
        public bool TryTakeCredits(long amount) => throw new System.NotImplementedException("ECO-004");
        public void GiveLoot(int lootTableIndex, int picks, ulong salt) => throw new System.NotImplementedException("ECO-004"); // §5.3, source Loot
        public event System.Action<int> ItemCountChanged { add => throw new System.NotImplementedException("ECO-004"); remove => throw new System.NotImplementedException("ECO-004"); }
        public event System.Action CreditsChanged { add => throw new System.NotImplementedException("ECO-004"); remove => throw new System.NotImplementedException("ECO-004"); }
        // Lifecycle
        public void InitializeNewGame(in Game.Core.Flow.NewGameArgs args) => throw new System.NotImplementedException("ECO-004");   // §5.2
        public void ResetToEmpty() => throw new System.NotImplementedException("ECO-004");
    }
}
