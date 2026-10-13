namespace Game.Data.Definitions
{
    /// <summary>SPEC-42 §4.3 authored objective marker data.</summary>
    [System.Serializable] public sealed class QuestMarker
    {
        public AuthoredStarSystemDefinition starSystem;        // null = no marker
        public string bodyTag;                                 // "" = the star system itself
        public string label;                                   // e.g. "Return to ARK"
    }
}
