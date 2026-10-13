using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Validation;
using Game.Editor.Content;
using Game.Editor.Migration;
using Game.Editor.Migration.Dto;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using Object = UnityEngine.Object;

namespace Game.Editor.Audio
{
    /// <summary>SPEC-64 §6.4 / AUD-007: strict, provenance-checked audio conversion with protected ownership handoff.</summary>
    public static class LegacyAudioCueImporter
    {
        internal const string SourcePath = "Import/Legacy/content/Audio/AudioLibrary.json";
        internal const string ManifestPath = "Import/Migration/audio-content-manifest.json";
        internal static IEnumerable<string> CueIds => Rows.Select(row => row.Id);
        const string Menu = "Game/Audio/Import Legacy Audio Library";

        readonly struct Row
        {
            internal readonly string Key, Id, Clip;
            internal readonly AudioBus Bus;
            internal readonly float Volume, Spatial, Cooldown, Pitch;
            internal readonly int Max, Priority;
            internal readonly CoalesceMode Coalesce;
            internal readonly bool Combat, Loop;
            internal Row(string key, string id, string clip, AudioBus bus, float volume, float spatial, int max, int priority,
                float cooldown, CoalesceMode coalesce, bool combat, float pitch = 1, bool loop = false)
            { Key = key; Id = id; Clip = clip; Bus = bus; Volume = volume; Spatial = spatial; Max = max; Priority = priority;
                Cooldown = cooldown; Coalesce = coalesce; Combat = combat; Pitch = pitch; Loop = loop; }
        }

