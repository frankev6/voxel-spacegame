using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Game.Data.Definitions;
using Game.Data.Validation;
using UnityEditor;

namespace Game.Editor.Migration.Narrative
{
    /// <summary>SPEC-40 §5.8: prepare all dialogue before checking overwrite permission or writing assets.</summary>
    internal static class LegacyNarrativeConverter
    {
        internal const string DefaultRoot = "Assets/Game/Content/Narrative";
        internal const string DefaultManifest = "docs/reference/legacy-narrative-manifest.md";
        internal sealed class Plan
        {
            internal readonly List<ConvertedEncounter> Encounters;
            internal readonly IReadOnlyCollection<FactDeclaration> Facts;
            internal readonly HashSet<string> Nested = new(StringComparer.Ordinal);
            internal readonly SortedDictionary<string, string> TextFiles = new(StringComparer.Ordinal);
            internal readonly LegacyManifestWriter Manifest = new();
            internal readonly Dictionary<string, ContentDefinition> References = new(StringComparer.Ordinal);
            internal readonly string Root, ManifestPath;
            internal Plan(List<ConvertedEncounter> encounters, IReadOnlyCollection<FactDeclaration> facts, string root, string manifestPath)
            { Encounters = encounters; Facts = facts; Root = root; ManifestPath = manifestPath; }
        }

        internal static Plan Prepare(LegacyNarrativeLoader.Sources sources, string root, string manifestPath,
            Func<string, ContentDefinition> resolve, ValidationReport report)
        {
            root = root.Replace('\\', '/').TrimEnd('/');
            if (!root.StartsWith("Assets/", StringComparison.Ordinal) || Array.Exists(root.Split('/'), part => part == ".." || part == "." || part.Length == 0))
                throw new ArgumentException("Narrative output must be an Assets subfolder.", nameof(root));
            if (string.IsNullOrWhiteSpace(manifestPath)) throw new ArgumentException("Manifest path is required.", nameof(manifestPath));
            var translator = new LegacyActionTranslator(sources);
            var encounters = translator.ConvertAll(report);
            if (encounters == null) return null;
            var plan = new Plan(encounters, translator.Facts, root, manifestPath);
            var starts = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var encounter in encounters)
            {
                if (encounter.Nodes.Count == 0) throw new FormatException("Encounter has no start page: " + encounter.Source.Id);
                starts.Add(encounter.Nodes[0].Title, encounter.BeatId);
                var placement = LegacyBeatWriter.GetPlacement(encounter.Source.Id);
                if (placement.BeatId != encounter.BeatId) throw new FormatException("Placement identity mismatch: " + encounter.Source.Id);
                foreach (string where in placement.Where)
                    if (where.StartsWith("starsys_", StringComparison.Ordinal) || placement.Trigger == BeatTrigger.Hail)
                    {
                        var definition = resolve(where);
                        bool valid = placement.Trigger == BeatTrigger.Hail ? definition is EncounterDefinition : definition is AuthoredStarSystemDefinition;
                        if (!valid) throw new FormatException("Missing typed beat location: " + where);
                        plan.References[where] = definition;
                    }
            }
            foreach (var encounter in encounters)
                foreach (var node in encounter.Nodes)
                    foreach (var option in node.Options)
                        foreach (string statement in option.Statements)
                            if (statement.StartsWith("<<detour ", StringComparison.Ordinal))
                            {
                                string target = statement.Substring(9).TrimEnd('>');
                                if (starts.TryGetValue(target, out string beat) && beat != encounter.BeatId) plan.Nested.Add(beat);
                            }
            foreach (var encounter in encounters)
            {
                string path = DialoguePath(root, encounter.BeatId);
                var emission = YarnEmitter.Emit(encounter, plan.Nested.Contains(encounter.BeatId));
                plan.TextFiles.Add(path, emission.Text);
                plan.Manifest.AddDialogue(path, emission);
            }
            plan.TextFiles.Add(manifestPath, plan.Manifest.Render());
            return plan;
        }

        internal static string DialoguePath(string root, string beatId)
            => root + "/Dialog/Legacy/" + (beatId.StartsWith("beat_debug_", StringComparison.Ordinal) ? "_Debug/" : "") + beatId.Substring(5) + ".yarn";

        internal static void Write(Plan plan, bool force, ValidationReport report)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            var paths = new List<string>(plan.TextFiles.Keys) { plan.Root + "/Facts/facts_legacy.asset" };
            foreach (var encounter in plan.Encounters) paths.Add(plan.Root + "/Beats/" + encounter.BeatId + ".asset");
            foreach (string path in paths)
            {
                if (Directory.Exists(path)) throw new IOException("Output path is a directory: " + path);
                if (!force && (File.Exists(path) || AssetDatabase.LoadMainAssetAtPath(path) != null))
                    throw new IOException("Output already exists; use --force to replace it: " + path);
            }
            // Type conflicts must also fail before a first output is written.
            CheckType<FactCatalogDefinition>(plan.Root + "/Facts/facts_legacy.asset");
            foreach (var encounter in plan.Encounters) CheckType<StoryBeatDefinition>(plan.Root + "/Beats/" + encounter.BeatId + ".asset");
            var writer = new LegacyAssetWriter(plan.Root, new MigrationManifest(), force, report);
            foreach (var encounter in plan.Encounters)
                LegacyBeatWriter.Write(writer, encounter, plan.Nested.Contains(encounter.BeatId), id => plan.References[id]);
            LegacyBeatWriter.WriteFacts(writer, plan.Facts);
            if (report.HasErrors) throw new InvalidDataException(report.ToText());
            foreach (var pair in plan.TextFiles)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(pair.Key)));
                if (!File.Exists(pair.Key) || File.ReadAllText(pair.Key) != pair.Value)
                    File.WriteAllText(pair.Key, pair.Value, new UTF8Encoding(false));
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        static void CheckType<T>(string path) where T : ContentDefinition
        {
            if (!File.Exists(path)) return;
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (!(asset is T)) throw new InvalidDataException("Existing output is not " + typeof(T).Name + ": " + path);
        }
        internal static ContentDefinition Resolve(string id)
        {
            ContentDefinition found = null;
            foreach (string guid in AssetDatabase.FindAssets("t:ContentDefinition"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<ContentDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null || asset.IdString != id) continue;
                if (found != null) throw new InvalidDataException("Duplicate reference ID: " + id);
                found = asset;
            }
            return found;
        }
    }
}
