using System.Collections.Generic;
using Game.Core.Save;
using Game.Persistence.Data;
using Game.Persistence.Services;

namespace Game.Tests.EditMode.Persistence
{
    /// <summary>ISaveContext for pipeline tests: an active session whose block reason, metadata and thumbnails the test sets.</summary>
    public sealed class FakeSaveContext : ISaveContext
    {
        public bool HasActiveSession { get; set; } = true;
        public SaveBlockReason Block = SaveBlockReason.None;
        public ThumbnailRequest SlotThumbnail;
        public readonly List<ThumbnailRequest> Changed = new List<ThumbnailRequest>();
        public readonly HashSet<string> Keep = new HashSet<string>();

        public SaveBlockReason GetBlockReason() => Block;

        public void DescribeForMeta(SaveMeta meta)
        {
            meta.CommanderName = "Tester";
            meta.LocationText = "Test Space";
            meta.PlaytimeSeconds = 42.5;
        }

        public bool TryCaptureSlotThumbnail(out ThumbnailRequest thumbnail) { thumbnail = SlotThumbnail; return thumbnail != null; }

        public void CollectThumbnails(List<ThumbnailRequest> changed, HashSet<string> keep)
        {
            changed.AddRange(Changed);
            keep.UnionWith(Keep);
            Changed.Clear();                                          // "changed" means changed since the last save
        }

        public static ThumbnailRequest Image(string key, byte shade, int w = 8, int h = 4)
        {
            var px = new byte[w * h * 4];
            for (int i = 0; i < px.Length; i++) px[i] = (i & 3) == 3 ? (byte)255 : shade;
            return new ThumbnailRequest { Key = key, Rgba32 = px, Width = w, Height = h };
        }
    }
}
