using Game.Data.Definitions;

namespace Game.Presentation.Data
{
    /// <summary>Distance-scaled presentation trauma rule (SPEC-62 §4.1).</summary>
    [System.Serializable]
    public struct ShakeRule
    {
        public FxKind Kind;
        public float Strength;
        public float RadiusMeters;
    }
}
