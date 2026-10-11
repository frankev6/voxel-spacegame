using System;
using System.IO;
using Game.Core;
using Game.Core.Events;
using Game.Core.Save;
using Game.Core.Settings;

namespace Game.App.Settings
{
    /// <summary>Loads, applies and saves the player's settings (SPEC-01 §5.6). Main thread. Writes are debounced and atomic (tmp + replace).</summary>
    public sealed class SettingsService : ISettingsService, IDisposable
    {
        readonly ISaveJsonCodec json;
        readonly SettingsPaths paths;
        GameSettings snapshot;          // values last published (DiffersFrom baseline)
        bool dirty;
        float timer;

        public SettingsService(ISaveJsonCodec json, SettingsPaths paths, float saveDebounceSeconds = 0.5f)
        {
            this.json = json ?? throw new ArgumentNullException(nameof(json));
            this.paths = paths ?? throw new ArgumentNullException(nameof(paths));
            DebounceSeconds = saveDebounceSeconds;
            snapshot = Current.Clone();
        }

        public GameSettings Current { get; } = new GameSettings();     // read-only by convention: never mutate
        public float DebounceSeconds { get; }
        internal int WriteCount { get; private set; }                   // tests: actual file writes

        /// <summary>Reads settings.json if present. On any error: defaults, the file is renamed to settings.broken.json, one warning.</summary>
        public void Load()
        {
            if (File.Exists(paths.FilePath))
            {
                try
                {
                    var dto = json.Deserialize<GameSettingsDto>(File.ReadAllBytes(paths.FilePath)) ?? throw new SaveFormatException("empty settings file", 0);
                    dto.CopyInto(Current);
                }
                catch (Exception e) when (e is SaveFormatException || e is IOException || e is UnauthorizedAccessException)
                {
                    Current.ResetToDefaults(SettingsSection.All);
                    try { if (File.Exists(paths.BrokenPath)) File.Delete(paths.BrokenPath); File.Move(paths.FilePath, paths.BrokenPath); }
                    catch (IOException) { }
                    GameLog.Warn(LogCategories.Settings, $"settings.json was unreadable ({e.Message}); using defaults, the file is kept as settings.broken.json");
                }
            }
            Current.Clamp();
            snapshot = Current.Clone();
        }

        public GameSettings CreateEditableCopy() => Current.Clone();

        /// <summary>Copies the sections, clamps, publishes SettingsChanged for the sections that actually changed and restarts the save debounce.</summary>
        public void Apply(GameSettings edited, SettingsSection sections)
        {
            Current.CopyFrom(edited, sections);
            Current.Clamp();
            var changed = Current.DiffersFrom(snapshot) & sections;
            if (changed == SettingsSection.None) return;
            snapshot.CopyFrom(Current, changed);
            EventBus<SettingsChanged>.Publish(new SettingsChanged(changed));
            dirty = true;
            timer = DebounceSeconds;
        }

        public void ResetToDefaults(SettingsSection sections)
        {
            var edited = Current.Clone();
            edited.ResetToDefaults(sections);
            Apply(edited, sections);
        }

        /// <summary>Writes when dirty and the debounce has elapsed (called once per frame with unscaled time by GameRoot).</summary>
        public void Tick(float unscaledDeltaTime)
        {
            if (!dirty) return;
            timer -= unscaledDeltaTime;
            if (timer <= 0f) Write();
        }

        public void Flush() { if (dirty) Write(); }                     // write now if dirty

        public void Dispose() => Flush();

        void Write()
        {
            dirty = false;
            var bytes = json.Serialize(GameSettingsDto.From(Current));
            Directory.CreateDirectory(Path.GetDirectoryName(paths.FilePath) ?? ".");
            File.WriteAllBytes(paths.TempPath, bytes);
            if (File.Exists(paths.FilePath)) File.Replace(paths.TempPath, paths.FilePath, null, ignoreMetadataErrors: true);
            else File.Move(paths.TempPath, paths.FilePath);
            WriteCount++;
        }
    }
}
