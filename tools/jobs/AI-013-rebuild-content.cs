// AI-013 lead-only catalog recipe; outside Assets, never compiled into the runtime project.
// After integrating AI-013, copy to Assets/Game/Editor/FormationCatalogJob.cs.
// Run Game.Editor.Content.FormationCatalogJob.Run in the lead's serialized disposable batch Editor;
// remove the temporary source/meta and retain the generated ContentDatabase.asset update.
// No boot changes: formations are part of the existing boot-owned AiConfig.Profiles allocation (ADR-0065).
namespace Game.Editor.Content
{
    public static class FormationCatalogJob
    {
        public static void Run()
        {
            int exitCode = 1;
            try
            {
                var database = ContentDatabaseBuilder.Rebuild();
                var report = new Game.Data.Validation.ValidationReport("AI-013 catalog");
                var catalog = Game.Data.Services.ContentCatalog.Build(database.Definitions, report);
                foreach (string id in new[] { "formation_line", "formation_wedge", "formation_column" })
                {
                    if (!catalog.TryGet<Game.Data.Definitions.FormationDefinition>(new Game.Core.ContentId(id), out var formation))
                        throw new System.InvalidOperationException("Formation did not import with its canonical type/ID: " + id);
                    formation.Validate(report.For(UnityEditor.AssetDatabase.GetAssetPath(formation)), null);
                    if (formation.Slots.Count != 10 || catalog.IndexOf(formation) == 0)
                        throw new System.InvalidOperationException("Formation slots/catalog index are incorrect: " + id);
                }
                if (report.HasErrors) throw new Game.Data.Validation.ContentValidationException(report);
                UnityEditor.AssetDatabase.SaveAssets();
                exitCode = 0;
            }
            catch (System.Exception exception) { Game.Core.GameLog.Exception("AI", exception, "AI-013 catalog regeneration failed"); }
            UnityEditor.EditorApplication.Exit(exitCode);
        }
    }
}
