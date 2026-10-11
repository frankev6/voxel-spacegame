using System.Collections.Generic;
using Game.Core.Save;
using Game.Persistence.Container;
using Game.Persistence.Data;

namespace Game.Persistence.Services
{
    /// <summary>Main-thread snapshot produced by capture (SPEC-05 §5.4 steps 1–7), consumed once by WriteCaptured. Implements SPEC-05 §4.5.</summary>
    internal sealed class CapturedSave
    {
        public SlotRef Slot;
        public SaveReason Reason;
        /// <summary>Set when capture stopped (Blocked, ParticipantFailed): nothing to write.</summary>
        public SaveResult? Failure;
        public SaveMeta Meta;
        public ContainerHeader Header;
        public SaveIdPalette Palette;
        public readonly List<(string Key, ushort Version, SaveWriter Writer)> Participants = new List<(string, ushort, SaveWriter)>();
        public readonly List<RawChunk> Preserved = new List<RawChunk>();
        public ThumbnailRequest SlotThumbnail;
        public readonly List<ThumbnailRequest> ChangedThumbnails = new List<ThumbnailRequest>();
        public readonly HashSet<string> KeepThumbnails = new HashSet<string>();
        public double CaptureMs;
        public System.Diagnostics.Stopwatch Total;

        /// <summary>Disposes every deferred block still held and every writer; safe to call twice. After a failure in the middle of encoding a
        /// block may be disposed twice, which the IDisposable contract allows (ISaveDeferredBlock implementations must tolerate it).</summary>
        public void Release()
        {
            foreach (var p in Participants)
            {
                p.Writer.DisposeDeferredBlocks();
                p.Writer.Dispose();
            }
            Participants.Clear();
        }
    }
}
