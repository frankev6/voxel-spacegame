namespace Game.Editor.Migration
{
    /// <summary>One converter step. Implemented by SPEC-72 and by SPEC-30/33/40/41/42 (Order ranges in §4.7). Implements SPEC-72 §4.2.</summary>
    public interface ILegacyConverter
    {
        string Name { get; }        // shown in the report, e.g. "Blocks"
        int Order { get; }          // ascending; ties by Name (ordinal)
        void Convert(LegacyMigrationContext context);
    }
}
