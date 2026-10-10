namespace Game.Core.Save
{
    /// <summary>Implemented by every module that owns persistent state (architecture §6.1, ADR-0007). Implements SPEC-02 §4.7.</summary>
    public interface ISaveParticipant
    {
        string Key { get; }                                  // exactly 4 chars [A-Z0-9], see SaveKey; unique per game
        int Version { get; }                                 // current chunk schema version, >= 1
        void Capture(SaveWriter writer);                     // main thread; reads managed state only (ECS was written back first, SPEC-01)
        void Restore(SaveReader reader, int fromVersion);    // fromVersion <= Version; restores IN PLACE
    }
}