        // Immutable playback table; volumes for legacy rows are read from the strict DTO, preserving the source float.
        [StaticStateOk("immutable SPEC-64 playback table")] static readonly Row[] Rows = {
            new("UI_Button_Click", "cue_ui_click", "INTERFACE-MetallicClick.wav", AudioBus.Ui, 1, 0, 4, 64, .03f, CoalesceMode.PerCue, false),
            new("UI_Button_Hover", "cue_ui_hover", "UIHover_INTERFACE-Positive Click_HY_PC-002.wav", AudioBus.Ui, 1, 0, 2, 96, .05f, CoalesceMode.PerCue, false),
            new("UI_Slot_Click", "cue_ui_slot_click", "INTERFACE-MetallicClick.wav", AudioBus.Ui, 1, 0, 4, 64, .03f, CoalesceMode.PerCue, false),
            new("UI_Slot_Hover", "cue_ui_slot_hover", "UIHover_INTERFACE-Positive Click_HY_PC-002.wav", AudioBus.Ui, .1f, 0, 2, 96, .05f, CoalesceMode.PerCue, false),
            new("UI_View_Open", "cue_ui_view_open", "UIClick_INTERFACE-OpenClose.wav", AudioBus.Ui, .5f, 0, 2, 64, .1f, CoalesceMode.PerCue, false),
            new("UI_View_Close", "cue_ui_view_close", "UIClick_INTERFACE-OpenClose.wav", AudioBus.Ui, .5f, 0, 2, 64, .1f, CoalesceMode.PerCue, false),
            new("UI_Fail", "cue_ui_fail", "Failure Stinger.wav", AudioBus.Ui, .3f, 0, 1, 32, .3f, CoalesceMode.PerCue, false),
            new("UI_Success", "cue_ui_success", "Success Stinger.wav", AudioBus.Ui, .3f, 0, 1, 32, .3f, CoalesceMode.PerCue, false),
            new("Weapon_Laser_Fire", "cue_weapon_laser_fire", "Projectile_EXPLOSION-Bit Bomb_HY_PC-001.wav", AudioBus.Sfx, .2f, 1, 6, 128, .05f, CoalesceMode.PerCell, true),
            new("Weapon_Plasma_Fire", "cue_weapon_plasma_fire", "EXPLOSION-Cruncher_HY_PC-003.wav", AudioBus.Sfx, .1f, 1, 6, 128, .05f, CoalesceMode.PerCell, true),
            new("Weapon_Cannon_Fire", "cue_weapon_cannon_fire", "EXPLOSION-Cruncher_HY_PC-003.wav", AudioBus.Sfx, .3f, 1, 6, 128, .05f, CoalesceMode.PerCell, true),
            new("Impact_Hull", "cue_impact_hull", "explosion_hull_impact.wav", AudioBus.Sfx, .3f, 1, 8, 140, .04f, CoalesceMode.PerCell, true),
            new("Impact_Explosion_Small", "cue_explosion_small", "EXPLOSION-Cruncher_HY_PC-003.wav", AudioBus.Sfx, .5f, 1, 6, 100, .05f, CoalesceMode.PerCell, true),
            new("Impact_Explosion_Large", "cue_explosion_large", "EXPLOSION-Cruncher_HY_PC-003.wav", AudioBus.Sfx, .3f, 1, 4, 80, .1f, CoalesceMode.PerCell, true),
            new("Editor_Place_Voxel", "cue_editor_place", "Rooted_Dirt_break3_placeholder.ogg", AudioBus.Ui, 1, 0, 4, 96, .03f, CoalesceMode.PerCue, false),
            new("Editor_Remove_Voxel", "cue_editor_remove", "Rooted_Dirt_break3_placeholder.ogg", AudioBus.Ui, 1, 0, 4, 96, .03f, CoalesceMode.PerCue, false),
            new("Combat_Standard", "cue_music_combat", "10 Boarding the Mechanism.wav", AudioBus.Music, .1f, 0, 1, 0, 0, CoalesceMode.None, false, loop: true),
            new("Exploration", "cue_music_exploration", "02 The Restricted Hangar.wav", AudioBus.Music, .1f, 0, 1, 0, 0, CoalesceMode.None, false, loop: true),
            new("MainMenu", "cue_music_menu", "02 The Restricted Hangar.wav", AudioBus.Music, .1f, 0, 1, 0, 0, CoalesceMode.None, false, loop: true),
            new("SpaceStation", "cue_music_station", "03 Promising Development.wav", AudioBus.Music, .1f, 0, 1, 0, 0, CoalesceMode.None, false, loop: true),
            new(null, "cue_impact_energy", "explosion_hull_impact.wav", AudioBus.Sfx, .25f, 1, 6, 120, .05f, CoalesceMode.PerCell, true, 1.3f),
            new(null, "cue_impact_explosive", "EXPLOSION-Cruncher_HY_PC-005.wav", AudioBus.Sfx, .4f, 1, 6, 120, .05f, CoalesceMode.PerCell, true),
            new(null, "cue_impact_ram", "EXPLOSION-Cruncher_HY_PC-005.wav", AudioBus.Sfx, .6f, 1, 6, 120, .05f, CoalesceMode.PerCell, true, .8f),
            new(null, "cue_shield_hit", "PROJECTILE-Laser Bursts_HY_PC-005.wav", AudioBus.Sfx, .2f, 1, 6, 120, .05f, CoalesceMode.PerCell, true, 1.5f),
            new(null, "cue_cell_break", "Rooted_Dirt_break3_placeholder.ogg", AudioBus.Sfx, .4f, 1, 6, 120, .05f, CoalesceMode.PerCell, false, .7f),
            new(null, "cue_break_off", "EXPLOSION-Cruncher_HY_PC-005.wav", AudioBus.Sfx, .5f, 1, 6, 120, .05f, CoalesceMode.PerCell, true, .8f),
            new(null, "cue_ship_death", "EXPLOSION-Cruncher_HY_PC-005.wav", AudioBus.Sfx, .8f, 1, 2, 40, .05f, CoalesceMode.PerCell, true),
            new(null, "cue_warp", "DSGNMisc_MOVEMENT-Mecha Large Takeoff_HY_PC-004.wav", AudioBus.Sfx, .6f, 0, 6, 120, .05f, CoalesceMode.PerCell, false),
            new(null, "cue_ui_trade", "DSGNTonl_USABLE-Coin Toss_HY_PC-002.wav", AudioBus.Ui, .5f, 0, 6, 120, .05f, CoalesceMode.PerCue, false),
            new(null, "cue_stinger_discovery", "Fantasy_Stinger_1.mp3", AudioBus.Music, .5f, 0, 1, 0, 0, CoalesceMode.None, false),
        };

