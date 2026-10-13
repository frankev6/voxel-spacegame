// Lead-only one-off job. Copy to Assets/Game/Editor/ShieldRetuneJob.cs, run
// Game.Editor.Migration.ShieldRetuneJob.Run, then delete the temporary script/meta.
namespace Game.Editor.Migration
{
    public static class ShieldRetuneJob
    {
        public static void Run()
        {
            const string path = "Assets/Game/Content/Blocks/block_shield_small.asset";
            const string manifestPath = "Import/Migration/manifest.json";
            var source = UnityEditor.AssetDatabase.LoadAssetAtPath<Game.Data.Definitions.BlockDefinition>(path);
            if (source == null) throw new System.InvalidOperationException("Missing canonical small shield.");
            Game.Data.Blocks.ShieldEmitterModule desired = null;
            foreach (var module in Converters.LegacyBlockValues.All["block_shield_small"].MakeModules())
                if (module is Game.Data.Blocks.ShieldEmitterModule shield) desired = shield;
            if (desired == null) throw new System.InvalidOperationException("Missing canonical shield value row.");
            var report = new Game.Data.Validation.ValidationReport("PWR-008 shield retune");
            var manifest = MigrationManifest.Load(manifestPath);
            var writer = new LegacyAssetWriter(Game.Editor.Content.ContentDatabaseBuilder.ContentRoot, manifest, false, report);
            var result = writer.CreateOrUpdate<Game.Data.Definitions.BlockDefinition>("Blocks", "block_shield_small", so =>
            {
                // Preserve every other authored field, link and stable managed-reference ID.
                UnityEditor.EditorUtility.CopySerialized(source, so.targetObject); so.Update();
                var modules = so.FindProperty("modules"); bool changed = false;
                for (int i = 0; i < modules.arraySize; i++)
                {
                    var entry = modules.GetArrayElementAtIndex(i);
                    if (!(entry.managedReferenceValue is Game.Data.Blocks.ShieldEmitterModule)) continue;
                    entry.FindPropertyRelative("Params.Capacity").floatValue = desired.Params.Capacity;
                    entry.FindPropertyRelative("Params.RegenPerSecond").floatValue = desired.Params.RegenPerSecond;
                    entry.FindPropertyRelative("Params.RechargeDelaySeconds").floatValue = desired.Params.RechargeDelaySeconds;
                    entry.FindPropertyRelative("Params.PowerDraw").floatValue = desired.Params.PowerDraw;
                    changed = true;
                }
                if (!changed) throw new System.InvalidOperationException("Canonical shield has no emitter module.");
            }, out var outcome);
            UnityEditor.AssetDatabase.SaveAssets(); manifest.Save(manifestPath);
            if (result == null || !result.TryGetModule<Game.Data.Blocks.ShieldEmitterModule>(out var actual) ||
                !actual.Params.Equals(desired.Params))
                throw new System.InvalidOperationException("Shield retune not applied: " + outcome + ". " + report.ToText());
            Game.Core.GameLog.Info("Migration", "PWR-008 shield retune: " + outcome + "; protected fields and GUID retained.");
            UnityEditor.EditorApplication.Exit(0);
        }
    }
}
