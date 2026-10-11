namespace Game.Persistence.Data
{
    /// <summary>Implements SPEC-05 §4.5. One slot as listed by SaveService.ListSlots.</summary>
    public sealed class SlotInfo
    {
        public SlotRef Ref; public SlotKind Kind; public SlotStatus Status;
        public SaveMeta Meta;            // null when Status is Corrupt or NewerFormat and no META could be read
        public long WorldBytes;          // size of world.sav (or world.bak for BackupOnly)
    }
}
