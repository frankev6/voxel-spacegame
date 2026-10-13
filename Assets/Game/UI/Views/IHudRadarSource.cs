namespace Game.UI.Views
{
    /// <summary>Reusable presentation radar data visible to UI without ECS access (SPEC-61 §6).</summary>
    public interface IHudRadarSource
    {
        System.ReadOnlySpan<HudRadarBlipView> Blips { get; }
    }
}
