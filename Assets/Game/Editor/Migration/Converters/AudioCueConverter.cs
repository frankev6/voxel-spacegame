using System;
using Game.Data.Definitions;
using Game.Editor.Audio;
using Game.Editor.Migration.Dto;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Migration.Converters
{
    /// <summary>SPEC-72 §4.6: imports legacy audio references, volumes and categories through the protected writer.</summary>
    public sealed class AudioCueConverter : ILegacyConverter
    {
        const string LegacyMusic = "Assets/Game/SFX/Music/";
        const string LegacySfx = "Assets/Game/SFX/";
        public string Name => "Audio";
        public int Order => 200;

        public void Convert(LegacyMigrationContext context)
        {
            var provenance = AssetDatabase.LoadAssetAtPath<AudioProvenanceTable>(AudioProvenanceTable.AssetPath);
            foreach (var file in context.Index.OfType("AudioLibrary"))
            {
                var dto = LegacyJson.Read<LegacyAudioLibraryDto>(file, context.Report);
                if (dto == null) continue;
                if (dto.Sounds != null)
                    for (int i = 0; i < dto.Sounds.Length; i++)
                    {
                        var entry = dto.Sounds[i];
                        if (entry == null) { context.Report.Error("MIG-E002", file.RelativePath, $"Sounds[{i}]", "Audio entry is null."); continue; }
                        ConvertEntry(context, provenance, file, $"Sounds[{i}]", entry.Id, entry.Clip, entry.Volume);
                    }
                if (dto.Music != null)
                    for (int i = 0; i < dto.Music.Length; i++)
                    {
                        var entry = dto.Music[i];
                        if (entry == null) { context.Report.Error("MIG-E002", file.RelativePath, $"Music[{i}]", "Audio entry is null."); continue; }
                        ConvertEntry(context, provenance, file, $"Music[{i}]", entry.Track, entry.Clip, entry.Volume);
                    }
            }
        }

        static void ConvertEntry(LegacyMigrationContext context, AudioProvenanceTable provenance,
            LegacyFile file, string field, string key, LegacyRef reference, double volume)
        {
            if (string.IsNullOrEmpty(key) || !context.IdMap.TryMap(LegacyIdKind.AudioCue, key, out string id))
            { context.Report.Error("MIG-E003", file.RelativePath, field, "Legacy audio key is not mapped: " + key); return; }
            if (id == null) return; // The explicit None mapping has no cue.
            string path = ClipPath(reference.Path);
            var clip = path == null ? null : AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
            { context.Report.Error("MIG-E060", file.RelativePath, field + ".Clip", "Audio clip missing at mapped path: " + (path ?? reference.Path ?? "<null>")); return; }

            AudioProvenanceEntry match = null;
            int matches = 0;
            if (provenance != null && provenance.entries != null)
                foreach (var entry in provenance.entries)
                    if (entry != null && entry.clip == clip) { match = entry; matches++; }
            // Unverified clips remain development-only under SPEC-64's existing release-build check.
            if (matches != 1 || (match.status != ProvenanceStatus.Unverified && match.status != ProvenanceStatus.Confirmed))
            {
                string reason = matches == 0 ? "missing provenance" : matches != 1 ? "duplicate provenance" : match.status.ToString();
                context.Report.Error("MIG-E061", file.RelativePath, field + ".Clip", "Audio cue skipped: " + path + " (" + reason + ").");
                return;
            }
            try
            {
                context.Writer.CreateOrUpdate<AudioCueDefinition>("Audio", id, so =>
                {
                    var writer = context.Writer;
                    writer.Set(so, "clips", p => { p.arraySize = 1; p.GetArrayElementAtIndex(0).objectReferenceValue = clip; });
                    writer.Set(so, "volume", p => p.floatValue = (float)volume);
                    writer.Set(so, "bus", p => p.intValue = id.StartsWith("cue_music_", StringComparison.Ordinal) ? (int)AudioBus.Music : (int)AudioBus.Sfx);
                    // AUD-007 owns the SPEC-64 playback table, additional cues and config wiring.
                }, out _);
            }
            catch (Exception e) { context.Report.Error("MIG-E099", file.RelativePath, field, "Audio conversion failed: " + e.Message); }
        }

        static string ClipPath(string path)
        {
            if (path == null) return null;
            string suffix, folder;
            if (path.StartsWith(LegacyMusic, StringComparison.Ordinal)) { suffix = path.Substring(LegacyMusic.Length); folder = "Music"; }
            else if (path.StartsWith(LegacySfx, StringComparison.Ordinal)) { suffix = path.Substring(LegacySfx.Length); folder = "Sfx"; }
            else return null;
            // The migration maps a file name, never an arbitrary asset outside the audio inventory.
            if (suffix.Length == 0 || suffix.Contains('/') || suffix.Contains('\\') || suffix == "." || suffix == "..") return null;
            return "Assets/Game/Audio/" + folder + "/" + suffix;
        }
    }
}
