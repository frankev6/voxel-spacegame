using Game.App.Boot;
using Game.Data;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Tests.Common;
using Game.Voxel.Rendering;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode.Voxel.Rendering
{
    /// <summary>REN-009 config copying, palette ownership and specified validation failures.</summary>
    public sealed class RenderConfigTests
    {
        const string ConfigPath = "Assets/Game/Content/Config/config_voxel_render.asset";
        [Test] public void Factory_CopiesValues()
        {
            var def = Object.Instantiate(AssetDatabase.LoadAssetAtPath<VoxelRenderConfigDefinition>(ConfigPath));
            var mesh = new Mesh(); VoxelRenderConfig config = default;
            try
            {
                using (var so = new SerializedObject(def))
                {
                    so.FindProperty("meshBatchMin").intValue = 32; so.FindProperty("meshBatchMax").intValue = 64;
                    so.FindProperty("meshApplyBudgetMs").floatValue = 1.5f; so.FindProperty("lod1Distance").floatValue = 500;
                    so.FindProperty("lod2Distance").floatValue = 2500; so.FindProperty("lodHysteresis").floatValue = .2f;
                    so.FindProperty("dormantDistance").floatValue = 3500; so.FindProperty("releaseCpuMeshCopies").boolValue = true;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                config = VoxelRenderConfigFactory.Create(def, new[] { mesh });
                Assert.AreSame(def.BrickMaterial, config.BrickMaterial.Value); Assert.AreSame(def.PropMaterial, config.PropMaterial.Value);
                Assert.AreSame(def.BrickPoolCopy, config.BrickPoolCopy.Value); Assert.AreSame(mesh, config.PropMeshes[0].Value);
                Assert.AreEqual(32, config.MeshBatchMin); Assert.AreEqual(64, config.MeshBatchMax);
                Assert.AreEqual(1.5f, config.MeshApplyBudgetMs); Assert.AreEqual(500, config.Lod1Distance);
                Assert.AreEqual(2500, config.Lod2Distance); Assert.AreEqual(.2f, config.LodHysteresis);
                Assert.AreEqual(3500, config.DormantDistance); Assert.AreEqual(1, config.ReleaseCpuMeshCopies);
                var palette = config.Palette.Value;
                VoxelRenderConfigFactory.Destroy(ref config);
                Assert.IsFalse(config.PropMeshes.IsCreated); Assert.IsTrue(palette == null);
                VoxelRenderConfigFactory.Destroy(ref config);
                using var content = new ContentService(AssetDatabase.LoadAssetAtPath<ContentDatabase>("Assets/Game/Content/ContentDatabase.asset"));
                using var world = EcsTestWorld.Create();
                content.CreateSingletons(world.EntityManager);
                using (BootSingletons.CreateAll(world.EntityManager, content.Catalog, content.PropMeshes))
                {
                    var actual = world.GetSingleton<VoxelRenderConfig>();
                    Assert.AreEqual(content.PropMeshes.Count, actual.PropMeshes.Length);
                    Assert.AreEqual("Game/Voxel/BrickLitIndirect", actual.BrickMaterial.Value.shader.name);
                    using var query = world.EntityManager.CreateEntityQuery(typeof(VoxelRenderConfig), typeof(BootSingleton));
                    Assert.AreEqual(1, query.CalculateEntityCount());
                }
                using var gone = world.EntityManager.CreateEntityQuery(typeof(VoxelRenderConfig));
                Assert.IsTrue(gone.IsEmptyIgnoreFilter);
            }
            finally { VoxelRenderConfigFactory.Destroy(ref config); Object.DestroyImmediate(mesh); Object.DestroyImmediate(def); }
        }
        [Test] public void PaletteTexture_MatchesColours()
        {
            var def = AssetDatabase.LoadAssetAtPath<VoxelRenderConfigDefinition>(ConfigPath);
            var config = VoxelRenderConfigFactory.Create(def, System.Array.Empty<Mesh>());
            try
            {
                var texture = config.Palette.Value;
                Assert.AreEqual(256, texture.width); Assert.AreEqual(1, texture.height); Assert.AreEqual(1, texture.mipmapCount);
                Assert.AreEqual(TextureFormat.RGBA32, texture.format); Assert.AreEqual(FilterMode.Point, texture.filterMode);
                Assert.AreEqual(TextureWrapMode.Clamp, texture.wrapMode);
                CollectionAssert.AreEqual(def.Palette.Colors, texture.GetPixels32());
            }
            finally { VoxelRenderConfigFactory.Destroy(ref config); }
        }
        [Test] public void Validation_Errors()
        {
            var def = Object.Instantiate(AssetDatabase.LoadAssetAtPath<VoxelRenderConfigDefinition>(ConfigPath));
            var palette = Object.Instantiate(def.Palette);
            try
            {
                foreach (string field in new[] { "brickMaterial", "propMaterial", "palette", "brickPoolCopy" })
                {
                    using var so = new SerializedObject(def); var p = so.FindProperty(field); var previous = p.objectReferenceValue;
                    p.objectReferenceValue = null; so.ApplyModifiedPropertiesWithoutUndo();
                    var report = new ValidationReport("REN-009"); def.Validate(report.For(ConfigPath), null);
                    Assert.AreEqual(1, report.ErrorCount, report.ToText()); Assert.AreEqual(field, report.Entries[0].Field);
                    p.objectReferenceValue = previous; so.ApplyModifiedPropertiesWithoutUndo();
                }
                using (var so = new SerializedObject(palette)) { so.FindProperty("colors").arraySize = 255; so.ApplyModifiedPropertiesWithoutUndo(); }
                var invalid = new ValidationReport("REN-009"); palette.Validate(invalid.For("palette"), null);
                Assert.AreEqual(1, invalid.ErrorCount); Assert.AreEqual("REN-E002", invalid.Entries[0].Code);
            }
            finally { Object.DestroyImmediate(def); Object.DestroyImmediate(palette); }
        }
    }
}
