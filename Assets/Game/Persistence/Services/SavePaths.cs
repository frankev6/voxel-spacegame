using System.IO;
using Game.Persistence.Data;

namespace Game.Persistence.Services
{
    /// <summary>Implements SPEC-05 §4.5. Every file path of the save folder layout (§4.1).</summary>
    public sealed class SavePaths
    {
        public SavePaths(string root)
        {
            if (string.IsNullOrEmpty(root)) throw new System.ArgumentException("A save root is required.", nameof(root));
            Root = Path.GetFullPath(root);
        }

        public static SavePaths CreateDefault() => new SavePaths(Path.Combine(UnityEngine.Application.persistentDataPath, "saves"));   // main thread
        public string Root { get; }
        public string ProfileDir(ProfileId p) => Path.Combine(Root, p.Value);
        public string ProfileFile(ProfileId p) => Path.Combine(ProfileDir(p), "profile.json");
        public string SlotDir(SlotRef s) => Path.Combine(ProfileDir(s.Profile), s.Slot.Value);
        public string WorldFile(SlotRef s) => Path.Combine(SlotDir(s), "world.sav");
        public string BackupFile(SlotRef s) => Path.Combine(SlotDir(s), "world.bak");
        public string TempFile(SlotRef s) => Path.Combine(SlotDir(s), "world.sav.tmp");
        public string MetaFile(SlotRef s) => Path.Combine(SlotDir(s), "meta.json");
        public string ThumbsDir(SlotRef s) => Path.Combine(SlotDir(s), "thumbs");
        public string ThumbnailFile(SlotRef s, string key) => Path.Combine(ThumbsDir(s), key + ".png");
    }
}
