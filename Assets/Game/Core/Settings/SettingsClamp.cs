namespace Game.Core.Settings
{
    /// <summary>Range helpers for the settings sections' Clamp (SPEC-02 §5.11).</summary>
    internal static class SettingsClamp
    {
        /// <summary>Clamps to [min, max]; NaN and ±∞ become the default.</summary>
        internal static float Range(float v, float min, float max, float fallback)
            => float.IsNaN(v) || float.IsInfinity(v) ? fallback : System.Math.Clamp(v, min, max);

        /// <summary>0 stays 0 ("keep current" / "default"); anything else is clamped to [min, max].</summary>
        internal static int ZeroOrRange(int v, int min, int max) => v == 0 ? 0 : System.Math.Clamp(v, min, max);
    }
}
