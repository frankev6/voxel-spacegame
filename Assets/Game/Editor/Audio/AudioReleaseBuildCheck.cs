using System;
using System.Collections.Generic;
using Game.Data.Definitions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Editor.Audio
{
    /// <summary>SPEC-64 §3.14: development is permitted; release requires confirmed referenced clips.</summary>
    public sealed class AudioReleaseBuildCheck : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report)
        {
            bool development = (report.summary.options & BuildOptions.Development) != 0;
            if (development) return;
            Check(false, AssetDatabase.LoadAssetAtPath<AudioProvenanceTable>(AudioProvenanceTable.AssetPath), FindCues());
        }
        internal static AudioCueDefinition[] FindCues()
        {
            var ids = AssetDatabase.FindAssets("t:AudioCueDefinition");
            var cues = new AudioCueDefinition[ids.Length];
            for (int i = 0; i < ids.Length; i++) cues[i] = AssetDatabase.LoadAssetAtPath<AudioCueDefinition>(AssetDatabase.GUIDToAssetPath(ids[i]));
            return cues;
        }
        internal static void Check(bool development, AudioProvenanceTable table, IEnumerable<AudioCueDefinition> cues)
        {
            if (development) return;
            var referenced = new HashSet<AudioClip>();
            foreach (var cue in cues)
                if (cue != null && cue.Clips != null)
                    foreach (var clip in cue.Clips) if (clip != null) referenced.Add(clip);
            var failures = new List<string>();
            foreach (var clip in referenced)
            {
                AudioProvenanceEntry match = null; int count = 0;
                if (table != null && table.entries != null)
                    foreach (var entry in table.entries)
                        if (entry != null && entry.clip == clip) { match = entry; count++; }
                if (count == 1 && match.status == ProvenanceStatus.Confirmed) continue;
                string path = AssetDatabase.GetAssetPath(clip);
                string status = count == 0 ? "missing provenance" : count != 1 ? "duplicate provenance" : match.status.ToString();
                failures.Add((string.IsNullOrEmpty(path) ? clip.name : path) + " (" + status + ")");
            }
            if (failures.Count == 0) return;
            failures.Sort(StringComparer.Ordinal);
            throw new BuildFailedException("Release audio requires confirmed licences:\n" + string.Join("\n", failures));
        }
    }
}
