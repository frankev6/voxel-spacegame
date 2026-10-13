namespace Game.UI.Views
{
    /// <summary>Implements SPEC-51 §4.4. Blueprint browser commands; implemented by the App presenter.</summary>
    public interface IBlueprintBrowserCommands
    {
        void Select(int index);
        void LoadStamp();
        void LoadReplace();
        void Delete();
    }
}
