namespace Game.UI.Views
{
    /// <summary>Screen command contract (SPEC-61 §4.3).</summary>
    public interface IStarSystemMapCommands
    {
        void Select(int pointIndex);
        void SetWaypoint();
        void FocusPlayer();
    }
}
