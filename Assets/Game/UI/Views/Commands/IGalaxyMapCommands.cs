namespace Game.UI.Views
{
    /// <summary>Screen command contract (SPEC-61 §4.3).</summary>
    public interface IGalaxyMapCommands
    {
        void Select(int pointIndex);
        void SetDestination();
        void ClearDestination();
        void FocusCurrent();
    }
}
