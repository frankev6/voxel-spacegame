namespace Game.Presentation.Components
{
    /// <summary>HUD state flags (SPEC-61 §4.5).</summary>
    [System.Flags]
    public enum HudFlags : byte
    {
        None = 0,
        HasShip = 1,
        IsHulk = 2,
        HasTarget = 4,
        HasInteraction = 8,
        Boosting = 16
    }
}
