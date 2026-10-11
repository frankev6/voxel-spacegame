using Game.Core.Save;

namespace Game.Persistence.Data
{
    /// <summary>Implements SPEC-05 §4.6. meta.json and the META chunk.</summary>
    public sealed class SaveMeta
    {
        public int MetaVersion = 1;
        public int FormatVersion;                      // container formatVersion
        public string GameVersion;                     // Application.version (captured on the main thread)
        public string DatabaseHash;                    // 16 lowercase hex digits of ContentService.DatabaseHash
        public string ProfileId;
        public string SlotId;
        public string DisplayName;                     // slot name shown in UI; default rules §5.2
        public SaveReason Reason;                      // serialized as its name (StringEnumConverter)
        public System.DateTime SavedUtc;               // ISO 8601, UTC
        public double PlaytimeSeconds;
        public string CommanderName;
        public string LocationText;                    // e.g. "Ark Start - near ARK station"
        public string Thumbnail;                       // "thumbs/slot.png" or null
        public long WorldBytes;                        // size of world.sav after the write (0 inside the META chunk)
    }
}
