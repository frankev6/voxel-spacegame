using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Validation;
using Game.Editor.Audio;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace Game.Editor.Art
{
    /// <summary>Read-only production-asset validation, menu and CLI for SPEC-81 §5.2.</summary>
    public static class ArtAssetValidator
    {
        const string ArtFolder = "Assets/Game/Art";
        const string PropMaterialPath = ArtAssetRules.PropsFolder + "/M_Prop_Trim.mat";
        static readonly HashSet<string> referenceExtensions = new(StringComparer.Ordinal)
            { ".asset", ".mat", ".prefab", ".unity", ".uss", ".uxml", ".vfx", ".shadergraph" };
        static readonly Regex guidPattern = new(@"(?<![a-fA-F0-9])[a-fA-F0-9]{32}(?![a-fA-F0-9])", RegexOptions.CultureInvariant);

        public static void Run(ValidationReport report, bool a2Mode)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            string[] paths = Directory.EnumerateFiles("Assets", "*", SearchOption.AllDirectories)
                .Select(p => p.Replace('\\', '/')).Where(p => !p.EndsWith(".meta", StringComparison.Ordinal)).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            var licences = ReadLicences(ArtAssetRules.LicenseRegisterPath);
            var placeholders = new HashSet<string>(paths.Where(IsPlaceholder), StringComparer.Ordinal);
            foreach (var row in licences)
                if ((row.Value == "placeholder-only" || row.Value == "remove") && IsAudioClip(row.Key)) placeholders.Add(row.Key);
            // AST-003 will complete LICENSES. Until then the canonical retained inventory identifies legacy clips.
            var provenance = AssetDatabase.LoadAssetAtPath<AudioProvenanceTable>(AudioProvenanceTable.AssetPath);
            if (provenance != null)
                foreach (var entry in provenance.entries)
                    if (entry?.clip != null && entry.status != ProvenanceStatus.Confirmed)
                    {
                        string path = AssetDatabase.GetAssetPath(entry.clip);
                        if (!licences.TryGetValue(path, out string status) || status != "verified") placeholders.Add(path);
                    }

            var placeholderGuids = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in placeholders.OrderBy(p => p, StringComparer.Ordinal))
            {
                string guid = AssetDatabase.AssetPathToGUID(path);
                if (!string.IsNullOrEmpty(guid)) placeholderGuids[guid] = path;
            }
            var propMeshes = new HashSet<Mesh>();
            var visuals = new Dictionary<BlockDefinition, BlockVisualDefinition>();
            foreach (string guid in AssetDatabase.FindAssets("t:BlockVisualDefinition", new[] { "Assets/Game/Content" }).OrderBy(g => g, StringComparer.Ordinal))
            {
                var visual = AssetDatabase.LoadAssetAtPath<BlockVisualDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (visual == null) continue;
                if (visual.Block != null && !visuals.ContainsKey(visual.Block)) visuals.Add(visual.Block, visual);
                if (visual.PropMeshLod1 != null) propMeshes.Add(visual.PropMeshLod1);
                if (visual.Parts != null) foreach (var part in visual.Parts)
                {
                    if (part.meshLod0 != null) propMeshes.Add(part.meshLod0);
                    if (part.meshLod1 != null) propMeshes.Add(part.meshLod1);
                }
            }
            var budgets = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (string path in paths)
            {
                if (IsArtFile(path))
                {
                    bool verified = licences.TryGetValue(path, out string status) && status == "verified";
                    ValidateAsset(path, path, report, a2Mode, verified, placeholders.Contains(path));
                    if (!IsPlaceholder(path)) MeasureTextures(path, budgets);
                }
                if (referenceExtensions.Contains(Path.GetExtension(path)) && !IsPlaceholder(path))
                    ValidateReferences(path, placeholderGuids, placeholders, report, a2Mode);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:BlockDefinition", new[] { "Assets/Game/Content" }))
            {
                var block = AssetDatabase.LoadAssetAtPath<BlockDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (block == null || block.Kind == BlockKind.Structural) continue;
                if (block.PropMesh != null) propMeshes.Add(block.PropMesh);
                visuals.TryGetValue(block, out var visual);
                ValidateBlock(block, visual, report);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:ArtDirectionDefinition", new[] { "Assets/Game/Content" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var direction = AssetDatabase.LoadAssetAtPath<ArtDirectionDefinition>(path);
                if (direction != null) ValidateMaterial(direction.PropMaterial, path, "propMaterial", report, a2Mode);
            }
            foreach (string path in paths)
            {
                if (IsPlaceholder(path)) continue;
                if (path.EndsWith(".prefab", StringComparison.Ordinal) || path.EndsWith(".fbx", StringComparison.Ordinal))
                    ValidateRenderers(AssetDatabase.LoadAssetAtPath<GameObject>(path), path, propMeshes, report, a2Mode);
                else if (path.EndsWith(".unity", StringComparison.Ordinal)) ValidateSceneRenderers(path, propMeshes, report, a2Mode);
            }
            foreach (Mesh mesh in propMeshes)
                if (!IsPlaceholder(AssetDatabase.GetAssetPath(mesh))) ValidateTexelDensity(mesh, report);
            ValidateMemory(budgets, report);
            ValidateManifest(paths, report);
        }

        [MenuItem("Game/Art/Validate Assets")]
        static void RunMenu()
        {
            var report = new ValidationReport("Art assets (A1)");
            Run(report, false);
            report.LogTo("Art", int.MaxValue);
        }

        public static void RunCli()
        {
            bool a2Mode = Environment.GetCommandLineArgs().Contains("-a2Mode", StringComparer.Ordinal);
            int exitCode = 1;
            try
            {
                var report = new ValidationReport(a2Mode ? "Art assets (A2)" : "Art assets (A1)");
                Run(report, a2Mode);
                // Preserve every entry; a logging cap must not hide a placeholder reference from the CLI evidence.
                Debug.Log(report.ToText(int.MaxValue));
                exitCode = report.HasErrors ? 1 : 0;
            }
            catch (Exception exception) { Debug.LogException(exception); }
            EditorApplication.Exit(exitCode);
        }

        internal static void ValidateAsset(string path, string rulePath, ValidationReport report, bool a2Mode, bool verified, bool retainedPlaceholder = false)
        {
            if (!verified && IsLicenceFile(rulePath)) StageIssue(report, a2Mode, "AST-E007", path, "licence", "No verified row in " + ArtAssetRules.LicenseRegisterPath + ".");
            if (IsPlaceholder(rulePath) || retainedPlaceholder) return;
            string pattern = NamingPattern(rulePath);
            if (pattern != null && !ArtImportPostprocessor.Matches(rulePath, pattern))
                report.Error("AST-E001", path, "name", "Expected " + pattern + ".");
            var importer = AssetImporter.GetAtPath(path);
            if (importer is TextureImporter textureImporter)
            {
                var problems = ArtImportPostprocessor.TextureSettings(textureImporter, rulePath, false);
                if (problems.Count > 0) report.Error("AST-E002", path, "import", "Import settings differ: " + string.Join(", ", problems) + ".");
                textureImporter.GetSourceTextureWidthAndHeight(out int width, out int height);
                ValidateTextureSize(path, rulePath, width, height, report);
            }
            else if (importer is ModelImporter model && ArtImportPostprocessor.IsProductionModel(rulePath))
            {
                if (model.globalScale != 1 || model.isReadable || model.importNormals != ModelImporterNormals.Import ||
                    model.importTangents != ModelImporterTangents.CalculateMikk || model.meshCompression != ModelImporterMeshCompression.Off ||
                    model.materialImportMode != ModelImporterMaterialImportMode.None || model.importAnimation || model.importCameras || model.importLights)
                    report.Error("AST-E002", path, "import", "Mesh import settings differ from SPEC-81 §5.1.");
            }
            else if (importer is AudioImporter audio && ArtImportPostprocessor.IsProductionAudio(rulePath))
            {
                bool sfx = ArtImportPostprocessor.Under(rulePath, ArtAssetRules.AudioFolder + "/Sfx");
                double seconds = 0;
                bool durationRead = !sfx || ArtImportPostprocessor.TryWavDuration(path, out seconds);
                bool matches = durationRead && CheckSettings(audio.defaultSampleSettings);
                if (audio.ContainsSampleSettingsOverride("Standalone")) matches &= CheckSettings(audio.GetOverrideSampleSettings("Standalone"));
                if (!matches) report.Error("AST-E002", path, "import", "Audio import settings differ from SPEC-81 §5.1, or WAV duration could not be read.");
                bool CheckSettings(AudioImporterSampleSettings settings) => sfx ? audio.forceToMono &&
                    settings.loadType == ArtImportPostprocessor.SfxLoadType(seconds) && settings.compressionFormat == AudioCompressionFormat.Vorbis && Mathf.Approximately(settings.quality, .7f) :
                    settings.loadType == AudioClipLoadType.Streaming && audio.loadInBackground;
            }
            // Generated LUTs and arrays have no TextureImporter. Validate the real imported dimensions without converting them to PNG.
            if (importer is not TextureImporter)
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (asset is Texture texture) ValidateTextureSize(path, rulePath, texture.width, texture.height, report);
        }

        static string NamingPattern(string path)
        {
            string extension = Path.GetExtension(path);
            if (ArtImportPostprocessor.Under(path, ArtAssetRules.BricksFolder + "/Layers")) return ArtAssetRules.BrickLayerSourcePattern;
            if (ArtImportPostprocessor.Under(path, ArtAssetRules.PropsFolder + "/Meshes")) return ArtAssetRules.MeshPattern;
            if (ArtImportPostprocessor.Under(path, ArtAssetRules.IconsFolder)) return ArtAssetRules.IconPattern;
            if (ArtImportPostprocessor.Under(path, ArtAssetRules.ThemeFolder + "/Glyphs")) return ArtAssetRules.GlyphPattern;
            if (ArtImportPostprocessor.Under(path, ArtAssetRules.ThemeFolder + "/Sprites")) return ArtAssetRules.UiSpritePattern;
            if (ArtImportPostprocessor.Under(path, ArtAssetRules.ThemeFolder + "/Cursors")) return ArtAssetRules.CursorPattern;
            if (ArtImportPostprocessor.Under(path, ArtAssetRules.ThemeFolder + "/Fonts"))
            {
                if (extension == ".ttf" || extension == ".otf") return ArtAssetRules.FontSourcePattern;
                if (extension == ".asset") return ArtAssetRules.FontAssetPattern;
                return null; // OFL.txt accompanies the source, rather than being a font.
            }
            if (ArtImportPostprocessor.Under(path, ArtAssetRules.AudioFolder + "/Sfx")) return ArtAssetRules.SfxPattern;
            if (ArtImportPostprocessor.Under(path, ArtAssetRules.AudioFolder + "/Music")) return ArtAssetRules.MusicPattern;
            if (ArtImportPostprocessor.Under(path, ArtAssetRules.AudioFolder + "/Ambience")) return ArtAssetRules.AmbiencePattern;
            if (extension == ".png" || AssetImporter.GetAtPath(path) is TextureImporter) return ArtAssetRules.TexturePattern;
            if (extension == ".mat") return ArtAssetRules.MaterialPattern;
            if (extension == ".fbx" || AssetImporter.GetAtPath(path) is ModelImporter) return ArtAssetRules.MeshPattern;
            if (extension == ".vfx") return ArtAssetRules.VfxPattern;
            if (extension == ".shader" || extension == ".shadergraph" || extension == ".shadersubgraph" || extension == ".hlsl") return ArtAssetRules.ShaderPattern;
            if (extension == ".asset" && ArtImportPostprocessor.Under(path, ArtAssetRules.BricksFolder + "/Arrays")) return ArtAssetRules.TextureArrayPattern;
            if (extension == ".asset" && AssetDatabase.LoadAssetAtPath<Texture>(path) != null)
                return ArtAssetRules.TexturePattern.Replace(@"\.png$", @"\.asset$");
            if (ArtImportPostprocessor.Under(path, ArtAssetRules.ArtContentFolder) && extension == ".asset") return @"^[a-z][a-z0-9_]*\.asset$";
            return null; // Preserve SPEC-60's existing USS/TSS/components and non-art definition contracts (Q16).
        }

        static void ValidateTextureSize(string path, string rulePath, int width, int height, ValidationReport report)
        {
            int max = ArtImportPostprocessor.Under(rulePath, ArtAssetRules.VfxFolder) ? ArtAssetRules.MaxVfxTextureSize : ArtAssetRules.MaxTextureSize;
            int exact = ArtImportPostprocessor.Under(rulePath, ArtAssetRules.BricksFolder + "/Layers") ? ArtAssetRules.BrickLayerSize :
                ArtImportPostprocessor.Under(rulePath, ArtAssetRules.IconsFolder) ? ArtAssetRules.IconShippedSize :
                ArtImportPostprocessor.Under(rulePath, ArtAssetRules.PropsFolder + "/Textures") && Path.GetFileName(rulePath).StartsWith("T_Prop_Trim_", StringComparison.Ordinal) ? ArtAssetRules.TrimSheetSize : 0;
            if (width > max || height > max || exact > 0 && (width != exact || height != exact))
                report.Error("AST-E004", path, "size", $"Source/imported size {width}×{height}; expected {(exact > 0 ? exact + "×" + exact : "at most " + max + "×" + max)}.");
        }

        internal static void ValidateBlock(BlockDefinition block, BlockVisualDefinition visual, ValidationReport report)
        {
            long lod0 = TriangleCount(block.PropMesh), lod1 = TriangleCount(visual != null ? visual.PropMeshLod1 : null);
            if (visual?.Parts != null) foreach (var part in visual.Parts) { lod0 += TriangleCount(part.meshLod0); lod1 += TriangleCount(part.meshLod1); }
            int cells = block.Cells?.Count ?? 0;
            Check(lod0, ArtAssetRules.Lod0Budget(cells), "LOD0");
            Check(lod1, ArtAssetRules.Lod1Budget(cells), "LOD1");
            void Check(long measured, int target, string level)
            {
                if (measured > target) report.Warning("AST-E003", AssetDatabase.GetAssetPath(block), level,
                    $"Whole block ({cells} cells): {measured} triangles, target {target}. Advisory under ADR-0042.");
            }
        }

        static long TriangleCount(Mesh mesh)
        {
            if (mesh == null) return 0;
            long count = 0;
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                if (mesh.GetTopology(submesh) == MeshTopology.Triangles) count += mesh.GetIndexCount(submesh) / 3;
            return count;
        }

        internal static void ValidateRenderers(GameObject root, string path, HashSet<Mesh> assignedMeshes, ValidationReport report, bool a2Mode)
        {
            if (root == null) return;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null || !assignedMeshes.Contains(mesh) && !ArtImportPostprocessor.Under(AssetDatabase.GetAssetPath(mesh), ArtAssetRules.PropsFolder)) continue;
                var materials = renderer.sharedMaterials;
                if (materials.Length == 0) ValidateMaterial(null, path, renderer.name + ".materials", report, a2Mode);
                for (int i = 0; i < materials.Length; i++) ValidateMaterial(materials[i], path, renderer.name + ".materials[" + i + "]", report, a2Mode);
            }
        }

        internal static void ValidateMaterial(Material material, string path, string field, ValidationReport report, bool a2Mode)
        {
            string materialPath = AssetDatabase.GetAssetPath(material);
            // A1 retained placeholders are already reported by E006; do not turn that staged warning into a material error.
            if (IsPlaceholder(materialPath)) return;
            if (material == null || materialPath != PropMaterialPath)
                report.Error("AST-E005", path, field, "Expected the shared " + PropMaterialPath + "; found " + (material == null ? "no material" : materialPath) + ".");
        }

        internal static void ValidateSceneRenderers(string path, HashSet<Mesh> assignedMeshes, ValidationReport report, bool a2Mode)
        {
            // Inspect YAML without opening scenes or changing the running Editor's scene state.
            var meshReferences = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Mesh mesh in assignedMeshes)
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string guid, out long localId)) meshReferences.Add(guid + ":" + localId);
            var propObjects = new HashSet<string>(StringComparer.Ordinal);
            string[] objects = Regex.Split(File.ReadAllText(path), @"(?m)^--- !u!");
            foreach (string obj in objects)
            {
                if (!obj.StartsWith("33 ", StringComparison.Ordinal) && !obj.StartsWith("137 ", StringComparison.Ordinal)) continue;
                var mesh = Regex.Match(obj, @"m_Mesh: \{fileID: (-?\d+), guid: ([a-fA-F0-9]{32})");
                string meshPath = mesh.Success ? AssetDatabase.GUIDToAssetPath(mesh.Groups[2].Value) : "";
                if (mesh.Success && (meshReferences.Contains(mesh.Groups[2].Value + ":" + mesh.Groups[1].Value) || ArtImportPostprocessor.Under(meshPath, ArtAssetRules.PropsFolder)))
                    propObjects.Add(Regex.Match(obj, @"m_GameObject: \{fileID: (-?\d+)\}").Groups[1].Value);
            }
            foreach (string obj in objects)
            {
                if (obj.StartsWith("1001 ", StringComparison.Ordinal)) ValidateSceneOverrides(obj, path, assignedMeshes, report, a2Mode);
                if (!obj.StartsWith("23 ", StringComparison.Ordinal) && !obj.StartsWith("137 ", StringComparison.Ordinal)) continue;
                if (!propObjects.Contains(Regex.Match(obj, @"m_GameObject: \{fileID: (-?\d+)\}").Groups[1].Value)) continue;
                var materials = Regex.Match(obj, @"(?m)^  m_Materials:\s*\n((?:  -[^\n]*\n)*)");
                if (!materials.Success || materials.Groups[1].Length == 0) ValidateMaterial(null, path, "m_Materials", report, a2Mode);
                else foreach (string line in materials.Groups[1].Value.Split('\n'))
                {
                    if (line.Length == 0) continue;
                    var guid = guidPattern.Match(line);
                    ValidateMaterial(guid.Success ? AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid.Value)) : null,
                        path, "m_Materials", report, a2Mode);
                }
            }
        }

        static void ValidateSceneOverrides(string obj, string path, HashSet<Mesh> assignedMeshes, ValidationReport report, bool a2Mode)
        {
            var overrides = Regex.Matches(obj, @"(?m)^\s*- target: ([^\n]*)\n\s*propertyPath: ([^\n]*)\n\s*value: ([^\n]*)\n\s*objectReference: ([^\n]*)");
            foreach (Match change in overrides)
            {
                string property = change.Groups[2].Value.Trim();
                if (!property.StartsWith("m_Materials.Array.data[", StringComparison.Ordinal) && property != "m_Materials.Array.size") continue;
                var targetGuid = guidPattern.Match(change.Groups[1].Value);
                var targetId = Regex.Match(change.Groups[1].Value, @"fileID: (-?\d+)");
                if (!targetGuid.Success || !targetId.Success) continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(targetGuid.Value));
                if (prefab == null) continue;
                foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                {
                    if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer, out string guid, out long localId) ||
                        !string.Equals(guid, targetGuid.Value, StringComparison.OrdinalIgnoreCase) || localId.ToString() != targetId.Groups[1].Value) continue;
                    Mesh mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                    if (mesh == null || !assignedMeshes.Contains(mesh) && !ArtImportPostprocessor.Under(AssetDatabase.GetAssetPath(mesh), ArtAssetRules.PropsFolder)) continue;
                    if (property == "m_Materials.Array.size")
                    {
                        if (change.Groups[3].Value.Trim() == "0") ValidateMaterial(null, path, property, report, a2Mode);
                    }
                    else
                    {
                        var materialGuid = guidPattern.Match(change.Groups[4].Value);
                        ValidateMaterial(materialGuid.Success ? AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(materialGuid.Value)) : null,
                            path, property, report, a2Mode);
                    }
                }
            }
        }

        internal static void ValidateReferences(string path, IReadOnlyDictionary<string, string> guids, IEnumerable<string> placeholderPaths, ValidationReport report, bool a2Mode)
        {
            string text = File.ReadAllText(path);
            var targets = new SortedSet<string>(StringComparer.Ordinal);
            foreach (Match match in guidPattern.Matches(text)) if (guids.TryGetValue(match.Value, out string target)) targets.Add(target);
            foreach (string target in placeholderPaths) if (text.Contains(target, StringComparison.Ordinal)) targets.Add(target);
            foreach (string target in targets) StageIssue(report, a2Mode, "AST-E006", path, "references", "References retained placeholder " + target + ".");
        }

        static void ValidateTexelDensity(Mesh mesh, ValidationReport report)
        {
            using var snapshot = MeshUtility.AcquireReadOnlyMeshData(mesh);
            var data = snapshot[0];
            string path = AssetDatabase.GetAssetPath(mesh);
            if (!data.HasVertexAttribute(VertexAttribute.TexCoord0))
            {
                report.Warning("AST-W001", path, "uv0", "Prop has no UV0; texel density is unavailable.");
                return;
            }
            using var vertices = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp);
            using var uvs = new NativeArray<Vector2>(data.vertexCount, Allocator.Temp);
            data.GetVertices(vertices); data.GetUVs(0, uvs);
            double area = 0, uvArea = 0;
            for (int submesh = 0; submesh < data.subMeshCount; submesh++)
            {
                var descriptor = data.GetSubMesh(submesh);
                if (descriptor.topology != MeshTopology.Triangles) continue;
                using var indices = new NativeArray<int>(descriptor.indexCount, Allocator.Temp);
                data.GetIndices(indices, submesh, true);
                for (int i = 0; i + 2 < indices.Length; i += 3)
                {
                    int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                    area += Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]).magnitude * .5;
                    Vector2 x = uvs[b] - uvs[a], y = uvs[c] - uvs[a];
                    uvArea += Math.Abs(x.x * y.y - x.y * y.x) * .5;
                }
            }
            if (area <= 0) { report.Warning("AST-W001", path, "uv0", "Prop has no non-degenerate triangle area; texel density is unavailable."); return; }
            double density = ArtAssetRules.TrimSheetSize * Math.Sqrt(uvArea / area);
            if (Math.Abs(density - ArtAssetRules.PropTexelsPerCell) > ArtAssetRules.PropTexelsPerCell * ArtAssetRules.PropTexelDensityTolerance)
                report.Warning("AST-W001", path, "uv0", $"Area-weighted density {density:F1} px/cell; expected {ArtAssetRules.PropTexelsPerCell} ±25%.");
        }

        static void MeasureTextures(string path, Dictionary<string, long> budgets)
        {
            string group = ArtImportPostprocessor.Under(path, ArtAssetRules.ThemeFolder) ? "UI" :
                ArtImportPostprocessor.Under(path, ArtAssetRules.BricksFolder + "/Layers") ? null :
                ArtImportPostprocessor.Under(path, ArtAssetRules.BricksFolder + "/Arrays") ? "Brick arrays" :
                ArtImportPostprocessor.Under(path, ArtAssetRules.BricksFolder) ? "Brick support" :
                ArtImportPostprocessor.Under(path, ArtAssetRules.PropsFolder) ? "Prop trim" :
                ArtImportPostprocessor.Under(path, ArtAssetRules.VfxFolder) ? "VFX" :
                ArtImportPostprocessor.Under(path, ArtAssetRules.SpaceFolder + "/Bodies") ? "Bodies" :
                ArtImportPostprocessor.Under(path, ArtAssetRules.SpaceFolder) ? "Space" : null;
            if (group == null) return;
            // LoadAllAssets includes generated textures/font atlases. Count each object once, not references to it.
            foreach (Texture texture in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Texture>().Distinct())
            {
                budgets.TryGetValue(group, out long previous);
                budgets[group] = previous + Profiler.GetRuntimeMemorySizeLong(texture);
            }
        }

        static void ValidateMemory(Dictionary<string, long> measured, ValidationReport report)
        {
            foreach (var group in new[] { ("Brick arrays", ArtAssetRules.BrickArraysBudget), ("Brick support", ArtAssetRules.BrickSupportBudget),
                ("Prop trim", ArtAssetRules.PropTrimBudget), ("VFX", ArtAssetRules.VfxBudget), ("Space", ArtAssetRules.SpaceBudget),
                ("Bodies", ArtAssetRules.BodiesBudget), ("UI", ArtAssetRules.UiBudget) })
            {
                measured.TryGetValue(group.Item1, out long bytes);
                Check(group.Item1, bytes, group.Item2);
            }
            long total = measured.Values.Sum();
            Check("Total", total, ArtAssetRules.TotalBudget);
            Check("Total hard cap", total, ArtAssetRules.TotalHardCap);
            void Check(string group, long bytes, long target)
            {
                report.Info("AST-I001", ArtFolder, group, $"Imported texture memory: {bytes} bytes; target {target} bytes (sources/placeholders excluded).");
                if (bytes > target) report.Warning("AST-E008", ArtFolder, group, $"{bytes} bytes exceeds {target} bytes. Advisory under ADR-0042.");
            }
        }

        static void ValidateManifest(string[] paths, ValidationReport report)
        {
            var existing = new HashSet<string>(paths, StringComparer.Ordinal);
            var planned = new HashSet<string>(StringComparer.Ordinal);
            if (File.Exists(ArtAssetRules.ManifestPath))
                foreach (string line in File.ReadLines(ArtAssetRules.ManifestPath).Skip(1))
                {
                    var columns = CsvColumns(line);
                    if (columns.Count != 9) { report.Warning("AST-W002", ArtAssetRules.ManifestPath, "row", "Malformed nine-column manifest row."); continue; }
                    if (columns[8] == "dropped") continue;
                    string path = columns[2];
                    if (!planned.Add(path)) report.Warning("AST-W002", path, "manifest", "Duplicate manifest path.");
                    if (!existing.Contains(path)) report.Warning("AST-W002", path, "manifest", "Manifest row has no file (status " + columns[8] + ").");
                }
            else report.Warning("AST-W002", ArtAssetRules.ManifestPath, "manifest", "Manifest is unavailable.");
            foreach (string path in paths)
                if (IsArtFile(path) && !planned.Contains(path)) report.Warning("AST-W002", path, "manifest", "File has no manifest row.");
        }

        static List<string> CsvColumns(string line)
        {
            var columns = new List<string>();
            var value = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (quoted && i + 1 < line.Length && line[i + 1] == '"') { value.Append('"'); i++; }
                    else quoted = !quoted;
                }
                else if (c == ',' && !quoted) { columns.Add(value.ToString()); value.Clear(); }
                else value.Append(c);
            }
            columns.Add(value.ToString());
            return quoted ? new List<string>() : columns;
        }

        internal static Dictionary<string, string> ReadLicences(string path)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!File.Exists(path)) return result;
            foreach (string line in File.ReadLines(path))
            {
                string[] columns = line.Split('|');
                if (columns.Length != 10) continue;
                string assetPath = columns[1].Trim().Trim('`');
                if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal)) continue;
                string status = columns[8].Trim();
                // A conflicting/duplicated row cannot silently certify shipping eligibility.
                if (result.ContainsKey(assetPath)) result[assetPath] = "duplicate";
                else result.Add(assetPath, status);
            }
            return result;
        }

        static bool IsLicenceFile(string path) => ArtImportPostprocessor.Under(path, ArtFolder) || ArtImportPostprocessor.Under(path, ArtAssetRules.ThemeFolder) || ArtImportPostprocessor.Under(path, ArtAssetRules.AudioFolder);
        static bool IsArtFile(string path) => IsLicenceFile(path) || ArtImportPostprocessor.Under(path, ArtAssetRules.ArtContentFolder);
        static bool IsPlaceholder(string path) => ArtImportPostprocessor.Under(path, ArtAssetRules.PlaceholderFolder);
        static bool IsAudioClip(string path) => ArtImportPostprocessor.Under(path, ArtAssetRules.AudioFolder) &&
            new[] { ".wav", ".ogg", ".mp3", ".aiff", ".aif" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
        static void StageIssue(ValidationReport report, bool a2Mode, string code, string path, string field, string message) =>
            report.Add(a2Mode ? ValidationSeverity.Error : ValidationSeverity.Warning, code, path, field, message);
    }
}
