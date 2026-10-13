namespace Game.UI.Views
{
    /// <summary>Screen command contract (SPEC-61 §4.3).</summary>
    public interface IMerchantCommands
    {
        void SelectCategory(int index);
        void SelectStock(int index);
        void SelectCargo(int index);
        void SetQuantity(int quantity);
        void Buy();
        void Sell();
    }
}
