namespace Game.UI.Views
{
    /// <summary>Screen command contract (SPEC-61 §4.3).</summary>
    public interface IInventoryCommands
    {
        void Select(int slot);
        void Move(int fromSlot, int toSlot);
        void Sort();
        void StackAll();
    }
}
