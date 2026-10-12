using System.Collections.Generic;
using UnityEngine;

namespace Game.Editor.Audio
{
    /// <summary>SPEC-64 §3.14, §4.4: editor-only licence inventory.</summary>
    [CreateAssetMenu(menuName = "Game/Audio Provenance", fileName = "AudioProvenance")]
    public sealed class AudioProvenanceTable : ScriptableObject
    {
        public List<AudioProvenanceEntry> entries = new List<AudioProvenanceEntry>();
        internal const string AssetPath = "Assets/Game/Audio/AudioProvenance.asset";
        internal static AudioClip[] FindClips()
        {
            var ids = UnityEditor.AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Game/Audio" });
            var clips = new AudioClip[ids.Length];
            for (int i = 0; i < ids.Length; i++) clips[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(UnityEditor.AssetDatabase.GUIDToAssetPath(ids[i]));
            return clips;
        }
        internal List<string> InventoryErrors(IEnumerable<AudioClip> clips)
        {
            var errors = new List<string>(); var expected = new HashSet<AudioClip>(clips);
            foreach (var clip in expected)
            {
                int count = 0;
                foreach (var entry in entries) if (entry != null && entry.clip == clip) count++;
                if (count != 1) errors.Add(clip.name + ": expected one provenance row, found " + count);
            }
            foreach (var entry in entries)
                if (entry == null || entry.clip == null || !expected.Contains(entry.clip)) errors.Add("Provenance row has a missing or out-of-inventory clip.");
            return errors;
        }
    }
}
