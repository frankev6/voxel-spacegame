using Game.Data.Services;
using Game.Data.Validation;
using Game.Editor.Content;
using UnityEditor;

namespace Game.Editor.Narrative.Dialog
{
    public static class DialogMenu
    {
        [MenuItem("Game/Narrative/Rebuild Dialog Tables")]
        public static void RebuildDialogTables()
        {
            var report = new ValidationReport("Rebuild dialog tables");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                report.Error("DLG-E013", DialogCommandTableGenerator.Root, "", "Dialog generation requires Edit mode.");
                ContentValidatorRunner.Log(report); throw new ContentValidationException(report);
            }
            var database = ContentDatabaseBuilder.Rebuild();
            var catalog = ContentCatalog.Build(database.Definitions, report);
            if (!report.HasErrors) report.Merge(DialogCommandTableGenerator.Generate(catalog));
            ContentValidatorRunner.Log(report);
            if (report.HasErrors) throw new ContentValidationException(report);
            ContentDatabaseBuilder.Rebuild();
        }
        [MenuItem("Game/Narrative/Tag Dialog Lines")]
        public static void TagDialogLines()
        {
            var report = DialogLineTagger.Tag(); ContentValidatorRunner.Log(report);
            if (report.HasErrors) throw new ContentValidationException(report);
            RebuildDialogTables();
        }
    }
}
