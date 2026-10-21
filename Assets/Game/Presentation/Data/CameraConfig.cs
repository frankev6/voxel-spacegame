namespace Game.Presentation.Data
{
    /// <summary>Serialized camera tuning (SPEC-62 §4.1).</summary>
    [UnityEngine.CreateAssetMenu(menuName = "Game/Presentation/Camera Config")]
    public sealed class CameraConfig : UnityEngine.ScriptableObject
    {
        public UnityEngine.GameObject CameraPrefab;          // Presentation/Data/MainCamera.prefab
        public float VerticalFovDeg = 40f;
        public float BoostFovKickDeg = 7f;      // QA-001: added to the flight FOV while boost is held
        public float BoostFovHalfLife = .18f;
        public float NearClip = 0.3f;
        public float FarClip = 1000000f;     // 1000 km: a star system's planets, rings and air at true scale (reversed-Z float depth)
        // cruise (ADR-0122; owner, 2026-10-21: "increase FOV and have a shader effect to make it feel like you're going really really fast")
        public float CruiseFovKickDeg = 14f, CruiseFovFullSpeed = 5000f;   // the kick grows with √(speed / full speed)
        public float CruiseSpoolFovDipDeg = 4f;                          // the view narrows while the drive spools, then opens
        public float CruiseHalfLife = .35f;
        public float CruiseRumbleDeg = .15f, CruiseRumbleHz = 31f;           // a fine shake at full speed: the hull straining
        public float CruiseSpeedHalfLife = .5f;   // the view eases through speed changes (the drive drops 5 km/s to a belt's 800 m/s in a tick)
        public float CruisePunchFovDeg = 9f, CruisePunchHalfLife = .3f;      // the kick as the drive engages
        // flight
        public float DefaultPitchDeg = 55f, MinPitchDeg = 35f, MaxPitchDeg = 80f;
        public float DefaultDistance = 150f, MinDistance = 25f, MaxDistance = 400f;
        public float ZoomStepFactor = 0.88f;                 // distance *= factor^steps (steps > 0 = zoom in)
        public float OrbitDegPerPixel = 0.25f;
        public float FocusHalfLife = 0.06f, YawHalfLife = 0.12f, PitchHalfLife = 0.08f, ZoomHalfLife = 0.12f;
        public float LookAheadSeconds = 0.4f, LookAheadMaxFraction = 0.35f, LookAheadHalfLife = 0.35f;
        public float TeleportCutDistance = 250f;
        public float OcclusionMargin = 1.0f, OcclusionMinDistance = 5f, OcclusionReleaseHalfLife = 0.3f;
        // editor orbit
        public float EditorMinPitchDeg = -89f, EditorMaxPitchDeg = 89f;
        public float EditorMinDistance = 2f, EditorDistanceRadiusFactor = 2.5f, EditorDistancePad = 5f;
        public float EditorMaxDistanceRadiusFactor = 6f, EditorMaxDistancePad = 100f;
        public float EditorPanPerPixelPerMeter = 0.0015f, EditorHalfLife = 0.05f, EditorFocusPadding = 1.2f;
        public float EditorPivotBoundsPad = 50f;
        // blends and menu
        public float BlendSeconds = 0.5f;
        public UnityEngine.Vector3 MenuPosition = new UnityEngine.Vector3(0f, 60f, -120f);
        public UnityEngine.Vector3 MenuLookAt = UnityEngine.Vector3.zero;
        public float MenuDriftDegPerSecond = 2f;
        // shake
        public float TraumaDecayPerSecond = 1.4f, ShakeFrequencyHz = 22f;
        public float ShakeMaxOffsetFraction = 0.01f;         // × current distance, metres
        public float ShakeMaxAngleDeg = 2.0f;
        public ShakeRule[] ShakeRules = new ShakeRule[0];
    }
}
