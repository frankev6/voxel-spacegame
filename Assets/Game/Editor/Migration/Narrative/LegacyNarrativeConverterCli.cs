using System;
using System.IO;
using Game.Data.Validation;
using UnityEditor;

namespace Game.Editor.Migration.Narrative
{
    public static class LegacyNarrativeConverterCli
    {
        public static void Run()
        {
            bool force = Array.IndexOf(Environment.GetCommandLineArgs(), "--force") >= 0;
            EditorApplication.Exit(RunAndGetExitCode(force));
        }
        [MenuItem("Game/Migration/Convert Legacy Narrative")]
        static void Menu()
        {
            bool existing = Directory.Exists(LegacyNarrativeConverter.DefaultRoot + "/Dialog/Legacy")
                || Directory.Exists(LegacyNarrativeConverter.DefaultRoot + "/Beats")
                || File.Exists(LegacyNarrativeConverter.DefaultRoot + "/Facts/facts_legacy.asset")
                || File.Exists(LegacyNarrativeConverter.DefaultManifest);
            if (existing && !EditorUtility.DisplayDialog("Convert legacy narrative", "Replace generated legacy narrative files?", "Replace", "Cancel")) return;
            RunAndGetExitCode(existing);
        }
        internal static int RunAndGetExitCode(bool force)
        {
            var report = new ValidationReport("Legacy narrative conversion");
            try
            {
                var sources = LegacyNarrativeLoader.Load("Import/Legacy", report);
                if (sources != null)
                {
                    var plan = LegacyNarrativeConverter.Prepare(sources, LegacyNarrativeConverter.DefaultRoot,
                        LegacyNarrativeConverter.DefaultManifest, LegacyNarrativeConverter.Resolve, report);
                    if (plan != null && !report.HasErrors)
                    {
                        LegacyNarrativeConverter.Write(plan, force, report);
                        // Batch exit cannot rely on the import postprocessor's deferred callback.
                        if (!report.HasErrors) Game.Editor.Content.ContentDatabaseBuilder.Rebuild();
                    }
                }
            }
            catch (Exception exception) { report.Error("MIG-E020", "LegacyNarrative", "", exception.GetType().Name + ": " + exception.Message); }
            Directory.CreateDirectory("Logs/migration");
            File.WriteAllText("Logs/migration/legacy-narrative.txt", report.ToText(int.MaxValue));
            Console.WriteLine(report.ToText(int.MaxValue));
            return report.HasErrors ? 1 : 0;
        }
    }
}
