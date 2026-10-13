namespace Game.Data.Definitions
{
    /// <summary>Implements SPEC-24 §4.1. Authored multiplier using SPEC-22's canonical StatId.</summary>
    [System.Serializable]
    public struct AbilityStatMod
    {
        public Game.Data.Combat.StatId Stat;
        [UnityEngine.Min(0.01f)] public float Multiplier;
    }
}
