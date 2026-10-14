using System;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Art
{
    /// <summary>QA-001: bakes the NOCTURNE brick look (DESIGN-GUIDE §3, §5.2) into the shared brick inputs.
    /// Writes layer rows 2/3 for the plating (NocturnePlating.hlsl), function layers with the guide's machinery and light
    /// colours, the ARK team colours and the baked plate maps (ArtSource/Bricks/bake_plating.py) on every brick material,
    /// the plate maps on the prop material, and each block's material layer by function.</summary>
    public static class NocturneBrickLook
    {
        const string LayerParams = "Assets/Game/Art/_Placeholder/Textures/BrickLayerParams.asset";
        static readonly string[] Materials =
        {
            "Assets/Game/Content/Config/BrickPool.mat", "Assets/Game/ShipEditor/Rendering/EditorBrick.mat",
            "Assets/Game/Art/_Placeholder/Materials/BrickPlaceholder.mat"
        };
        const string PropMaterial = "Assets/Game/Art/_Placeholder/Materials/PropPlaceholder.mat";
        const string Plating = "Assets/Game/Art/Nocturne/Plating/Plating_";
        public const int Hull = 0, Armour = 1, Engine = 2, Weapon = 3, Power = 4, Shield = 5, Cargo = 6, Utility = 7, XenoTissue = 8, XenoOrgan = 9;

        struct Layer
        {
            public int Index, Style; public float Windows, Relief, Metallic, Glow; public string Override, Emission;
        }
        static readonly Layer[] Layers =
        {
            // Hull plate takes the paint (team primary by default); sodium windows on vertical faces (§3.3: 7.5).
            new Layer { Index = Hull, Style = 1, Windows = .55f, Relief = 1, Metallic = .35f, Emission = "FFAE4A", Glow = 7.5f },
            new Layer { Index = Armour, Style = 2, Relief = 1.2f, Metallic = .6f },
            // Function machinery uses the guide's function paint (§3.2) and light colours (§3.3) regardless of player paint.
            new Layer { Index = Engine, Style = 3, Relief = 1, Metallic = .7f, Override = "4E524D", Emission = "BFD6FF", Glow = 4 },
            new Layer { Index = Weapon, Style = 3, Relief = 1, Metallic = .6f, Override = "7A2E24", Emission = "FF5530", Glow = 5 },
            new Layer { Index = Power, Style = 3, Relief = 1, Metallic = .55f, Override = "B08A2E", Emission = "FFB347", Glow = 5 },
            new Layer { Index = Shield, Style = 3, Relief = 1, Metallic = .6f, Override = "3E4A46", Emission = "9FE0C8", Glow = 4 },
            new Layer { Index = Cargo, Style = 4, Relief = 1, Metallic = .4f, Override = "5E6448" },
            new Layer { Index = Utility, Style = 3, Relief = 1, Metallic = .7f, Override = "34362F", Emission = "FFAE4A", Glow = 2 },
            // ADR-0115 Xeno, colourway Obsidian & acid (§7): style 5 shades grown tissue; row 0 is the vein colour, Windows = organ glow.
            new Layer { Index = XenoTissue, Style = 5, Windows = 0, Metallic = .05f, Override = "121315", Emission = "B8FF2E", Glow = 1 },
            new Layer { Index = XenoOrgan, Style = 5, Windows = .45f, Metallic = .05f, Override = "121315", Emission = "B8FF2E", Glow = 1 },
        };

        [MenuItem("Game/Art/Bake NOCTURNE Brick Look")]
        public static void Bake()
        {
            var parameters = AssetDatabase.LoadAssetAtPath<Texture2D>(LayerParams) ?? throw new InvalidOperationException("Missing " + LayerParams);
            var rows01 = new Color[64 * 2];
            for (int x = 0; x < 64; x++) for (int y = 0; y < 2; y++) rows01[x + y * 64] = parameters.height > y ? parameters.GetPixel(x, y) : y == 1 ? new Color(1, 1, 1, 0) : Color.clear;
            parameters.Reinitialize(64, 4, TextureFormat.RGBAHalf, false);
            for (int x = 0; x < 64; x++)
            {
                parameters.SetPixel(x, 0, rows01[x]); parameters.SetPixel(x, 1, rows01[x + 64]);
                parameters.SetPixel(x, 2, Color.clear); parameters.SetPixel(x, 3, Color.clear);
            }
            foreach (var layer in Layers)
            {
                Color glow = layer.Emission == null ? Color.clear : Hex(layer.Emission).linear * layer.Glow;
                Color colour = layer.Override == null ? Color.black : Hex(layer.Override).linear;
                parameters.SetPixel(layer.Index, 0, new Color(glow.r, glow.g, glow.b, 0));
                parameters.SetPixel(layer.Index, 1, new Color(1, 1, 1, 0));
                parameters.SetPixel(layer.Index, 2, new Color(layer.Style, layer.Windows, layer.Relief, layer.Override == null ? 0 : 1));
                parameters.SetPixel(layer.Index, 3, new Color(colour.r, colour.g, colour.b, layer.Metallic));
            }
            parameters.Apply(false, false); EditorUtility.SetDirty(parameters);
            foreach (string path in Materials)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) continue;
                // ARK fallback team colours (§3.1/§3.4 company livery): worn-grey plate, ochre secondary, oxblood accent.
                material.SetVector("_TeamPrimary", Hex("A3A49C").linear); material.SetVector("_TeamSecondary", Hex("A89060").linear);
                material.SetVector("_TeamAccent", Hex("B23A2A").linear);
                material.SetFloat("_AOStrength", .75f); material.SetColor("_AOColor", Hex("0D0E0C"));
                material.SetFloat("_MacroStrength", .025f); material.SetFloat("_EmissiveIntensity", 1);
                AssignPlating(material);
            }
            AssignPlating(AssetDatabase.LoadAssetAtPath<Material>(PropMaterial));
            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:BlockDefinition", new[] { "Assets/Game/Content/Blocks" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var block = AssetDatabase.LoadMainAssetAtPath(path); if (block == null) continue;
                int layer = LayerFor(block.name); if (layer < 0) continue;
                var so = new SerializedObject(block); var field = so.FindProperty("materialLayer");
                if (field.intValue == layer) continue;
                field.intValue = layer; so.ApplyModifiedPropertiesWithoutUndo(); changed++;
            }
            AssetDatabase.SaveAssets();
            GameLog.Info("Art", "Baked NOCTURNE brick look: " + Layers.Length + " layers, " + changed + " block layer changes.");
        }
        static void AssignPlating(Material material)
        {
            if (material == null) return;
            foreach (string map in new[] { "A", "B", "N" })
                material.SetTexture("_Plating" + map, AssetDatabase.LoadAssetAtPath<Texture2DArray>(Plating + map + ".png")
                    ?? throw new InvalidOperationException("Missing " + Plating + map + ".png (run ArtSource/Bricks/bake_plating.py)"));
            EditorUtility.SetDirty(material);
        }
        static int LayerFor(string id)
        {
            if (id == "block_missing") return -1;
            if (id.Contains("xeno")) return id == "block_xeno_tissue" || id == "block_xeno_sac" ? XenoTissue : XenoOrgan;
            if (id.StartsWith("block_hull", StringComparison.Ordinal)) return Hull;
            if (id.Contains("thruster")) return Engine;
            if (id.StartsWith("weapon_", StringComparison.Ordinal)) return Weapon;
            if (id.Contains("reactor") || id.Contains("capacitor") || id.Contains("core")) return Power;
            if (id.Contains("shield")) return Shield;
            if (id.Contains("cargo")) return Cargo;
            return Utility;
        }
        static Color Hex(string value) => ColorUtility.TryParseHtmlString("#" + value, out var colour) ? colour : throw new InvalidOperationException(value);
    }
}
