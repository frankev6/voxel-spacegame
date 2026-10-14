// Lead-only, stopped serialized Editor:
// cp tools/jobs/QST-009-import-opening.cs Assets/Game/Editor/OpeningContentJob.cs
// tools/compile.sh
// tools/playtest/g.sh eval --code 'Game.Editor.OpeningContentJob.Run(); return "QST-009 imported";'
// Remove Assets/Game/Editor/OpeningContentJob.cs and its meta after execution.
// Uses the actual DialogMenu/compiler pipeline; acceptance tests only read the saved outputs.
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Game.Core;
using Game.Data;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Editor.Content;
using Game.Editor.Narrative;
using Game.Editor.Narrative.Dialog;
using UnityEditor;

namespace Game.Editor
{
    public static class OpeningContentJob
    {
        const string Evidence = "Logs/playtest/qst009-content";
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Opening import requires Edit mode.");
            Directory.CreateDirectory(Evidence);
            var report = new ValidationReport("QST-009 opening import");
            ContentDatabase database = null;
            string failure = "";
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                database = ContentDatabaseBuilder.Rebuild();
                var identities = database.Definitions.Where(value => value != null).Select(AssetDatabase.GetAssetPath).Distinct()
                    .ToDictionary(path => path, path => AssetDatabase.AssetPathToGUID(path), StringComparer.Ordinal);
                Compile(database, report);
                if (report.HasErrors) throw new ContentValidationException(report);
                // This canonical entry compiles both the command table and the real graph program.
                DialogMenu.RebuildDialogTables();
                database = ContentDatabaseBuilder.Rebuild();
                Compile(database, report); // Include generated program condition/expression wrappers.
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                database = ContentDatabaseBuilder.Rebuild();
                var catalog = ContentCatalog.Build(database.Definitions, report);
                var context = DialogCommandTableGenerator.Context(catalog, report);
                var sources = DialogCommandTableGenerator.ReadSources(DialogCommandTableGenerator.Root);
                var plan = DialogCommandTableGenerator.Prepare(context, sources, DialogCommandTableGenerator.Root);
                var table = catalog.Get<DialogCommandTableDefinition>(new ContentId("dialog_command_table"));
                var program = catalog.Get<DialogProgramDefinition>(new ContentId("dialog_program"));
                if (table.SourcesHash != plan.Hash || program.SourcesHash != plan.Hash)
                    throw new InvalidOperationException("Stored command table/program must share the exact current source-and-fact hash.");
                var titles = DialogCommandTableGenerator.Parse(sources).SelectMany(script => script.Nodes).Select(node => node.Title)
                    .OrderBy(title => title, StringComparer.Ordinal).ToArray();
                if (!titles.SequenceEqual(program.Nodes.Select(node => node.Title)))
                    throw new InvalidOperationException("Stored real dialog program does not contain the exact current source nodes.");
                foreach (var pair in identities)
                    if (AssetDatabase.AssetPathToGUID(pair.Key) != pair.Value)
                        throw new InvalidOperationException("Import changed an existing content identity: " + pair.Key);
                report.Merge(ContentValidatorRunner.Validate(database));
                if (report.HasErrors) throw new ContentValidationException(report);
                Console.WriteLine("QST-009 real compilation/program generation/stored validation: " + report.ToText());
            }
            catch (ContentValidationException error)
            {
                if (!ReferenceEquals(error.Report, report)) report.Merge(error.Report);
                failure = error.ToString(); throw;
            }
            catch (Exception error) { failure = error.ToString(); throw; }
            finally
            {
                File.WriteAllText(Evidence + "/validation.txt", report.ToText(int.MaxValue));
                File.WriteAllText(Evidence + "/failure.txt", failure);
                if (database != null) Readback(database);
            }
        }
        static void Compile(ContentDatabase database, ValidationReport report)
        {
            var compiled = LogicCompilePostprocessor.RecompileAll(database);
            if (compiled == null) throw new InvalidOperationException("Canonical logic compilation did not run.");
            report.Merge(compiled);
        }
        static void Readback(ContentDatabase database)
        {
            var text = new StringBuilder();
            foreach (var definition in database.Definitions.Where(value => value != null &&
                (value.IdString == "facts_opening" || value.IdString == "beat_drift_survey_cache" || value.IdString == "beat_gate_awakens" ||
                 value.IdString == "narrative_config" || value.IdString == "dialog_command_table" || value.IdString == "dialog_program"))
                .OrderBy(value => value.IdString, StringComparer.Ordinal))
            {
                string path = AssetDatabase.GetAssetPath(definition);
                using var sha = SHA256.Create();
                text.AppendLine(definition.IdString + " " + path + " GUID=" + AssetDatabase.AssetPathToGUID(path) +
                    " SHA256=" + BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant());
                if (definition is DialogCommandTableDefinition table) text.AppendLine("  sourcesHash=" + table.SourcesHash + " commands=" + table.Entries.Count);
                if (definition is DialogProgramDefinition program) text.AppendLine("  sourcesHash=" + program.SourcesHash + " nodes=" + program.Nodes.Count + " instructions=" + program.Code.Count);
                using var serialized = new SerializedObject(definition);
                var property = serialized.GetIterator(); bool children = true;
                while (property.Next(children))
                {
                    children = true;
                    if (property.isArray) continue; // Visit elements, not the collection container.
                if (property.type != nameof(CompiledCondition) && property.type != nameof(CompiledIntExpr) && property.type != nameof(CompiledEffectList)) continue;
                    text.AppendLine("  " + property.propertyPath + " sourceHash=" + property.FindPropertyRelative("sourceHash").ulongValue +
                        " version=" + property.FindPropertyRelative("compilerVersion").intValue + " stale=" + LogicCompilePostprocessor.IsStale(property) +
                        " errors=" + property.FindPropertyRelative("hasErrors").boolValue);
                    children = false;
                }
            }
            File.WriteAllText(Evidence + "/readback.txt", text.ToString());
        }
    }
}
