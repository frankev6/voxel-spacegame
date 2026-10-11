using System.Collections.Generic;
using Game.Persistence.Data;

namespace Game.Persistence.Services
{
    /// <summary>Implements SPEC-05 §4.5. Implemented by Game.App (SPEC-01). The only way SaveService learns about the session.</summary>
    public interface ISaveContext
    {
        bool HasActiveSession { get; }
        Game.Core.Save.SaveBlockReason GetBlockReason();                  // None when saving is allowed
        void DescribeForMeta(SaveMeta meta);                              // fills CommanderName, LocationText, PlaytimeSeconds
        bool TryCaptureSlotThumbnail(out ThumbnailRequest thumbnail);     // Key must be "slot"
        void CollectThumbnails(List<ThumbnailRequest> changed, HashSet<string> keep);  // keep = every non-slot key that must exist after the save
    }
}
