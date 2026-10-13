using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Art
{
    /// <summary>Applies the production-folder import policy of SPEC-81 §5.1.</summary>
    public sealed class ArtImportPostprocessor : AssetPostprocessor
    {
        // SPEC-64 still owns legacy audio. Only canonical production names opt into this later policy.
        public override int GetPostprocessOrder() => 100;

        void OnPreprocessTexture() => TextureSettings((TextureImporter)assetImporter, assetPath, true);

        void OnPreprocessModel()
        {
            if (!IsProductionModel(assetPath)) return;
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1;
            importer.isReadable = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
        }

        void OnPreprocessAudio()
        {
            if (!IsProductionAudio(assetPath)) return;
            var importer = (AudioImporter)assetImporter;
            var settings = importer.defaultSampleSettings;
            if (Under(assetPath, ArtAssetRules.AudioFolder + "/Sfx"))
            {
                // Read the source RIFF chunks: the old imported clip's length can be stale during reimport.
                if (!TryWavDuration(assetPath, out double seconds)) return;
                settings.loadType = SfxLoadType(seconds);
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = .7f;
                importer.forceToMono = true;
            }
            else
            {
                settings.loadType = AudioClipLoadType.Streaming;
                importer.loadInBackground = true;
            }
            importer.defaultSampleSettings = settings;
            if (importer.ContainsSampleSettingsOverride("Standalone")) importer.SetOverrideSampleSettings("Standalone", settings);
        }

        internal static bool Under(string path, string folder) => path.StartsWith(folder + "/", StringComparison.Ordinal);
        internal static bool Matches(string path, string pattern) => Regex.IsMatch(Path.GetFileName(path), pattern, RegexOptions.CultureInvariant);

        internal static bool IsProductionModel(string path) =>
            (Under(path, ArtAssetRules.PropsFolder + "/Meshes") || Under(path, ArtAssetRules.SpaceFolder)) && Matches(path, ArtAssetRules.MeshPattern);

        internal static bool IsProductionAudio(string path) =>
            Under(path, ArtAssetRules.AudioFolder + "/Sfx") && Matches(path, ArtAssetRules.SfxPattern) ||
            Under(path, ArtAssetRules.AudioFolder + "/Music") && Matches(path, ArtAssetRules.MusicPattern) ||
            Under(path, ArtAssetRules.AudioFolder + "/Ambience") && Matches(path, ArtAssetRules.AmbiencePattern);

        internal static AudioClipLoadType SfxLoadType(double seconds) => seconds < 2 ? AudioClipLoadType.DecompressOnLoad :
            seconds <= 10 ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.Streaming;

        internal static bool TryWavDuration(string path, out double seconds)
        {
            seconds = 0;
            if (!File.Exists(path)) return false;
            using var reader = new BinaryReader(File.OpenRead(path));
            if (reader.BaseStream.Length < 12 || reader.ReadUInt32() != 0x46464952) return false; // RIFF
            reader.ReadUInt32();
            if (reader.ReadUInt32() != 0x45564157) return false; // WAVE
            uint bytesPerSecond = 0;
            long dataBytes = -1;
            while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
            {
                uint kind = reader.ReadUInt32(), size = reader.ReadUInt32();
                long start = reader.BaseStream.Position;
                if (size > reader.BaseStream.Length - start) return false;
                if (kind == 0x20746d66 && size >= 16) // fmt
                {
                    reader.ReadUInt16(); reader.ReadUInt16(); reader.ReadUInt32();
                    bytesPerSecond = reader.ReadUInt32();
                }
                if (kind == 0x61746164) dataBytes = size; // data
                reader.BaseStream.Position = start + size + (size & 1);
            }
            if (bytesPerSecond == 0 || dataBytes < 0) return false;
            seconds = (double)dataBytes / bytesPerSecond;
            return true;
        }

        // The validator uses the same policy without changing or reimporting any asset.
        internal static List<string> TextureSettings(TextureImporter importer, string rulePath, bool apply)
        {
            var problems = new List<string>();
            bool layer = Under(rulePath, ArtAssetRules.BricksFolder + "/Layers") && Matches(rulePath, ArtAssetRules.BrickLayerSourcePattern);
            bool trim = Under(rulePath, ArtAssetRules.PropsFolder + "/Textures") &&
                Path.GetFileName(rulePath).StartsWith("T_Prop_Trim_", StringComparison.Ordinal) && Matches(rulePath, ArtAssetRules.TexturePattern);
            bool icon = Under(rulePath, ArtAssetRules.IconsFolder) && Matches(rulePath, ArtAssetRules.IconPattern);
            bool sprite = Under(rulePath, ArtAssetRules.ThemeFolder + "/Sprites") && Matches(rulePath, ArtAssetRules.UiSpritePattern);
            bool cursor = Under(rulePath, ArtAssetRules.ThemeFolder + "/Cursors") && Matches(rulePath, ArtAssetRules.CursorPattern);
            if (!layer && !trim && !icon && !sprite && !cursor) return problems;

            bool normal = rulePath.EndsWith("_Normal.png", StringComparison.Ordinal);
            bool mask = rulePath.EndsWith("_Mask.png", StringComparison.Ordinal);
            var type = cursor ? TextureImporterType.Cursor : icon || sprite ? TextureImporterType.Sprite :
                normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            var compression = cursor || layer && !normal && !mask ? TextureImporterCompression.Uncompressed : TextureImporterCompression.Compressed;
            int maxSize = layer ? ArtAssetRules.BrickLayerSize : trim ? ArtAssetRules.TrimSheetSize : icon ? ArtAssetRules.IconShippedSize : ArtAssetRules.MaxTextureSize;
            var format = compression == TextureImporterCompression.Uncompressed ? TextureImporterFormat.RGBA32 :
                normal ? TextureImporterFormat.BC5 : TextureImporterFormat.BC7;
            Check(importer.textureType == type, "textureType");
            Check(importer.isReadable == (layer || cursor), "isReadable");
            Check(importer.sRGBTexture == (!normal && !mask), "sRGBTexture");
            Check(importer.maxTextureSize == maxSize, "maxTextureSize");
            Check(importer.textureCompression == compression, "textureCompression");
            if (!cursor)
            {
                Check(importer.mipmapEnabled == (layer || trim), "mipmapEnabled");
                Check(importer.wrapMode == (layer || trim ? TextureWrapMode.Repeat : TextureWrapMode.Clamp), "wrapMode");
            }
            if (icon) Check(importer.spriteImportMode == SpriteImportMode.Single && importer.filterMode == FilterMode.Bilinear, "spriteImportMode/filterMode");
            if (trim) Check(importer.anisoLevel == 4, "anisoLevel");
            if (apply) importer.textureType = type; // Validate/set platform formats for the intended texture type.
            foreach (string platform in new[] { "DefaultTexturePlatform", "Standalone" })
            {
                var settings = platform == "DefaultTexturePlatform" ? importer.GetDefaultPlatformTextureSettings() : importer.GetPlatformTextureSettings(platform);
                // An absent desktop override inherits the explicit default format.
                if (platform == "DefaultTexturePlatform" || settings.overridden)
                    Check(settings.format == format && settings.maxTextureSize == maxSize && settings.textureCompression == compression, platform);
                if (apply)
                {
                    settings.format = format;
                    settings.maxTextureSize = maxSize;
                    settings.textureCompression = compression;
                    if (platform == "Standalone") settings.overridden = true;
                    importer.SetPlatformTextureSettings(settings);
                }
            }
            if (apply)
            {
                importer.textureType = type;
                importer.isReadable = layer || cursor;
                importer.sRGBTexture = !normal && !mask;
                importer.maxTextureSize = maxSize;
                importer.textureCompression = compression;
                if (!cursor)
                {
                    importer.mipmapEnabled = layer || trim;
                    importer.wrapMode = layer || trim ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                }
                if (icon) { importer.spriteImportMode = SpriteImportMode.Single; importer.filterMode = FilterMode.Bilinear; }
                if (trim) importer.anisoLevel = 4;
            }
            return problems;

            void Check(bool matches, string field) { if (!matches) problems.Add(field); }
        }
    }
}
