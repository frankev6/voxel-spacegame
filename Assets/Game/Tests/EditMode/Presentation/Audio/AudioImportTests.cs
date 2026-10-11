using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode.Presentation.Audio
{
    public sealed class AudioImportTests
    {
        static IEnumerable<string> Clips(string area)
        {
            var ids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Game/Audio/" + area }); Assert.Greater(ids.Length, 0);
            foreach (string id in ids) yield return AssetDatabase.GUIDToAssetPath(id);
        }
        [Test] public void MusicFolder_IsStreaming()
        {
            foreach (string path in Clips("Music"))
            {
                var importer = (AudioImporter)AssetImporter.GetAtPath(path); var sample = importer.defaultSampleSettings;
                Assert.AreEqual(AudioClipLoadType.Streaming, sample.loadType, path); Assert.AreEqual(AudioCompressionFormat.Vorbis, sample.compressionFormat, path);
                Assert.AreEqual(.6f, sample.quality, .001f, path); Assert.IsFalse(importer.forceToMono, path);
                Assert.IsFalse(sample.preloadAudioData, path); Assert.IsTrue(importer.loadInBackground, path);
            }
        }
        [Test] public void SfxFolder_DecompressOnLoadMono()
        {
            foreach (string path in Clips("Sfx"))
            {
                var importer = (AudioImporter)AssetImporter.GetAtPath(path); var sample = importer.defaultSampleSettings;
                Assert.AreEqual(AudioClipLoadType.DecompressOnLoad, sample.loadType, path); Assert.AreEqual(AudioCompressionFormat.Vorbis, sample.compressionFormat, path);
                Assert.AreEqual(.7f, sample.quality, .001f, path); Assert.IsTrue(importer.forceToMono, path);
                Assert.IsTrue(sample.preloadAudioData, path); Assert.IsFalse(importer.loadInBackground, path);
                Assert.AreEqual(1, AssetDatabase.LoadAssetAtPath<AudioClip>(path).channels, path);
            }
        }
        [Test] public void SfxDecompressedSize_Under24MB()
        {
            long bytes = 0;
            foreach (string path in Clips("Sfx")) { var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path); bytes += (long)clip.samples * clip.channels * sizeof(float); }
            Debug.Log($"SFX decompressed sample data: {bytes} B."); Assert.Greater(bytes, 0); Assert.LessOrEqual(bytes, 24 * 1024 * 1024);
        }
    }
}
