namespace Game.Presentation.Data
{
    /// <summary>Distance-scaled presentation trauma rule (SPEC-62 §4.1).</summary>
    [System.Serializable]
    public struct ShakeRule
    {
        // TODO(CAM-008): add FxKind Kind after SPEC-63's contract exists.
        public float Strength;
        public float RadiusMeters;
    }
}
