using System.IO;
using System.Text.RegularExpressions;
using Game.Data.Validation;
using Game.Editor.Art;
using NUnit.Framework;

namespace Game.Tests.EditMode.Editor.Art
{
    /// <summary>SPEC-81 AST-001: exact naming contracts and the shared footprint budgets.</summary>
    public sealed class ArtAssetRulesTests
    {
        [Test] public void Patterns_AcceptDesignSystemExamples()
        {
            var examples = new (string Pattern, string Name)[]
            {
                (ArtAssetRules.BrickLayerSourcePattern, "TL_00_hull_plain_Albedo.png"),
                (ArtAssetRules.TextureArrayPattern, "TA_Brick_Albedo.asset"),
                (ArtAssetRules.TexturePattern, "T_Prop_Trim_Albedo.png"),
                (ArtAssetRules.TexturePattern, "T_Fx_Spark_Flipbook.png"),
                (ArtAssetRules.MaterialPattern, "M_Brick.mat"),
                (ArtAssetRules.MaterialPattern, "M_Prop_Trim.mat"),
                (ArtAssetRules.MaterialPattern, "M_Sky_Procedural.mat"),
                (ArtAssetRules.ShaderPattern, "SH_Sky_Procedural.shadergraph"),
                (ArtAssetRules.MeshPattern, "SM_weapon_plasma_turret_small_Yaw_LOD0.fbx"),
                (ArtAssetRules.VfxPattern, "VFX_Debris_Cells.vfx"),
                (ArtAssetRules.IconPattern, "ICO_block_hull.png"),
                (ArtAssetRules.GlyphPattern, "GLY_undo.svg"),
                (ArtAssetRules.UiSpritePattern, "UI_Bracket_Focus.png"),
                (ArtAssetRules.CursorPattern, "CUR_Default_32.png"),
                (ArtAssetRules.FontSourcePattern, "FNT_Barlow-Regular.ttf"),
                (ArtAssetRules.FontAssetPattern, "FNT_Barlow-Regular_SDF.asset"),
                (ArtAssetRules.SfxPattern, "SFX_Weapon_PlasmaFire_01.wav"),
                (ArtAssetRules.MusicPattern, "MUS_Explore_DriftingLight.ogg"),
                (ArtAssetRules.AmbiencePattern, "AMB_Station_Hub.ogg")
            };
            foreach (var example in examples) Assert.IsTrue(Regex.IsMatch(example.Name, example.Pattern), example.Name);
            // §10.7 definition/source examples use canonical content IDs, not a separate art-only ID scheme.
            foreach (string name in new[] { "artdir_drydock.asset", "brickmat_drydock.asset", "vis_weapon_railgun_medium.asset" })
                Assert.IsTrue(ContentIdRules.IsValidFormat(Path.GetFileNameWithoutExtension(name)), name);
            const string source = "ArtSource/Blocks/weapon_railgun_medium.blend";
            Assert.IsTrue(source.StartsWith("ArtSource/", System.StringComparison.Ordinal));
            Assert.IsTrue(ContentIdRules.IsValidFormat(Path.GetFileNameWithoutExtension(source)));
        }

        [Test] public void Patterns_RejectSpacesAndWrongCase()
        {
            var examples = new (string Pattern, string Valid, string WrongCase)[]
            {
                (ArtAssetRules.BrickLayerSourcePattern, "TL_00_hull_plain_Albedo.png", "TL_00_hull_plain_albedo.png"),
                (ArtAssetRules.TextureArrayPattern, "TA_Brick_Albedo.asset", "TA_brick_Albedo.asset"),
                (ArtAssetRules.TexturePattern, "T_Prop_Trim_Albedo.png", "T_prop_Trim_Albedo.png"),
                (ArtAssetRules.MaterialPattern, "M_Brick.mat", "m_Brick.mat"),
                (ArtAssetRules.ShaderPattern, "SH_Sky_Procedural.shadergraph", "sh_Sky_Procedural.shadergraph"),
                (ArtAssetRules.MeshPattern, "SM_block_hull_LOD0.fbx", "SM_block_hull_lod0.fbx"),
                (ArtAssetRules.VfxPattern, "VFX_Debris_Cells.vfx", "VFX_debris_Cells.vfx"),
                (ArtAssetRules.IconPattern, "ICO_block_hull.png", "ICO_Block_hull.png"),
                (ArtAssetRules.GlyphPattern, "GLY_undo.svg", "GLY_Undo.svg"),
                (ArtAssetRules.UiSpritePattern, "UI_Bracket_Focus.png", "UI_bracket_Focus.png"),
                (ArtAssetRules.CursorPattern, "CUR_Default_32.png", "CUR_default_32.png"),
                (ArtAssetRules.FontSourcePattern, "FNT_Barlow-Regular.ttf", "fnt_Barlow-Regular.ttf"),
                (ArtAssetRules.FontAssetPattern, "FNT_Barlow-Regular_SDF.asset", "FNT_Barlow-Regular_sdf.asset"),
                (ArtAssetRules.SfxPattern, "SFX_Weapon_PlasmaFire_01.wav", "SFX_weapon_PlasmaFire_01.wav"),
                (ArtAssetRules.MusicPattern, "MUS_Explore_DriftingLight.ogg", "MUS_explore_DriftingLight.ogg"),
                (ArtAssetRules.AmbiencePattern, "AMB_Station_Hub.ogg", "AMB_station_Hub.ogg")
            };
            foreach (var example in examples)
            {
                Assert.IsFalse(Regex.IsMatch(example.Valid.Insert(4, " "), example.Pattern), "Space in " + example.Valid);
                Assert.IsFalse(Regex.IsMatch(example.WrongCase, example.Pattern), example.WrongCase);
            }
        }

        [Test] public void Lod0Budget_MatchesTable()
        {
            foreach (var row in new[] { (Cells: 1, Budget: 150), (Cells: 4, Budget: 600), (Cells: 18, Budget: 1500), (Cells: 64, Budget: 3000), (Cells: 65, Budget: 5000) })
                Assert.AreEqual(row.Budget, ArtAssetRules.Lod0Budget(row.Cells), "Footprint " + row.Cells);
        }

        [Test] public void Lod1Budget_IsAtMostQuarterOfLod0()
        {
            // SPEC-81 Q15: preserve the explicit one-cell 40-triangle row instead of silently lowering it to 37.
            Assert.AreEqual(40, ArtAssetRules.Lod1Budget(1));
            for (int cells = 2; cells <= 128; cells++)
                Assert.LessOrEqual(ArtAssetRules.Lod1Budget(cells), ArtAssetRules.Lod0Budget(cells) * .25f, "Footprint " + cells);
        }
    }
}