        [MenuItem(Menu)] static void ImportMenu() => Run().LogTo("Audio");
        [MenuItem(Menu, true)] static bool CanImport() => !EditorApplication.isPlayingOrWillChangePlaymode;

        /// <summary>Imports production audio and rebuilds the catalog; generation and Unity execution belong to the lead.</summary>
        public static ValidationReport Run()
        {
            var report = new ValidationReport("AUD-007");
            if (!CanImport()) { report.Error("AUD-E030", SourcePath, "", "Audio import requires Edit mode."); return report; }
            try
            {
                var owned = MigrationManifest.Load(ManifestPath);
                var migrated = MigrationManifest.Load("Import/Migration/manifest.json");
                Import(ContentDatabaseBuilder.ContentRoot, owned, report, SourcePath, migrated);
                if (!report.HasErrors) { owned.Save(ManifestPath); ContentDatabaseBuilder.Rebuild(); }
            }
            catch (Exception e) { report.Error("AUD-E030", SourcePath, "", e.Message); }
            return report;
        }

        internal static IReadOnlyList<(string AssetPath, WriteOutcome Outcome)> Import(string contentRoot, MigrationManifest owned,
            ValidationReport report, string sourcePath = SourcePath, MigrationManifest migrated = null)
        {
            var writer = new LegacyAssetWriter(contentRoot, owned, false, report);
            var dto = LegacyJson.ReadFile<LegacyAudioLibraryDto>(Path.GetDirectoryName(Path.GetFullPath(sourcePath)), Path.GetFileName(sourcePath), report);
            if (dto == null) return writer.Outcomes; // LegacyJson uses Newtonsoft MissingMemberHandling.Error, never a permissive reader.
            var entries = new Dictionary<string, (LegacyRef Clip, double Volume)>(StringComparer.Ordinal);
            if (dto.Sounds == null || dto.Music == null) report.Error("AUD-E030", sourcePath, "", "Sounds and Music arrays are required.");
            if (dto.Sounds != null) foreach (var entry in dto.Sounds) Add(entry?.Id, entry?.Clip ?? default, entry?.Volume ?? double.NaN);
            if (dto.Music != null) foreach (var entry in dto.Music) Add(entry?.Track, entry?.Clip ?? default, entry?.Volume ?? double.NaN);
            foreach (string key in entries.Keys)
                if (!Rows.Any(r => r.Key == key)) report.Error("AUD-E030", sourcePath, key, "Unexpected legacy audio key.");
            var clips = AudioProvenanceTable.FindClips();
            var provenance = AssetDatabase.LoadAssetAtPath<AudioProvenanceTable>(AudioProvenanceTable.AssetPath);
            var candidates = new Dictionary<string, AudioCueDefinition>(StringComparer.Ordinal);
            AudioConfigDefinition candidateConfig = null;
            try
            {
                foreach (var row in Rows)
                {
                    float volume = row.Volume; string file = row.Clip;
                    if (row.Key != null)
                    {
                        if (!entries.TryGetValue(row.Key, out var entry)) { report.Error("AUD-E030", sourcePath, row.Key, "Required legacy audio row is missing."); continue; }
                        file = Path.GetFileName(entry.Clip.Path?.Replace('\\', '/')); volume = (float)entry.Volume;
                        if (!float.IsFinite(volume) || volume < 0 || volume > 1) report.Error("AUD-E030", sourcePath, row.Key + ".Volume", "Volume must be finite and within [0, 1].");
                    }
                    var clip = Resolve(file, clips, provenance, report, sourcePath, row.Id);
                    if (file != row.Clip) report.Error("AUD-E030", sourcePath, row.Key + ".Clip", "Clip file name does not match SPEC-64: " + row.Clip + ".");
                    if (clip == null) continue;
                    var cue = ScriptableObject.CreateInstance<AudioCueDefinition>(); cue.name = row.Id;
                    using (var so = new SerializedObject(cue)) { FillCue(so, row, volume, clip); so.ApplyModifiedPropertiesWithoutUndo(); }
                    candidates.Add(row.Id, cue); cue.Validate(report.For(Target(row.Id)), null);
                    CheckTarget(cue);
                }
                if (candidates.Count == Rows.Length)
                {
                    candidateConfig = ScriptableObject.CreateInstance<AudioConfigDefinition>(); candidateConfig.name = "config_audio";
                    using (var so = new SerializedObject(candidateConfig)) { FillConfig(so, candidates); so.ApplyModifiedPropertiesWithoutUndo(); }
                    candidateConfig.Validate(report.For(Target("config_audio")), null);
                    // For a protected existing config, compare its persistent references, not temporary candidate IDs.
                    var existingConfig = Existing<AudioConfigDefinition>(Target("config_audio"));
                    if (existingConfig != null && !Own(Target("config_audio"), existingConfig))
                    {
                        var persistent = candidates.Keys.ToDictionary(id => id, id => AssetDatabase.LoadAssetAtPath<AudioCueDefinition>(Target(id)));
                        using var so = new SerializedObject(candidateConfig); FillConfig(so, persistent); so.ApplyModifiedPropertiesWithoutUndo();
                    }
                    CheckTarget(candidateConfig);
                }
                if (report.HasErrors) return writer.Outcomes; // Entire plan is checked before any asset write.
                var written = new Dictionary<string, AudioCueDefinition>(StringComparer.Ordinal);
                foreach (var row in Rows)
                {
                    var cue = candidates[row.Id];
                    written.Add(row.Id, writer.CreateOrUpdate<AudioCueDefinition>("Audio", row.Id,
                        so => FillCue(so, row, cue.Volume, cue.Clips[0]), out _));
                }
                writer.CreateOrUpdate<AudioConfigDefinition>("Audio", "config_audio", so => FillConfig(so, written), out _);
                return writer.Outcomes;
            }
            finally
            {
                foreach (var cue in candidates.Values) Object.DestroyImmediate(cue);
                if (candidateConfig != null) Object.DestroyImmediate(candidateConfig);
            }

            void Add(string key, LegacyRef clip, double volume)
            {
                if (string.IsNullOrEmpty(key) || entries.ContainsKey(key)) report.Error("AUD-E030", sourcePath, key ?? "", "Null or duplicate legacy audio key.");
                else entries.Add(key, (clip, volume));
            }
            string Target(string id) => contentRoot + "/Audio/" + id + ".asset";
            T Existing<T>(string path) where T : ContentDefinition
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if ((asset != null && asset.GetType() != typeof(T)) || (asset == null && (File.Exists(path) || File.Exists(path + ".meta") || Directory.Exists(path))))
                { report.Error("AUD-E033", path, "", "Existing asset type could not be matched; preserved."); return null; }
                return asset as T;
            }
            bool Own(string path, ContentDefinition asset)
            {
                if (EditorUtility.IsDirty(asset) || !File.Exists(path)) return false;
                ulong hash = StableHash.Hash64(File.ReadAllBytes(path));
                if (owned.TryGetHash(path, out ulong expected)) return expected == hash;
                // Adopt only an unchanged MIG-011 asset. Leave its old manifest hash stale so Run All cannot reset AUD fields.
                if (migrated != null && migrated.TryGetHash(path, out expected) && expected == hash) { owned.SetHash(path, hash); return true; }
                return false;
            }
            void CheckTarget(ContentDefinition candidate)
            {
                string path = Target(candidate.IdString);
                var asset = candidate is AudioCueDefinition ? (ContentDefinition)Existing<AudioCueDefinition>(path) : Existing<AudioConfigDefinition>(path);
                if (asset == null) return;
                if (asset.IdString != candidate.IdString) report.Error("AUD-E033", path, "id", "Existing ID does not match; preserved.");
                else if (!Own(path, asset) && EditorJsonUtility.ToJson(asset) != EditorJsonUtility.ToJson(candidate))
                    report.Error("AUD-E034", path, "", "Hand-edited or unowned audio asset differs from the import; preserved. Review before generation.");
            }
        }

