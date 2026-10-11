using Game.Persistence.Data;

namespace Game.Persistence.Services
{
    /// <summary>Implements SPEC-05 §4.5. Every file path of the save folder layout (§4.1).</summary>
    public sealed class SavePaths
    {
        public SavePaths(string root) { throw new System.NotImplementedException("PERS-002"); }
        public static SavePaths CreateDefault() { throw new System.NotImplementedException("PERS-002"); }                     // Path.Combine(Application.persistentDataPath, "saves"); main thread
        public string Root => throw new System.NotImplementedException("PERS-002");
        public string ProfileDir(ProfileId p) { throw new System.NotImplementedException("PERS-002"); }
        public string ProfileFile(ProfileId p) { throw new System.NotImplementedException("PERS-002"); } // .../profile.json
        public string SlotDir(SlotRef s) { throw new System.NotImplementedException("PERS-002"); }
        public string WorldFile(SlotRef s) { throw new System.NotImplementedException("PERS-002"); }     // world.sav
        public string BackupFile(SlotRef s) { throw new System.NotImplementedException("PERS-002"); }    // world.bak
        public string TempFile(SlotRef s) { throw new System.NotImplementedException("PERS-002"); }      // world.sav.tmp
        public string MetaFile(SlotRef s) { throw new System.NotImplementedException("PERS-002"); }      // meta.json
        public string ThumbsDir(SlotRef s) { throw new System.NotImplementedException("PERS-002"); }
        public string ThumbnailFile(SlotRef s, string key) { throw new System.NotImplementedException("PERS-002"); }   // thumbs/<key>.png
    }
}
