namespace Game.Data.Definitions
{
    /// <summary>SPEC-42 §4.2 authored star-system, body-tag and hail location.</summary>
    [System.Serializable] public sealed class BeatLocation
    {
        public AuthoredStarSystemDefinition starSystem;        // null = any star system
        public string[] bodyTags = System.Array.Empty<string>(); // Interact: any of these storyTags; empty = any interactable body
        public EncounterDefinition hailEncounter;              // Hail: the encounter whose ships were hailed (required for Hail)
    }
}
