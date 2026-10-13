namespace Game.UI.Views
{
    /// <summary>Root screens installed by SceneFlow; implemented by UiComposition (SPEC-61 §6).</summary>
    public interface IRootScreenSet
    {
        UIScreen MainMenu { get; }
        UIScreen FlightHud { get; }
        UIScreen ShipEditor { get; }
        UIScreen Loading { get; }
    }
}