        static AudioClip Resolve(string file, AudioClip[] inventory, AudioProvenanceTable table, ValidationReport report, string sourcePath, string id)
        {
            if (file == "ComfyUI_00018_.mp3" || file == "upbeat.mp3")
            { report.Error("AUD-E032", sourcePath, id, "Blocked clip is never mapped: " + file); return null; }
            var matches = inventory.Where(c => c != null && Path.GetFileName(AssetDatabase.GetAssetPath(c)) == file).ToArray();
            if (string.IsNullOrEmpty(file) || matches.Length != 1)
            { report.Error("AUD-E031", sourcePath, id, "Expected one clip under Assets/Game/Audio/: " + (file ?? "<null>") + "; found " + matches.Length + "."); return null; }
            var rows = table?.entries?.Where(e => e != null && e.clip == matches[0]).ToArray();
            if (rows == null || rows.Length != 1 || (rows[0].status != ProvenanceStatus.Unverified && rows[0].status != ProvenanceStatus.Confirmed))
            { report.Error("AUD-E032", sourcePath, id, "Clip provenance is missing, duplicated or Blocked: " + file + "."); return null; }
            return matches[0];
        }

        static void FillCue(SerializedObject so, Row row, float volume, AudioClip clip)
        {
            so.FindProperty("id").stringValue = row.Id;
            var clips = so.FindProperty("clips"); clips.arraySize = 1; clips.GetArrayElementAtIndex(0).objectReferenceValue = clip;
            so.FindProperty("bus").intValue = (int)row.Bus; so.FindProperty("volume").floatValue = volume;
            so.FindProperty("spatialBlend").floatValue = row.Spatial; so.FindProperty("maxVoices").intValue = row.Max;
            so.FindProperty("priority").intValue = row.Priority; so.FindProperty("cooldown").floatValue = row.Cooldown;
            so.FindProperty("coalesce").intValue = (int)row.Coalesce; so.FindProperty("combatSignal").boolValue = row.Combat;
            so.FindProperty("pitch").floatValue = row.Pitch; so.FindProperty("loop").boolValue = row.Loop;
        }
        static void FillConfig(SerializedObject so, IReadOnlyDictionary<string, AudioCueDefinition> cues)
        {
            so.FindProperty("id").stringValue = "config_audio";
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/Game/Audio/Mixer/GameMixer.mixer"); so.FindProperty("mixer").objectReferenceValue = mixer;
            foreach (string bus in new[] { "Sfx", "Ui", "Music", "Ambience" })
            {
                var groups = mixer != null ? mixer.FindMatchingGroups("Master/" + bus) : Array.Empty<AudioMixerGroup>();
                so.FindProperty(char.ToLowerInvariant(bus[0]) + bus.Substring(1) + "Group").objectReferenceValue = groups.Length == 1 ? groups[0] : null;
            }
            var ui = so.FindProperty("uiSounds"); ui.arraySize = 11;
            string[] uiIds = { "cue_ui_hover", "cue_ui_click", "cue_ui_view_open", "cue_ui_view_close", "cue_ui_view_close", "cue_ui_fail",
                "cue_ui_success", "cue_ui_slot_hover", "cue_ui_slot_click", "cue_ui_success", "cue_ui_click" };
            for (int i = 0; i < uiIds.Length; i++) ui.GetArrayElementAtIndex(i).objectReferenceValue = cues[uiIds[i]];
            foreach (var link in new[] { ("musicMenu", "cue_music_menu"), ("musicExploration", "cue_music_exploration"), ("musicCombat", "cue_music_combat"),
                ("musicEditor", "cue_music_station"), ("impactKinetic", "cue_impact_hull"), ("impactEnergy", "cue_impact_energy"), ("impactExplosive", "cue_impact_explosive"),
                ("impactRam", "cue_impact_ram"), ("explosionSmall", "cue_explosion_small"), ("explosionLarge", "cue_explosion_large"), ("shieldHit", "cue_shield_hit"),
                ("cellBreak", "cue_cell_break"), ("breakOff", "cue_break_off"), ("shipDeath", "cue_ship_death") })
                so.FindProperty(link.Item1).objectReferenceValue = cues[link.Item2];
            foreach (string field in new[] { "ambienceWorld", "engineLoop", "beamLoop" }) so.FindProperty(field).objectReferenceValue = null;
        }
    }
}
