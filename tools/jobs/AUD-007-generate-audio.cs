// Lead-only AUD-007 recipe. No new clips, licence decisions or Force writes.
// From project root, copy into an unused temporary Editor source (preserve pre-existing source/metas):
//   test ! -e Assets/Game/Editor/AUD007GenerationJob.cs && test ! -e Assets/Game/Editor/AUD007GenerationJob.cs.meta && cp tools/jobs/AUD-007-generate-audio.cs Assets/Game/Editor/AUD007GenerationJob.cs
// With Editor closed and the lead's Unity channel free (docs/08-tools.md §3):
//   mkdir -p Logs/tools
//   . tools/_env.sh
//   "$(unity_editor)" -batchmode -nographics -projectPath "$PWD" -executeMethod Game.Editor.GenerateAudioContent.Run -logFile Logs/tools/aud007-content.log
// After process exit, retain Logs/aud007/generation-readback.txt and remove only the temporary source/meta:
//   rm Assets/Game/Editor/AUD007GenerationJob.cs Assets/Game/Editor/AUD007GenerationJob.cs.meta
// Commit the 30 cue assets/metas, config_audio asset/meta, ContentDatabase and Import/Migration/audio-content-manifest.json.
// Preserve Import/Migration/manifest.json unchanged: its stale MIG-011 hashes prevent later Run All from resetting AUD playback fields.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Validation;
using Game.Editor.Audio;
using Game.Editor.Content;
using UnityEditor;

namespace Game.Editor
{
    /// <summary>Lead-run SPEC-64 AUD-007 production import, exact repeat bytes/GUIDs and catalog/provenance readback.</summary>
    public static class GenerateAudioContent
    {
        public static void Run()
        {
            try
            {
                const string root = "Assets/Game/Content/Audio";
                const string migrationManifest = "Import/Migration/manifest.json";
                byte[] legacyHashes = File.ReadAllBytes(migrationManifest);
                var expected = new HashSet<string>(LegacyAudioCueImporter.CueIds, StringComparer.Ordinal);
                foreach (string guid in AssetDatabase.FindAssets("t:AudioCueDefinition", new[] { root }))
                {
                    var cue = AssetDatabase.LoadAssetAtPath<AudioCueDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                    if (cue == null || !expected.Contains(cue.IdString)) throw new InvalidOperationException("Unexpected audio cue; preserve/review before generation: " + AssetDatabase.GUIDToAssetPath(guid));
                }
                var first = LegacyAudioCueImporter.Run();
                if (first.HasErrors) throw new InvalidOperationException(first.ToText());
                var cues = AssetDatabase.FindAssets("t:AudioCueDefinition", new[] { root }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToArray();
                if (cues.Length != 30) throw new InvalidOperationException("Expected exactly 30 audio cues, found " + cues.Length + ". Preserve/review unexpected assets.");
                var config = AssetDatabase.LoadAssetAtPath<AudioConfigDefinition>(root + "/config_audio.asset");
                if (config == null || config.IdString != "config_audio") throw new InvalidOperationException("Audio config readback failed.");
                var report = new ValidationReport("AUD-007 readback");
                config.Validate(report.For(root + "/config_audio.asset"), null);
                var table = AssetDatabase.LoadAssetAtPath<AudioProvenanceTable>(AudioProvenanceTable.AssetPath);
                var referenced = new HashSet<UnityEngine.AudioClip>();
                foreach (string path in cues)
                {
                    var cue = AssetDatabase.LoadAssetAtPath<AudioCueDefinition>(path); cue.Validate(report.For(path), null);
                    foreach (var clip in cue.Clips)
                    {
                        referenced.Add(clip);
                        var rows = table.entries.Where(e => e != null && e.clip == clip).ToArray();
                        if (rows.Length != 1 || (rows[0].status != ProvenanceStatus.Unverified && rows[0].status != ProvenanceStatus.Confirmed) ||
                            Path.GetFileName(AssetDatabase.GetAssetPath(clip)) == "ComfyUI_00018_.mp3" || Path.GetFileName(AssetDatabase.GetAssetPath(clip)) == "upbeat.mp3")
                            throw new InvalidOperationException("Missing/Blocked provenance: " + path);
                    }
                }
                if (report.HasErrors) throw new InvalidOperationException(report.ToText());
                var database = ContentDatabaseBuilder.Rebuild();
                foreach (string path in cues.Append(root + "/config_audio.asset"))
                    if (!database.Definitions.Contains(AssetDatabase.LoadAssetAtPath<ContentDefinition>(path))) throw new InvalidOperationException("Catalog missing " + path);
                var paths = cues.SelectMany(p => new[] { p, p + ".meta" }).Concat(new[] {
                    root + "/config_audio.asset", root + "/config_audio.asset.meta", ContentDatabaseBuilder.DatabasePath,
                    LegacyAudioCueImporter.ManifestPath }).ToArray();
                var bytes = paths.ToDictionary(p => p, File.ReadAllBytes);
                var guids = cues.Append(root + "/config_audio.asset").ToDictionary(p => p, AssetDatabase.AssetPathToGUID);
                var second = LegacyAudioCueImporter.Run(); if (second.HasErrors) throw new InvalidOperationException(second.ToText());
                foreach (string path in paths) if (!bytes[path].SequenceEqual(File.ReadAllBytes(path))) throw new InvalidOperationException("Repeat changed bytes: " + path);
                foreach (var pair in guids) if (string.IsNullOrEmpty(pair.Value) || pair.Value != AssetDatabase.AssetPathToGUID(pair.Key)) throw new InvalidOperationException("GUID readback failed: " + pair.Key);
                if (!legacyHashes.SequenceEqual(File.ReadAllBytes(migrationManifest))) throw new InvalidOperationException("MIG ownership manifest changed.");
                Directory.CreateDirectory("Logs/aud007");
                string result = "AUD-007: 20 legacy + 10 new cues; validated config; catalog registered; second import identical bytes/GUIDs/ownership.\n" +
                    "No Blocked references. Unverified clips remain dev-only; AUD-011 owner licence gate stays open.\n" +
                    string.Join("\n", guids.Select(p => p.Key + ": GUID " + p.Value)) + "\n" + report.ToText();
                File.WriteAllText("Logs/aud007/generation-readback.txt", result); GameLog.Info("Audio", result); EditorApplication.Exit(0);
            }
            catch (Exception e) { GameLog.Error("Audio", e.ToString()); EditorApplication.Exit(1); }
        }
    }
}
