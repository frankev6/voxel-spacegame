namespace Game.Data.Logic
{
    /// <summary>Public narrative contract (SPEC-40 §4.3); later task implementations are explicitly labelled.</summary>
    public interface ILogicEconomy                     // implemented by Game.Economy.EconomyService (SPEC-50)
    {
        int GetItemCount(int itemIndex);                // itemIndex = ContentCatalog.IndexOf<ItemDefinition>
        long GetCredits();
        void GiveItems(int itemIndex, int count);       // never fails: overflow hold (SPEC-50 §5.3)
        bool TryTakeItems(int itemIndex, int count);    // all-or-nothing
        void GiveCredits(long amount);
        bool TryTakeCredits(long amount);
        void GiveLoot(int lootTableIndex, int picks, ulong salt);   // picks < 0 = table default
        event System.Action<int> ItemCountChanged;      // itemIndex
        event System.Action CreditsChanged;
    }
}
