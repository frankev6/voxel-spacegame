// Lead-only: copy to Assets/Game/Editor/Migration/Narrative/LegacyQuestGenerationJob.cs, invoke
// Game.Editor.Migration.Narrative.LegacyQuestGenerationJob.Run in the serialized stopped Editor,
// then remove temporary source/meta. Retains the existing dialogue conversion and rebuilds the real catalog.
using System;
using System.IO;
using Game.Data.Validation;
using Game.Editor.Content;
using UnityEditor;

namespace Game.Editor.Migration.Narrative
{
    public static class LegacyQuestGenerationJob
    {
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Generation requires a stopped Editor.");
            var report = new ValidationReport("NAR-016 generation");
            var sources = LegacyNarrativeLoader.Load("Import/Legacy", report);
            if (sources == null || report.HasErrors) throw new InvalidDataException(report.ToText());
            var plan = LegacyNarrativeConverter.Prepare(sources, LegacyNarrativeConverter.DefaultRoot,
                LegacyNarrativeConverter.DefaultManifest, LegacyNarrativeConverter.Resolve, report, includeQuests: true);
            if (plan == null || report.HasErrors) throw new InvalidDataException(report.ToText());
            LegacyNarrativeConverter.Write(plan, true, report);
            ContentDatabaseBuilder.Rebuild();
            Directory.CreateDirectory("Logs/migration"); File.WriteAllText("Logs/migration/nar016.txt", report.ToText(int.MaxValue));
            if (report.HasErrors) throw new InvalidDataException(report.ToText());
            Console.WriteLine(report.ToText(int.MaxValue));
        }
    }
}
