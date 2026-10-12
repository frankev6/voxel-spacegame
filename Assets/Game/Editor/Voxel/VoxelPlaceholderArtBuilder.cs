using System;
using System.IO;
using Game.Core;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Editor.Voxel
{
    /// <summary>Procedural, replaceable inputs for SPEC-11 REN-008 / ADR-0040; no legacy bitmap sources.</summary>
    public static class VoxelPlaceholderArtBuilder
    {
        const string Root = "Assets/Game/Art/_Placeholder";
        const int Size = 256, Layers = 64;
        static readonly string[] PaintHex =
        {
            "A89060", "3A3F48", "8E1F35", "1F6B55", "23458F", "B8902A", "A8501F", "5A2E8C",
            "1D7F95", "9A3F72", "4D3428", "0E1014", "34432F", "33506E", "8A7A5E", "4F1622",
            "C9CED6", "A89060", "34362F", "B23A2A", "BFD6FF", "FF5530", "FFB347", "9FE0C8",
            "FFB347", "FFAE4A", "B23A2A", "0D0E0C", "FFFFFF", "CFE6FF", "000000", "121315"
        };
        [MenuItem("Game/Voxel/Build Placeholder Brick Art")]
        public static void Build()
        {
            string textures = Root + "/Textures", materials = Root + "/Materials";
            Directory.CreateDirectory(textures); Directory.CreateDirectory(materials); AssetDatabase.Refresh();
            var albedo = new Texture2DArray(Size, Size, Layers, TextureFormat.BC7, true, false) { name = "BrickAlbedoArray", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear };
            var normal = new Texture2DArray(Size, Size, Layers, TextureFormat.BC5, true, true) { name = "BrickNormalArray", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear };
            var mask = new Texture2DArray(Size, Size, Layers, TextureFormat.BC7, true, true) { name = "BrickMaskArray", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear };
            try
            {
                for (int layer = 0; layer < Layers; layer++)
                {
                    var pixels = new Color[Size * Size];
                    // Fine neutral metal noise is a technical substitute for the forbidden legacy image, not a new hull design.
                    Color tint = layer == 0 ? Hex("A3A49C") : Color.Lerp(Hex("A3A49C"), Hex(PaintHex[layer % PaintHex.Length]), .12f);
                    for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
                    {
                        uint h = Hash((uint)(x + y * Size)); float grain = .96f + .08f * (h & 65535) / 65535f;
                        pixels[x + y * Size] = layer == 63 ? (((x / 16 + y / 16) & 1) == 0 ? Color.magenta : Color.black) : new Color(tint.r * grain, tint.g * grain, tint.b * grain, 1);
                    }
                    using (var source = new TemporaryTexture(ColourTexture(Size, Size, pixels, true))) CopyCompressed(source.Value, albedo, layer, TextureFormat.BC7);
                    using (var source = new TemporaryTexture(FlatTexture(Size, Size, new Color(.5f, .5f, 1, 1), true, true))) CopyCompressed(source.Value, normal, layer, TextureFormat.BC5);
                    using (var source = new TemporaryTexture(FlatTexture(Size, Size, new Color(.6f, .4f, layer == 63 ? 0 : 1, 0), true, true))) CopyCompressed(source.Value, mask, layer, TextureFormat.BC7);
                }
                albedo.Apply(false, false); normal.Apply(false, false); mask.Apply(false, false);
                albedo = Save(albedo, textures + "/BrickAlbedoArray.asset");
                normal = Save(normal, textures + "/BrickNormalArray.asset");
                mask = Save(mask, textures + "/BrickMaskArray.asset");
                var parameters = new Texture2D(64, 2, TextureFormat.RGBAHalf, false, true) { name = "BrickLayerParams", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                for (int i = 0; i < Layers; i++) { parameters.SetPixel(i, 0, Color.clear); parameters.SetPixel(i, 1, new Color(1, 1, 1, 0)); }
                parameters.Apply(); parameters = Save(parameters, textures + "/BrickLayerParams.asset");
                var palette = new Texture2D(256, 1, TextureFormat.RGBA32, false, false) { name = "PaintPalette", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                for (int i = 0; i < 256; i++) palette.SetPixel(i, 0, Hex(PaintHex[i < PaintHex.Length ? i : 0]));
                palette.Apply(); palette = Save(palette, textures + "/PaintPalette.asset");
                var crack = ScalarTexture("CrackTex", (x, y) =>
                {
                    float ux = x / 32f, uy = y / 32f;
                    float jitter = ((Hash((uint)(y / 16)) & 255) / 255f - .5f) * .3f;
                    return Mathf.Min(Mathf.Abs(ux + jitter - Mathf.Round(ux + jitter)), Mathf.Abs(uy - Mathf.Round(uy)));
                });
                crack = Save(crack, textures + "/CrackTex.asset");
                var noise = ScalarTexture("MacroNoiseTex", (x, y) => (Hash((uint)(x + y * Size)) & 65535) / 65535f);
                noise = Save(noise, textures + "/MacroNoiseTex.asset");
                var rampPixels = new Color[256]; var toonPixels = new Color[256];
                for (int x = 0; x < 256; x++) { rampPixels[x] = Color.Lerp(Color.white, new Color(.7f, .7f, .7f, 1), x / 255f); float v = x < 85 ? .35f : x < 170 ? .7f : 1; toonPixels[x] = new Color(v, v, v, 1); }
                var ramp = Save(ColourTexture(256, 1, rampPixels, false), textures + "/DamageRamp.asset"); ramp.wrapMode = TextureWrapMode.Clamp; EditorUtility.SetDirty(ramp);
                var toon = Save(ColourTexture(256, 1, toonPixels, false), textures + "/ToonRamp.asset"); toon.wrapMode = TextureWrapMode.Clamp; EditorUtility.SetDirty(toon);
                var propAlbedo = SaveCompressedFlat("PropAlbedo", Hex("A3A49C"), false, TextureFormat.BC7, textures);
                var propMask = SaveCompressedFlat("PropMask", new Color(.6f, .4f, 1, 0), true, TextureFormat.BC7, textures);
                var propNormal = SaveCompressedFlat("PropNormal", new Color(.5f, .5f, 1, 1), true, TextureFormat.BC5, textures);
                foreach (string kind in new[] { "Brick", "Prop" })
                {
                    var shader = Shader.Find(kind == "Brick" ? "Game/Voxel/BrickLit" : "Game/Voxel/PropInstanced");
                    if (!shader) throw new InvalidOperationException("Missing " + kind + " shader");
                    var material = new Material(shader) { name = kind + "Placeholder", enableInstancing = true };
                    material.SetTexture("_BrickAlbedoArray", albedo); material.SetTexture("_BrickNormalArray", normal); material.SetTexture("_BrickMaskArray", mask);
                    material.SetTexture("_BrickLayerParams", parameters); material.SetTexture("_PaintPalette", palette);
                    material.SetTexture("_CrackTex", crack); material.SetTexture("_DamageRamp", ramp); material.SetTexture("_MacroNoiseTex", noise); material.SetTexture("_ToonRamp", toon);
                    material.SetVector("_TeamPrimary", Hex("A89060").linear); material.SetVector("_TeamSecondary", Hex("34362F").linear); material.SetVector("_TeamAccent", Hex("B23A2A").linear);
                    // NOCTURNE is physical wear/embers, not the superseded glowing-seam concept (DESIGN-GUIDE §5.4).
                    material.SetFloat("_SeamEmission", 0); material.SetFloat("_RimStrength", 0); material.SetColor("_EmberColor", Hex("FF6A2A"));
                    if (kind == "Prop") { material.SetTexture("_PropAlbedo", propAlbedo); material.SetTexture("_PropMask", propMask); material.SetTexture("_PropNormal", propNormal); }
                    Save(material, materials + "/" + kind + "Placeholder.mat");
                }
                AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); GameLog.Info("Voxel", "Built procedural brick/prop placeholders (ADR-0040).");
            }
            catch
            {
                if (albedo && !AssetDatabase.Contains(albedo)) Object.DestroyImmediate(albedo);
                if (normal && !AssetDatabase.Contains(normal)) Object.DestroyImmediate(normal);
                if (mask && !AssetDatabase.Contains(mask)) Object.DestroyImmediate(mask);
                throw;
            }
        }
        static uint Hash(uint x) { unchecked { x ^= x >> 16; x *= 0x7feb352d; x ^= x >> 15; x *= 0x846ca68b; return x ^ (x >> 16); } }
        static Color Hex(string value) { if (!ColorUtility.TryParseHtmlString("#" + value, out var color)) throw new InvalidOperationException(value); return color; }
        static Texture2D ColourTexture(int width, int height, Color[] pixels, bool mips)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, mips, false);
            texture.SetPixels(pixels);
            // Albedo mips must average in linear light, including the final mean used for paint luminance preservation.
            for (int mip = 1; mip < texture.mipmapCount; mip++)
            {
                int w = Mathf.Max(1, width / 2), h = Mathf.Max(1, height / 2); var next = new Color[w * h];
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                {
                    Color sum = Color.clear;
                    for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < 2; dx++) sum += pixels[Mathf.Min(height - 1, y * 2 + dy) * width + Mathf.Min(width - 1, x * 2 + dx)].linear;
                    next[y * w + x] = (sum * .25f).gamma;
                }
                texture.SetPixels(next, mip); pixels = next; width = w; height = h;
            }
            texture.Apply(false, false); return texture;
        }
        static Texture2D FlatTexture(int w, int h, Color color, bool mips, bool linear)
        {
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, mips, linear); var pixels = new Color[w * h];
            Array.Fill(pixels, color); texture.SetPixels(pixels); texture.Apply(mips, false); return texture;
        }
        static Texture2D ScalarTexture(string name, Func<int, int, float> sample)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.R8, true, true) { name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear };
            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++) pixels[x + y * Size] = new Color(sample(x, y), 0, 0, 1);
            texture.SetPixels(pixels); texture.Apply(); return texture;
        }
        static Texture2D SaveCompressedFlat(string name, Color color, bool linear, TextureFormat format, string folder)
        {
            var texture = FlatTexture(2048, 2048, color, true, linear); texture.name = name;
            EditorUtility.CompressTexture(texture, format, TextureCompressionQuality.Normal);
            return Save(texture, folder + "/" + name + ".asset");
        }
        static void CopyCompressed(Texture2D source, Texture2DArray array, int layer, TextureFormat format)
        {
            EditorUtility.CompressTexture(source, format, TextureCompressionQuality.Normal);
            for (int mip = 0; mip < source.mipmapCount; mip++) array.SetPixelData(source.GetPixelData<byte>(mip), mip, layer);
        }
        static T Save<T>(T asset, string path) where T : Object
        {
            asset.name = Path.GetFileNameWithoutExtension(path);
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing) { EditorUtility.CopySerialized(asset, existing); Object.DestroyImmediate(asset); EditorUtility.SetDirty(existing); return existing; }
            AssetDatabase.CreateAsset(asset, path); return asset;
        }
        sealed class TemporaryTexture : IDisposable
        {
            public readonly Texture2D Value;
            public TemporaryTexture(Texture2D value) { Value = value; }
            public void Dispose() => Object.DestroyImmediate(Value);
        }
    }
}
