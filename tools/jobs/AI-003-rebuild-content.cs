// AI-003 lead-only catalog recipe; this file is outside Assets and is not compiled into the project.
// Integrate the generated MIG-010 abilities (8f264b3) first, then copy to Assets/Game/Editor/AiContentCatalogJob.cs.
// Execute Game.Editor.Content.AiContentCatalogJob.Run in a disposable batch Editor; remove temporary source/meta afterward.
// Commit the builder's ContentDatabase.asset update with AI-003 closeout.
//
// Exact SPEC-01 lifetime hookup for the lead (no BootSingletons edits in this worker delivery):
// Add using Game.Simulation.Ai; to BootSingletons and these fields to its existing boot lifetime:
//   internal Entity AiEntity;
//   internal BlobAssetReference<AiProfilesBlob> AiProfiles;
//   AiContent.CreateSingletons(em, catalog, created); // boot table's AI row; the existing loop adds BootSingleton to created entities
//   using (var ai = em.CreateEntityQuery(ComponentType.ReadOnly<AiConfig>()))
//   {
//       lifetime.AiEntity = ai.GetSingletonEntity();
//       lifetime.AiProfiles = ai.GetSingleton<AiConfig>().Profiles;
//   }
// At disposal, in the existing world.IsCreated block after world.EntityManager.CompleteAllTrackedJobs():
//   if (AiEntity != Entity.Null && world.EntityManager.Exists(AiEntity)) world.EntityManager.DestroyEntity(AiEntity);
// Then, outside that block: if (AiProfiles.IsCreated) AiProfiles.Dispose();
// Release exactly once, including boot failure; do not move this ownership into a scene or system.
namespace Game.Editor.Content
{
    public static class AiContentCatalogJob
    {
        public static void Run()
        {
            var database = ContentDatabaseBuilder.Rebuild();
            var report = new Game.Data.Validation.ValidationReport("AI-003 catalog job");
            var catalog = Game.Data.Services.ContentCatalog.Build(database.Definitions, report);
            if (catalog.All<Game.Data.Definitions.AiConfigDefinition>().Count != 1)
                throw new System.InvalidOperationException("Exactly one AI config is required.");
            foreach (string id in new[] { "ability_shield_toggle", "ability_speed_boost" })
                if (!catalog.TryGet<Game.Data.Definitions.AbilityDefinition>(new Game.Core.ContentId(id), out _))
                    throw new System.InvalidOperationException("MIG-010 prerequisite missing: " + id);
            if (!catalog.TryGet<Game.Data.Definitions.AiConfigDefinition>(new Game.Core.ContentId("config_ai"), out var config))
                throw new System.InvalidOperationException("config_ai did not import with its canonical type/ID.");
            config.Validate(report.For(UnityEditor.AssetDatabase.GetAssetPath(config)),
                new Game.Data.Validation.ContentValidationContext("AI-003", true, d => System.Linq.Enumerable.Contains(database.Definitions, d), catalog));
            foreach (string id in new[] { "ai_default", "ai_turret", "ai_skittish", "ai_civilian" })
            {
                if (!catalog.TryGet<Game.Data.Definitions.AiProfileDefinition>(new Game.Core.ContentId(id), out var profile))
                    throw new System.InvalidOperationException("AI profile did not import: " + id);
                profile.Validate(report.For(UnityEditor.AssetDatabase.GetAssetPath(profile)), null);
                int expected = id == "ai_default" ? 2 : 1;
                if (profile.AbilityRules.Count != expected) throw new System.InvalidOperationException("Incorrect authored rule count: " + id);
                for (int i = 0; i < expected; i++)
                {
                    bool shield = id == "ai_turret" || (id == "ai_default" && i == 0);
                    var rule = profile.AbilityRules[i];
                    if (rule.ability == null || rule.ability.IdString != (shield ? "ability_shield_toggle" : "ability_speed_boost") ||
                        rule.trigger != (shield ? Game.Data.Definitions.AiAbilityTrigger.ShieldOff : Game.Data.Definitions.AiAbilityTrigger.Fleeing))
                        throw new System.InvalidOperationException("Incorrect canonical ability rule: " + id);
                }
            }
            if (report.HasErrors) throw new Game.Data.Validation.ContentValidationException(report);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.EditorApplication.Exit(0);
        }
    }
}
