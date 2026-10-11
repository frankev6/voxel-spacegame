using System;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Audio
{
    /// <summary>SPEC-64 §6.5: enforce the folder's streaming, compression and spatial-audio import policy.</summary>
    public sealed class AudioImportPostprocessor : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            bool music = assetPath.StartsWith("Assets/Game/Audio/Music/", StringComparison.Ordinal);
            bool sfx = assetPath.StartsWith("Assets/Game/Audio/Sfx/", StringComparison.Ordinal);
            bool ambience = assetPath.StartsWith("Assets/Game/Audio/Ambience/", StringComparison.Ordinal);
            if (!music && !sfx && !ambience) return;
            var importer = (AudioImporter)assetImporter; var settings = importer.defaultSampleSettings;
            settings.loadType = music ? AudioClipLoadType.Streaming : sfx ? AudioClipLoadType.DecompressOnLoad : AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat = AudioCompressionFormat.Vorbis; settings.quality = sfx ? .7f : .6f;
            settings.preloadAudioData = !music; importer.defaultSampleSettings = settings;
            importer.forceToMono = sfx; importer.loadInBackground = !sfx;
        }
    }
}
