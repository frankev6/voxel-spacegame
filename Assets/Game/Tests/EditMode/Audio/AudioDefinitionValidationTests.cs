using System.Linq;
using Game.Data.Definitions;
using Game.Data.Validation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static Game.Tests.EditMode.Data.TestContentFactory;

namespace Game.Tests.EditMode.Audio
{
    /// <summary>AUD-001: AudioCueDefinition and AudioConfigDefinition validation (SPEC-64 §4.3; codes in SPEC-64 open question 12).</summary>
    public sealed class AudioDefinitionValidationTests
    {
        [TearDown] public void TearDown() => DestroyAll();

        static AudioClip Clip() => AudioClip.Create("test", 64, 1, 44100, false);

        static void SetClips(SerializedObject so, params AudioClip[] clips)
        {
            var p = so.FindProperty("clips");
            p.arraySize = clips.Length;
            for (int i = 0; i < clips.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
        }

        static AudioCueDefinition Cue(string id, AudioBus bus = AudioBus.Sfx, bool loop = false, System.Action<SerializedObject> edit = null)
            => Create<AudioCueDefinition>(id, so =>
            {
                SetClips(so, Clip());
                Set(so, "bus", (int)bus); Set(so, "loop", loop);
                if (bus != AudioBus.Sfx) Set(so, "spatialBlend", 0f);
                edit?.Invoke(so);
            });

        static string[] Codes(ValidationReport r) => r.Entries.Select(e => e.Code).ToArray();

        [Test]
        public void InvalidCue_ReportsErrors()
        {
            Assert.AreEqual(0, Validate(Cue("cue_ok")).ErrorCount, "valid cue");
            CollectionAssert.AreEqual(new[] { "AUD-E001" }, Codes(Validate(Cue("cue_a", edit: so => SetClips(so)))), "no clips");
            CollectionAssert.AreEqual(new[] { "AUD-E001" }, Codes(Validate(Cue("cue_b", edit: so => SetClips(so, Clip(), null)))), "null clip");
            CollectionAssert.AreEqual(new[] { "AUD-E002" }, Codes(Validate(Cue("cue_c", edit: so => { Set(so, "minDistance", 600f); Set(so, "maxDistance", 600f); }))), "min ≥ max");
            CollectionAssert.AreEqual(new[] { "AUD-E003" }, Codes(Validate(Cue("cue_d", AudioBus.Ui, edit: so => Set(so, "spatialBlend", 0.5f)))), "spatial UI cue");
            Assert.AreEqual(0, Validate(Cue("cue_e", AudioBus.Ui)).ErrorCount, "2D UI cue");
        }

        [Test]
        public void InvalidConfig_ReportsErrors()
        {
            var sfx = Cue("cue_sfx");
            var notLooping = Cue("cue_music_not_looping", AudioBus.Music, loop: false);
            var ambienceOnSfx = Cue("cue_ambience_wrong_bus", AudioBus.Sfx, loop: true);
            var config = Create<AudioConfigDefinition>("config_audio", so =>
            {
                Set(so, "musicMenu", notLooping);
                Set(so, "ambienceWorld", ambienceOnSfx);
                Set(so, "impactKinetic", sfx);
                Set(so, "combatEnterSignals", 0);
                var scenes = so.FindProperty("sceneMusic");
                scenes.arraySize = 3;   // drops BlueprintLab
            });
            var r = Validate(config);
            var codes = Codes(r);
            Assert.AreEqual(5, codes.Count(c => c == "AUD-E010"), "mixer and four groups: " + r.ToText());
            Assert.AreEqual(11, codes.Count(c => c == "AUD-E011"), "eleven empty UI sounds");
            Assert.AreEqual(4, codes.Count(c => c == "AUD-E012"), "three missing music cues and one that does not loop");
            Assert.AreEqual(1, codes.Count(c => c == "AUD-E013"), "ambience on the wrong bus");
            Assert.AreEqual(9, codes.Count(c => c == "AUD-E014"), "nine missing combat cues");
            Assert.AreEqual(1, codes.Count(c => c == "AUD-E015"), "combatEnterSignals 0");
            Assert.AreEqual(1, codes.Count(c => c == "AUD-E016"), "BlueprintLab scene missing");
            Assert.AreEqual(r.ErrorCount, codes.Length, "only errors");

            var wrongCount = Create<AudioConfigDefinition>("config_audio_b", so => so.FindProperty("uiSounds").arraySize = 10);
            Assert.AreEqual(1, Codes(Validate(wrongCount)).Count(c => c == "AUD-E011"), "wrong UI sound count is one error");
        }
    }
}
