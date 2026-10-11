namespace Game.Editor.Migration
{
    /// <summary>Implements SPEC-72 §4.7. Which legacy narrative encounters a body offered (for SPEC-41/42 beat locations).</summary>
    public readonly struct LegacyBodyEncounterLink
    {
        public readonly string BodyId; public readonly string LegacyEncounterId; public readonly float Weight; public readonly bool AutoInteract; public readonly string EncounterIdOverride;
        public LegacyBodyEncounterLink(string bodyId, string legacyEncounterId, float weight, bool autoInteract, string encounterIdOverride)
        { BodyId = bodyId; LegacyEncounterId = legacyEncounterId; Weight = weight; AutoInteract = autoInteract; EncounterIdOverride = encounterIdOverride; }
    }
}
