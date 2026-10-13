namespace Game.UI.Views
{
    /// <summary>Screen command contract (SPEC-61 §4.3).</summary>
    public interface IFleetCommands
    {
        void Select(int index);
        void Edit();
        void SetActive();
    }
}
