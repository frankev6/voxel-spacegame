namespace Game.Data.Definitions
{
    /// <summary>Public narrative contract (SPEC-40 §4.12); behavior follows in the named implementation task.</summary>
    [ContentIdPrefix("narrative_")]
    [UnityEngine.CreateAssetMenu(menuName = "Game/Narrative/Config", fileName = "narrative_config")]
    public sealed class NarrativeConfigDefinition : ContentDefinition
    {
        [UnityEngine.SerializeField, UnityEngine.Min(1)] private int hullDamagePerPoint = 20;      // damage_ship amount × this = DamageEvent HP
        [UnityEngine.SerializeField] private string[] knownBodyTags = System.Array.Empty<string>(); // extra validator vocabulary; the main source is every BodyTemplateDefinition.storyTag (SPEC-30)
        [UnityEngine.SerializeField] private string[] knownStarSystems = System.Array.Empty<string>(); // fallback IDs until SPEC-30 provides a resolver
        public int HullDamagePerPoint => hullDamagePerPoint;
        public System.Collections.Generic.IReadOnlyList<string> KnownBodyTags => knownBodyTags;
        public System.Collections.Generic.IReadOnlyList<string> KnownStarSystems => knownStarSystems;
    }
}
