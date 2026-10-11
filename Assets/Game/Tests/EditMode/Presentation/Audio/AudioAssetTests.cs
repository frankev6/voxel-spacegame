using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace Game.Tests.EditMode.Presentation.Audio
{
    public sealed class AudioAssetTests
    {
        const string MixerPath = "Assets/Game/Audio/Mixer/GameMixer.mixer";
        [Test] public void Mixer_HasFourChildGroups()
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath); Assert.NotNull(mixer);
            foreach (string bus in new[] { "Music", "Sfx", "Ui", "Ambience" })
                Assert.AreEqual(1, mixer.FindMatchingGroups("Master/" + bus).Length, bus);
            Assert.AreEqual(5, mixer.FindMatchingGroups("").Length); Assert.NotNull(mixer.FindSnapshot("Default"));
        }
        [Test] public void Mixer_ExposesFiveVolumes()
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath); Assert.NotNull(mixer);
            foreach (string name in new[] { "MasterVolume", "MusicVolume", "SfxVolume", "UiVolume", "AmbienceVolume" })
                Assert.IsTrue(mixer.GetFloat(name, out _), name);
        }
        [Test] public void ProjectVoiceCounts()
        {
            Assert.AreEqual(64, AudioSettings.GetConfiguration().numRealVoices);
            Assert.AreEqual(512, AudioSettings.GetConfiguration().numVirtualVoices);
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/AudioManager.asset")[0]);
            Assert.AreEqual(64, settings.FindProperty("m_RealVoiceCount").intValue);
            Assert.AreEqual(512, settings.FindProperty("m_VirtualVoiceCount").intValue);
        }
    }
}
