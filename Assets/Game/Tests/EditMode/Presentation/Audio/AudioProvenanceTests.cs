using System.Collections.Generic;
using Game.Data.Definitions;
using Game.Editor.Audio;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests.EditMode.Presentation.Audio
{
    public sealed class AudioProvenanceTests
    {
        static AudioProvenanceTable Table()
        {
            var table = AssetDatabase.LoadAssetAtPath<AudioProvenanceTable>(AudioProvenanceTable.AssetPath);
            Assert.IsNotNull(table); return table;
        }
        [Test] public void EveryClip_HasOneRow()
        {
            var table = Table(); var clips = AudioProvenanceTable.FindClips();
            Assert.Greater(clips.Length, 0); CollectionAssert.IsEmpty(table.InventoryErrors(clips));
            var added = AudioClip.Create("Unlisted clip", 8, 1, 8000, false);
            try
            {
                var withNewClip = new List<AudioClip>(clips) { added };
                Assert.That(table.InventoryErrors(withNewClip), Has.Some.Contains("Unlisted clip"), "Adding an unlisted clip must fail the same inventory check");
            }
            finally { Object.DestroyImmediate(added); }
        }
        [Test] public void NoCue_ReferencesBlocked()
        {
            var blocked = new HashSet<AudioClip>();
            foreach (var entry in Table().entries) if (entry.status == ProvenanceStatus.Blocked) blocked.Add(entry.clip);
            foreach (var cue in AudioReleaseBuildCheck.FindCues())
                foreach (var clip in cue.Clips) Assert.IsFalse(blocked.Contains(clip), cue.name + " references blocked " + clip.name);
        }
        [Test] public void ReleaseCheck_FailsOnUnverified()
        {
            var table = ScriptableObject.CreateInstance<AudioProvenanceTable>();
            var cue = ScriptableObject.CreateInstance<AudioCueDefinition>();
            var first = AudioClip.Create("Unverified first", 8, 1, 8000, false);
            var second = AudioClip.Create("Unverified second", 8, 1, 8000, false);
            try
            {
                table.entries.Add(new AudioProvenanceEntry { clip = first }); table.entries.Add(new AudioProvenanceEntry { clip = second });
                SetClips(cue, first, second);
                var error = Assert.Throws<BuildFailedException>(() => AudioReleaseBuildCheck.Check(false, table, new[] { cue }));
                StringAssert.Contains(first.name, error.Message); StringAssert.Contains(second.name, error.Message);
                foreach (var entry in table.entries) entry.status = ProvenanceStatus.Confirmed;
                Assert.DoesNotThrow(() => AudioReleaseBuildCheck.Check(false, table, new[] { cue }));
                table.entries[0].status = ProvenanceStatus.Blocked;
                StringAssert.Contains("Blocked", Assert.Throws<BuildFailedException>(() => AudioReleaseBuildCheck.Check(false, table, new[] { cue })).Message);
            }
            finally { Object.DestroyImmediate(cue); Object.DestroyImmediate(table); Object.DestroyImmediate(first); Object.DestroyImmediate(second); }
        }
        [Test] public void ReleaseCheck_PassesDevelopmentBuild()
        {
            var cue = ScriptableObject.CreateInstance<AudioCueDefinition>(); var table = Table();
            try
            {
                var clips = AudioProvenanceTable.FindClips(); SetClips(cue, clips);
                Assert.DoesNotThrow(() => AudioReleaseBuildCheck.Check(true, table, new[] { cue }));
            }
            finally { Object.DestroyImmediate(cue); }
        }
        static void SetClips(AudioCueDefinition cue, params AudioClip[] clips)
        {
            var serialized = new SerializedObject(cue); var property = serialized.FindProperty("clips"); property.arraySize = clips.Length;
            for (int i = 0; i < clips.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
