using Game.App.Boot;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Editor.Content;
using Game.Tests.Common;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode.Voxel
{
    /// <summary>VOX-010: blueprint validation, payload authoring and boot configuration (SPEC-10 §4.7).</summary>
    public sealed class VoxelDefinitionTests
    {
        [Test]
        public void Blueprint_ValidationCodes()
        {
            var definition = ScriptableObject.CreateInstance<ShipBlueprintDefinition>();
            try
            {
                Check(definition, "VOX-E001", "payload");
                var bytes = new byte[] { 1, 2, 3 };
                var min = new Vector3Int(-2, 3, 7); var max = new Vector3Int(2, 6, 9);
                definition.EditorSetPayload(bytes, 19, min, max);
                bytes[0] = 8;
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, definition.Payload.ToArray());
                Assert.AreEqual(19, definition.CellCount); Assert.AreEqual(min, definition.BoundsMin); Assert.AreEqual(max, definition.BoundsMax);
                Check(definition, null, null);
                definition.EditorSetPayload(System.Array.Empty<byte>(), 0, default, default);
                Check(definition, "VOX-E001", "payload");
                definition.EditorSetPayload(new byte[] { 1 }, 1, default, default);
                using var serialized = new SerializedObject(definition);
                serialized.FindProperty("cellSize").floatValue = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo(); Check(definition, "VOX-E002", "cellSize");
                serialized.FindProperty("cellSize").floatValue = 0.5f;
                serialized.ApplyModifiedPropertiesWithoutUndo(); Check(definition, null, null);
                serialized.FindProperty("payloadVersion").intValue = 2;
                serialized.ApplyModifiedPropertiesWithoutUndo(); Check(definition, "VOX-E003", "payloadVersion");
                serialized.FindProperty("payloadVersion").intValue = 1;
                serialized.ApplyModifiedPropertiesWithoutUndo(); Check(definition, null, null);
            }
            finally { Object.DestroyImmediate(definition); }
        }
        static void Check(ShipBlueprintDefinition definition, string code, string field)
        {
            const string path = "ship_fixture";
            var report = new ValidationReport("VOX-010");
            definition.Validate(report.For(path), null);
            Assert.AreEqual(code == null ? 0 : 1, report.ErrorCount, report.ToText());
            if (code == null) return;
            Assert.AreEqual(code, report.Entries[0].Code); Assert.AreEqual(field, report.Entries[0].Field);
            Assert.AreEqual(path, report.Entries[0].AssetPath);
        }

        [Test]
        public void ConfigFactory_CopiesValues()
        {
            var definition = ScriptableObject.CreateInstance<VoxelConfigDefinition>();
            try
            {
                using var serialized = new SerializedObject(definition);
                serialized.FindProperty("spawnBricksPerFrame").intValue = 317;
                serialized.FindProperty("defaultCellSize").floatValue = 0.75f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var config = VoxelConfigFactory.Create(definition);
                Assert.AreEqual(317, config.SpawnBricksPerFrame); Assert.AreEqual(0.75f, config.DefaultCellSize);
                Assert.AreEqual(1, config.RequireRenderer);
            }
            finally { Object.DestroyImmediate(definition); }
            using var content = new ContentService(ContentDatabaseBuilder.Rebuild());
            using var world = EcsTestWorld.Create();
            content.CreateSingletons(world.EntityManager);
            BootSingletons.CreateAll(world.EntityManager, content.Catalog);
            var asset = content.Catalog.Get<VoxelConfigDefinition>(new ContentId("config_voxel"));
            Assert.AreEqual(1024, asset.SpawnBricksPerFrame); Assert.AreEqual(0.5f, asset.DefaultCellSize);
            Assert.AreEqual(VoxelConfigFactory.Create(asset), world.GetSingleton<VoxelConfig>());
            using var query = world.EntityManager.CreateEntityQuery(typeof(VoxelConfig), typeof(BootSingleton));
            Assert.AreEqual(1, query.CalculateEntityCount());
        }
    }
}
