using Unity.Mathematics;

namespace Game.Presentation.Services
{
    /// <summary>CAM-013 (ADR-0123 decision 6): the free-aim chase camera of experimental 3D flight. While the player ship flies in 3D with
    /// face-the-mouse on, the flight camera is the <see cref="PresetName"/> preset; when either ends, the preset the player had returns.</summary>
    public static class FreeAim
    {
        public const string PresetName = "freeaim";
        /// <summary>m along the reticle ray when it hits nothing: far enough that the nose and the guns point where the view looks.</summary>
        public const float FarAimDistance = 1000;

        /// <summary>The preset to switch to this frame, or null to keep the current one. <paramref name="saved"/> holds the player's
        /// preset while free aim is on.</summary>
        public static string Switch(bool active, string current, ref string saved)
        {
            if (active && current != PresetName) { saved = current; return PresetName; }
            if (!active && current == PresetName) { string back = saved ?? "chase"; saved = null; return back; }
            return null;
        }

        /// <summary>The aim point of a reticle ray that hit nothing.</summary>
        public static float3 FarPoint(float3 origin, float3 direction) => origin + math.normalizesafe(direction) * FarAimDistance;
    }
}
