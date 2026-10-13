// PWR-003 lead-only temporary Unity job recipe. This file is outside Assets and is not compiled into the project.
// After integrating PWR-003, copy it to Assets/Game/Editor/PowerTuningCatalogJob.cs in the validation checkout.
// Invoke Game.Editor.Content.PowerTuningCatalogJob.Run through the normal Unity job workflow, then remove that temporary file/meta.
// Commit the resulting ContentDatabase.asset update with the central PWR-003 closeout; do not edit its YAML by hand.
namespace Game.Editor.Content
{
    public static class PowerTuningCatalogJob
    {
        public static void Run()
        {
            var definition = UnityEditor.AssetDatabase.LoadAssetAtPath<Game.Data.Definitions.PowerTuningDefinition>(
                "Assets/Game/Content/Tuning/tuning_power.asset");
            if (definition == null || definition.IdString != "tuning_power")
                throw new System.InvalidOperationException("PWR-003 tuning asset did not import with its canonical type and ID.");
            var database = ContentDatabaseBuilder.Rebuild();
            var report = new Game.Data.Validation.ValidationReport("PWR-003 catalog job");
            definition.Validate(report.For(UnityEditor.AssetDatabase.GetAssetPath(definition)), null);
            var catalog = Game.Data.Services.ContentCatalog.Build(database.Definitions, report);
            if (catalog.All<Game.Data.Definitions.PowerTuningDefinition>().Count != 1 || catalog.IndexOf(definition) == 0)
                throw new System.InvalidOperationException("Expected exactly one power tuning definition in the rebuilt database.");
            if (report.HasErrors) throw new Game.Data.Validation.ContentValidationException(report);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.EditorApplication.Exit(0);
        }
    }
}
