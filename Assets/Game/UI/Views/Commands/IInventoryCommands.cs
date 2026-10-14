namespace Game.UI.Views
{
    /// <summary>Screen command contract (SPEC-61 §4.3).</summary>
    public interface IInventoryCommands
    {
        void Select(int slot);
        void Move(int fromSlot, int toSlot);
        void Sort();
        void StackAll();
        /// QA-001: right-click halves a stack into the first empty slot.
        void Split(int slot);
        /// QA-001: shift-click gathers every stack of that item into the clicked one.
        void Gather(int slot);
    }
}
