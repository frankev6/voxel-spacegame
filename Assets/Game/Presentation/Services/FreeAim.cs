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
        public const float DefaultTiltDeg = 8, MaxTiltDeg = 30;
        /// <summary>Owner (2026-11-04): "add an offset between the ship and the crosshair, so the ship doesn't sit right on top of the
        /// crosshair". The free-aim view looks this far above the orbit line, so the ship sits below the centre reticle. A camera offset,
        /// not a ship one: the ship and its guns stay where the simulation has them, and the reticle ray still sets the aim.
        /// Tuned live with `camera tilt=`.</summary>
        [Game.Core.StaticStateOk("dev tuning of the free-aim camera tilt, main thread; reset on load")]
        public static float TiltDeg = DefaultTiltDeg;
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => TiltDeg = DefaultTiltDeg;

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
