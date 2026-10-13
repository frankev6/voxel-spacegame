using System;
using System.IO;
using Game.Data;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Editor.Content;
using UnityEditor;

namespace Game.Editor.Narrative
{
    /// <summary>SPEC-40 §5.7 narrative-only batch entry; writes the report even when the pass fails.</summary>
    public static class NarrativeValidatorCli
    {
        public static void Run() => EditorApplication.Exit(RunAndGetExitCode());
        internal static int RunAndGetExitCode() => RunAndGetExitCode(ContentDatabaseBuilder.Rebuild());
        internal static int RunAndGetExitCode(ContentDatabase database, string reportPath = "Temp/narrative-validation.txt")
        {
            var report = new ValidationReport("Narrative validation");
            try
            {
                var content = ContentCatalog.Build(database?.Definitions ?? Array.Empty<Game.Data.Definitions.ContentDefinition>(), report);
                new NarrativeValidator().Validate(content, report);
            }
            catch (Exception exception) { report.Error("DATA-E099", "NarrativeValidator", "", "Narrative validation failed: " + exception.GetType().Name + ": " + exception.Message); }
            string text = report.ToText(int.MaxValue);
            string directory = Path.GetDirectoryName(reportPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(reportPath, text); Console.WriteLine(text);
            return report.HasErrors ? 1 : 0;
        }
    }
}
