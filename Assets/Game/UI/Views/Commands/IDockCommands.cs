namespace Game.UI.Views
{
    /// <summary>Screen command contract (SPEC-61 §4.3).</summary>
    public interface IDockCommands
    {
        void Trade();
        void EditShip();
        void Undock();
    }
}
