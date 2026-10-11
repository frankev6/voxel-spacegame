using System.IO;

namespace Game.App.Settings
{
    /// <summary>Where settings.json lives (SPEC-01 §5.1). Tests pass a temporary file path.</summary>
    public sealed class SettingsPaths
    {
        public SettingsPaths(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) throw new System.ArgumentException("A settings file path is required.", nameof(filePath));
            FilePath = Path.GetFullPath(filePath);
        }

        public static SettingsPaths Default => new SettingsPaths(Path.Combine(UnityEngine.Application.persistentDataPath, "settings.json"));
        public string FilePath { get; }
        public string TempPath => FilePath + ".tmp";
        public string BrokenPath => Path.Combine(Path.GetDirectoryName(FilePath) ?? "", "settings.broken.json");
    }
}
