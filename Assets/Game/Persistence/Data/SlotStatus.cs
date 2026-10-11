namespace Game.Persistence.Data
{
    /// <summary>Implements SPEC-05 §4.5.</summary>
    public enum SlotStatus : byte { Ok = 0, MetaMissing = 1, Corrupt = 2, BackupOnly = 3, NewerFormat = 4 }
}
