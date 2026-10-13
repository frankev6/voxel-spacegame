using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Validation;
using Game.Editor.Art;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode.Editor.Art
{
    /// <summary>The seven SPEC-81 AST-002 checks, using disposable assets under Content/_Test.</summary>
    public sealed class ArtAssetValidatorTests
    {
        const string Root = "Assets/Game/Content/_Test/ArtAssetValidator";

        [SetUp] public void SetUp()
        {
            Assert.IsFalse(Directory.Exists(Root), "A previous test's assets must not be overwritten.");
            Directory.CreateDirectory(Root);
            AssetDatabase.Refresh();
        }

        [TearDown] public void TearDown() => AssetDatabase.DeleteAsset(Root);

        [Test] public void BadName_E001()
        {
            string path = Png("Wrong Name.png", 128);
            var report = Validate(path, ArtAssetRules.IconsFolder + "/Wrong Name.png");
            AssertEntry(report, "AST-E001", path, ValidationSeverity.Error);
            Assert.IsFalse(Validate(path, ArtAssetRules.IconsFolder + "/ICO_block_hull.png").Entries.Any(e => e.Code == "AST-E001"));
        }

        [Test] public void WrongIconImport_E002()
        {
            string path = Png("ICO_block_hull.png", 128);
            string rulePath = ArtAssetRules.IconsFolder + "/ICO_block_hull.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            ArtImportPostprocessor.TextureSettings(importer, rulePath, true);
            importer.SaveAndReimport();
            Assert.IsFalse(Validate(path, rulePath).HasErrors, "The production policy must pass its validator.");
            importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Point;
            var desktop = importer.GetPlatformTextureSettings("Standalone");
            desktop.format = TextureImporterFormat.DXT5;
            importer.SetPlatformTextureSettings(desktop);
            importer.SaveAndReimport();
            var before = EditorJsonUtility.ToJson(importer);
            var report = Validate(path, rulePath);
            AssertEntry(report, "AST-E002", path, ValidationSeverity.Error);
            StringAssert.Contains("mipmapEnabled", report.ToText());
            StringAssert.Contains("Standalone", report.ToText());
            Assert.AreEqual(before, EditorJsonUtility.ToJson(importer), "Validation must not repair or reimport assets.");
        }

        [Test] public void MeshOverBudget_E003()
        {
            var block = ScriptableObject.CreateInstance<BlockDefinition>();
            AssetDatabase.CreateAsset(block, Root + "/block_test.asset");
            Mesh baseMesh = MeshAsset("base", 100);
            SetReference(block, "propMesh", baseMesh);
            baseMesh.UploadMeshData(true);
            Assert.IsFalse(baseMesh.isReadable, "Production meshes must remain countable without a CPU-readable copy.");
            var serialized = new SerializedObject(block);
            serialized.FindProperty("kind").enumValueIndex = (int)BlockKind.Functional;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var visual = ScriptableObject.CreateInstance<BlockVisualDefinition>();
            AssetDatabase.CreateAsset(visual, Root + "/vis_block_test.asset");
            SetReference(visual, "block", block);
            SetReference(visual, "propMeshLod1", MeshAsset("lod1", 41));
            serialized = new SerializedObject(visual);
            var parts = serialized.FindProperty("parts");
            parts.arraySize = 1;
            parts.GetArrayElementAtIndex(0).FindPropertyRelative("meshLod0").objectReferenceValue = MeshAsset("part", 51);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var report = new ValidationReport("test");
            ArtAssetValidator.ValidateBlock(block, visual, report);
            var entries = report.Entries.Where(e => e.Code == "AST-E003").ToArray();
            Assert.AreEqual(2, entries.Length, "LOD0 must include moving parts, and LOD1 uses the explicit 40-triangle row.");
            StringAssert.Contains("151 triangles, target 150", entries.Single(e => e.Field == "LOD0").Message);
            StringAssert.Contains("41 triangles, target 40", entries.Single(e => e.Field == "LOD1").Message);
            Assert.IsTrue(entries.All(e => e.Severity == ValidationSeverity.Warning), "ADR-0042 makes performance-only misses advisory.");
            SetReference(visual, "propMeshLod1", MeshAsset("lod1_valid", 40));
            serialized = new SerializedObject(visual);
            serialized.FindProperty("parts").arraySize = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            report = new ValidationReport("within budget");
            ArtAssetValidator.ValidateBlock(block, visual, report);
            Assert.AreEqual(0, report.Entries.Count);
        }

        [Test] public void WrongLayerSize_E004()
        {
            string path = Png("TL_00_hull_plain_Albedo.png", 512);
            string rulePath = ArtAssetRules.BricksFolder + "/Layers/TL_00_hull_plain_Albedo.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            ArtImportPostprocessor.TextureSettings(importer, rulePath, true);
            importer.SaveAndReimport();
            Assert.AreEqual(256, AssetDatabase.LoadAssetAtPath<Texture2D>(path).width, "The importer hides the source-size error by downscaling.");
            var report = Validate(path, rulePath);
            AssertEntry(report, "AST-E004", path, ValidationSeverity.Error);
            StringAssert.Contains("512×512", report.ToText());
        }

        [Test] public void ForeignMaterialOnProp_E005()
        {
            Mesh mesh = MeshAsset("prop", 1);
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            Assert.IsNotNull(shader);
            var material = new Material(shader);
            AssetDatabase.CreateAsset(material, Root + "/M_Foreign.mat");
            var root = new GameObject("prop");
            try
            {
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                root.AddComponent<MeshRenderer>().sharedMaterial = material;
                string path = Root + "/prop.prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                var report = new ValidationReport("test");
                ArtAssetValidator.ValidateRenderers(prefab, path, new HashSet<Mesh> { mesh }, report, false);
                AssertEntry(report, "AST-E005", path, ValidationSeverity.Error);
                ArtAssetValidator.ValidateMaterial(material, Root + "/artdir_test.asset", "propMaterial", report, false);
                AssertEntry(report, "AST-E005", Root + "/artdir_test.asset", ValidationSeverity.Error);
                Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string meshGuid, out long meshId));
                Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(material, out string materialGuid, out long materialId));
                string sceneText = Root + "/scene-yaml.txt";
                File.WriteAllText(sceneText, "--- !u!33 &2\nMeshFilter:\n  m_GameObject: {fileID: 1}\n  m_Mesh: {fileID: " + meshId + ", guid: " + meshGuid + ", type: 2}\n" +
                    "--- !u!23 &3\nMeshRenderer:\n  m_GameObject: {fileID: 1}\n  m_Materials:\n  - {fileID: " + materialId + ", guid: " + materialGuid + ", type: 2}\n");
                var sceneReport = new ValidationReport("scene renderer");
                ArtAssetValidator.ValidateSceneRenderers(sceneText, new HashSet<Mesh> { mesh }, sceneReport, false);
                AssertEntry(sceneReport, "AST-E005", sceneText, ValidationSeverity.Error);
                Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(prefab.GetComponent<MeshRenderer>(), out string rendererGuid, out long rendererId));
                File.WriteAllText(sceneText, "--- !u!1001 &4\nPrefabInstance:\n  m_Modification:\n    m_Modifications:\n" +
                    "    - target: {fileID: " + rendererId + ", guid: " + rendererGuid + ", type: 3}\n" +
                    "      propertyPath: m_Materials.Array.data[0]\n      value: \n" +
                    "      objectReference: {fileID: " + materialId + ", guid: " + materialGuid + ", type: 2}\n");
                sceneReport = new ValidationReport("scene prefab override");
                ArtAssetValidator.ValidateSceneRenderers(sceneText, new HashSet<Mesh> { mesh }, sceneReport, false);
                AssertEntry(sceneReport, "AST-E005", sceneText, ValidationSeverity.Error);
                // The same arbitrary renderer is not a prop unless its mesh is assigned to a block or under Props.
                report = new ValidationReport("test");
                ArtAssetValidator.ValidateRenderers(prefab, path, new HashSet<Mesh>(), report, false);
                Assert.IsFalse(report.HasErrors);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test] public void PlaceholderReference_E006_WarningInA1_ErrorInA2()
        {
            var texture = new Texture2D(4, 4);
            string target = Root + "/placeholder.asset";
            AssetDatabase.CreateAsset(texture, target);
            string guid = AssetDatabase.AssetPathToGUID(target);
            var guids = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [guid] = target };
            // Cover serialized GUIDs, a direct USS path, and duplicate references without changing real placeholders.
            foreach (string extension in new[] { ".asset", ".mat", ".prefab", ".unity", ".uss", ".uxml", ".vfx", ".shadergraph" })
            {
                string path = Root + "/reference" + extension;
                File.WriteAllText(path, extension == ".uss" ? "url(\"project://database/" + target + "\")" : "guid: " + guid.ToUpperInvariant() + "\nguid: " + guid);
                foreach (bool a2Mode in new[] { false, true })
                {
                    var report = new ValidationReport("test");
                    ArtAssetValidator.ValidateReferences(path, guids, new[] { target }, report, a2Mode);
                    AssertEntry(report, "AST-E006", path, a2Mode ? ValidationSeverity.Error : ValidationSeverity.Warning);
                    Assert.AreEqual(1, report.Entries.Count, "One finding per referenced target in a file.");
                }
            }
        }

        [Test] public void MissingLicenceRow_E007()
        {
            string path = Png("ICO_block_hull.png", 128);
            string rulePath = ArtAssetRules.IconsFolder + "/ICO_block_hull.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            ArtImportPostprocessor.TextureSettings(importer, rulePath, true);
            importer.SaveAndReimport();
            string register = Root + "/LICENSES.txt";
            foreach (string status in new[] { "", "placeholder-only", "remove", "verified" })
            {
                File.WriteAllText(register, "| Path | Kind | Source | Licence | Author | Date | Proof | Status |\n" +
                    (status.Length == 0 ? "" : "| `" + path + "` | icon | own work | - | test | 2026-10-13 | - | " + status + " |\n"));
                var rows = ArtAssetValidator.ReadLicences(register);
                bool verified = rows.TryGetValue(path, out string rowStatus) && rowStatus == "verified";
                foreach (bool a2Mode in new[] { false, true })
                {
                    var report = new ValidationReport("test");
                    ArtAssetValidator.ValidateAsset(path, rulePath, report, a2Mode, verified);
                    if (status == "verified") Assert.IsFalse(report.HasErrors || report.WarningCount > 0);
                    else AssertEntry(report, "AST-E007", path, a2Mode ? ValidationSeverity.Error : ValidationSeverity.Warning);
                }
            }
        }

        static ValidationReport Validate(string path, string rulePath)
        {
            var report = new ValidationReport("test");
            ArtAssetValidator.ValidateAsset(path, rulePath, report, false, true);
            return report;
        }

        static void AssertEntry(ValidationReport report, string code, string path, ValidationSeverity severity)
        {
            Assert.IsTrue(report.Entries.Any(e => e.Code == code && e.AssetPath == path && e.Severity == severity), report.ToText());
        }

        static string Png(string name, int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            string path = Root + "/" + name;
            try { File.WriteAllBytes(path, texture.EncodeToPNG()); }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return path;
        }

        static Mesh MeshAsset(string name, int triangles)
        {
            var mesh = new Mesh { name = name };
            mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
            mesh.triangles = Enumerable.Range(0, triangles * 3).Select(i => i % 3).ToArray();
            AssetDatabase.CreateAsset(mesh, Root + "/" + name + ".asset");
            return mesh;
        }

        static void SetReference(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
