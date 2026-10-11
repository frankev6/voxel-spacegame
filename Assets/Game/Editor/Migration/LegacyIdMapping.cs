namespace Game.Editor.Migration
{
    /// <summary>Implements SPEC-72 §4.2. One row of the ID map; V2Id null = not converted.</summary>
    public readonly struct LegacyIdMapping
    {
        public readonly LegacyIdKind Kind; public readonly string LegacyKey; public readonly string V2Id;
        public readonly string Note;
        public LegacyIdMapping(LegacyIdKind kind, string legacyKey, string v2Id, string note) { Kind = kind; LegacyKey = legacyKey; V2Id = v2Id; Note = note; }
    }
}
