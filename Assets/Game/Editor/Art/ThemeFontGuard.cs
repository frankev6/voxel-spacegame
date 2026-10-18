using UnityEditor;
using UnityEngine.TextCore.Text;

namespace Game.Editor.Art
{
    /// <summary>
    /// Keeps the theme's dynamic font assets coherent in the Editor. Play fills their atlases and the Editor can
    /// save that into the files; resetting the files (git restore) reloads the serialized tables but leaves each
    /// asset's in-memory character lookup pointing at glyphs of the old atlas, so every label draws as a dot.
    /// Rebuilding the lookups after an import, and before every Play, clears that state.
    /// </summary>
    [InitializeOnLoad]
    public sealed class ThemeFontGuard : AssetPostprocessor
    {
        public const string Folder = "Assets/Game/UI/Theme/Fonts";

        static ThemeFontGuard() => EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode) RebuildAll();
        };

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var path in imported)
                if (path.StartsWith(Folder) && path.EndsWith(".asset"))
                    Rebuild(AssetDatabase.LoadAssetAtPath<FontAsset>(path));
        }

        public static void RebuildAll()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:FontAsset", new[] { Folder }))
                Rebuild(AssetDatabase.LoadAssetAtPath<FontAsset>(AssetDatabase.GUIDToAssetPath(guid)));
        }

        static void Rebuild(FontAsset font)
        {
            if (font == null || font.atlasPopulationMode != AtlasPopulationMode.Dynamic) return;
            font.ReadFontAssetDefinition();
        }
    }
}
