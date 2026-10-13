// Lead-only: copy temporarily to Assets/Game/Editor/StreamingConfigSetup.cs, run Game.Editor.StreamingConfigSetup.Run,
// then remove the temporary source/meta. Uses the one-off Editor-job workflow in docs/08-tools.md §2.
using System;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Editor.Content;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class StreamingConfigSetup
    {
        public static void Run()
        {
            try
            {
                const string path = "Assets/Game/Content/Config/config_streaming.asset";
                var existing = AssetDatabase.LoadMainAssetAtPath(path);
                var definition = existing as StreamingConfigDefinition;
                if (existing != null && definition == null) throw new InvalidOperationException("Streaming config path has another asset type.");
                if (definition == null)
                {
                    if (System.IO.File.Exists(path)) throw new InvalidOperationException("Streaming config path exists but could not be loaded.");
                    definition = ScriptableObject.CreateInstance<StreamingConfigDefinition>();
                    definition.name = "config_streaming";
                    using (var so = new SerializedObject(definition))
                    {
                        so.FindProperty("id").stringValue = "config_streaming";
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                    AssetDatabase.CreateAsset(definition, path);
                }
                // Existing authored values and GUIDs are preserved.
                if (definition.IdString != "config_streaming") throw new InvalidOperationException("Streaming config has a noncanonical ID.");
                AssetDatabase.SaveAssets();
                var database = ContentDatabaseBuilder.Rebuild();
                var report = new ValidationReport("SYS-002");
                var catalog = ContentCatalog.Build(database.Definitions, report);
                definition.Validate(report.For(path), new ContentValidationContext(path, true, _ => true, catalog));
                Debug.Log(report.ToText());
                if (report.HasErrors) throw new InvalidOperationException(report.ToText());
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
