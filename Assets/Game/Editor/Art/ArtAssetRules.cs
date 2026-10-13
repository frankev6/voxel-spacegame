namespace Game.Editor.Art
{
    /// <summary>Naming, folder and budget rules of DESIGN-SYSTEM §10.7–§10.11. Implements SPEC-81 §4.1. Constants and pure lookups only.</summary>
    public static class ArtAssetRules
    {
        // Folders (DESIGN-SYSTEM §10.8)
        public const string BricksFolder = "Assets/Game/Art/Bricks";
        public const string PropsFolder = "Assets/Game/Art/Props";
        public const string SpaceFolder = "Assets/Game/Art/Space";
        public const string VfxFolder = "Assets/Game/Art/VFX";
        public const string ShadersFolder = "Assets/Game/Art/Shaders";
        public const string LookTestFolder = "Assets/Game/Art/LookTest";
        public const string PlaceholderFolder = "Assets/Game/Art/_Placeholder";
        public const string ThemeFolder = "Assets/Game/UI/Theme";
        public const string IconsFolder = "Assets/Game/UI/Theme/Icons";
        public const string AudioFolder = "Assets/Game/Audio";
        public const string ArtContentFolder = "Assets/Game/Content/Art";
        public const string LicenseRegisterPath = "docs/design/LICENSES.md";
        public const string ManifestPath = "docs/design/asset-manifest.csv";

        // Naming (DESIGN-SYSTEM §10.7), matched against the file name only
        public const string BrickLayerSourcePattern = @"^TL_\d{2}_[a-z0-9_]+_(Albedo|Mask|Normal)\.png$";
        public const string TextureArrayPattern = @"^TA_Brick_(Albedo|Mask|Normal)\.asset$";
        public const string TexturePattern = @"^T_[A-Z][A-Za-z0-9]*_[A-Za-z0-9]+(_[A-Za-z0-9]+)*\.png$";
        public const string MaterialPattern = @"^M_[A-Za-z0-9_]+\.mat$";
        public const string ShaderPattern = @"^SH_[A-Za-z0-9_]+\.(shadergraph|shadersubgraph|shader|hlsl)$";
        public const string MeshPattern = @"^SM_[a-z0-9_]+?(_[A-Z][A-Za-z0-9]*)?_LOD[0-2]\.fbx$";
        public const string VfxPattern = @"^VFX_[A-Z][A-Za-z0-9]*_[A-Za-z0-9]+\.vfx$";
        public const string IconPattern = @"^ICO_[a-z0-9_]+\.png$";
        public const string GlyphPattern = @"^GLY_[a-z0-9_]+\.svg$";
        public const string UiSpritePattern = @"^UI_[A-Z][A-Za-z0-9]*_[A-Za-z0-9]+\.png$";
        public const string CursorPattern = @"^CUR_[A-Z][A-Za-z0-9]*_(32|64)\.png$";
        public const string FontSourcePattern = @"^FNT_[A-Za-z0-9]+-[A-Za-z0-9]+\.(ttf|otf)$";
        public const string FontAssetPattern = @"^FNT_[A-Za-z0-9]+-[A-Za-z0-9]+_SDF\.asset$";
        public const string SfxPattern = @"^SFX_[A-Z][A-Za-z0-9]*_[A-Za-z0-9]+_\d{2}\.wav$";
        public const string MusicPattern = @"^MUS_[A-Z][A-Za-z0-9]*_[A-Za-z0-9]+\.ogg$";
        public const string AmbiencePattern = @"^AMB_[A-Z][A-Za-z0-9]*_[A-Za-z0-9]+\.ogg$";

        // Sizes and densities
        public const int BrickLayerSize = 256;
        public const int TrimSheetSize = 2048;
        public const int IconMasterSize = 256;
        public const int IconShippedSize = 128;
        public const int MaxTextureSize = 2048;
        public const int MaxVfxTextureSize = 1024;
        public const float PropTexelsPerCell = 128f;          // trim sheet density
        public const float PropTexelDensityTolerance = 0.25f; // ±25 %

        // Texture budgets in bytes (DESIGN-SYSTEM §10.10)
        public const long BrickArraysBudget = 18L << 20;
        public const long BrickSupportBudget = 512L << 10;
        public const long PropTrimBudget = 17L << 20;
        public const long VfxBudget = 16L << 20;
        public const long SpaceBudget = 8L << 20;
        public const long BodiesBudget = 8L << 20;
        public const long UiBudget = 24L << 20;
        public const long TotalBudget = 92L << 20;
        public const long TotalHardCap = 128L << 20;

        // Triangle budgets (DESIGN-SYSTEM §10.11)
        public const int ProjectileTriangleBudget = 100;
        public const int GateLod0Budget = 20000, GateLod1Budget = 5000;
        public const int PlanetSphereBudget = 8192, StarSphereBudget = 2048;
        public static int Lod0Budget(int footprintCells) =>
            footprintCells <= 1 ? 150 : footprintCells <= 4 ? 600 : footprintCells <= 18 ? 1500 : footprintCells <= 64 ? 3000 : 5000;
        public static int Lod1Budget(int footprintCells) =>
            footprintCells <= 1 ? 40 : footprintCells <= 4 ? 150 : footprintCells <= 18 ? 375 : footprintCells <= 64 ? 750 : 1250;
    }
}
