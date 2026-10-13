using System.Collections;
using System.IO;
using Game.Data.Definitions;
using Game.Data.Logic.Nodes;
using Game.Editor.Content;
using Game.Editor.Narrative;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode.Editor.Narrative
{
    /// <summary>SPEC-40 NAR-009: real temporary imports, one-update compilation and unchanged-write suppression.</summary>
    public sealed class LogicCompilePostprocessorTests
    {
        const string Parent = "Assets/Game/Content/_Test";
        const string Root = Parent + "/NAR009";
        const string Path = Root + "/nartest_source.asset";
        bool ownsParent, ownsRoot;
        LogicCompileFixtureDefinition asset;
        [SetUp] public void SetUp()
        {
            Assert.IsFalse(AssetDatabase.IsValidFolder(Root), "Preserve unrelated temporary/user assets.");
            if (!AssetDatabase.IsValidFolder(Parent)) { AssetDatabase.CreateFolder(ContentDatabaseBuilder.ContentRoot, "_Test"); ownsParent = true; }
            AssetDatabase.CreateFolder(Parent, "NAR009"); ownsRoot = true;
            asset = ScriptableObject.CreateInstance<LogicCompileFixtureDefinition>();
            using (var so = new SerializedObject(asset))
            {
                so.FindProperty("id").stringValue = "nartest_source";
                so.FindProperty("condition.source").stringValue = "true";
                so.FindProperty("progress.source").stringValue = "1 + 2";
                var source = so.FindProperty("effects.source"); source.arraySize = 1; source.GetArrayElementAtIndex(0).stringValue = "give_credits 1";
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            AssetDatabase.CreateAsset(asset, Path);
            AssetDatabase.ImportAsset(Path, ImportAssetOptions.ForceUpdate);
            var report = LogicCompilePostprocessor.FlushPending();
            Assert.IsFalse(asset.Condition.IsStale, report?.ToText());
        }
        [TearDown] public void TearDown()
        {
            if (ownsRoot) AssetDatabase.DeleteAsset(Root);
            if (ownsParent && AssetDatabase.IsValidFolder(Parent) && Directory.GetFileSystemEntries(Parent).Length == 0)
                AssetDatabase.DeleteAsset(Parent);
            ownsRoot = ownsParent = false;
            ContentDatabaseBuilder.Rebuild();
            LogicCompilePostprocessor.FlushPending();
        }
        [UnityTest] public IEnumerator EditedSource_Recompiles()
        {
            using (var so = new SerializedObject(asset))
            { so.FindProperty("condition.source").stringValue = "false"; so.ApplyModifiedPropertiesWithoutUndo(); }
            AssetDatabase.SaveAssetIfDirty(asset);
            AssetDatabase.ImportAsset(Path, ImportAssetOptions.ForceUpdate);
            yield return null; // exactly one Editor update; no readiness/wall-clock loop
            var loaded = AssetDatabase.LoadAssetAtPath<LogicCompileFixtureDefinition>(Path);
            Assert.IsFalse(loaded.Condition.IsStale); Assert.IsFalse(loaded.Condition.HasErrors);
            using var saved = new SerializedObject(loaded);
            Assert.IsInstanceOf<BoolLiteral>(saved.FindProperty("condition.root").managedReferenceValue);
            Assert.IsFalse(saved.FindProperty("condition.root.value").boolValue, "Graph changed to the edited false literal.");
            Assert.IsFalse(EditorUtility.IsDirty(loaded), "Compilation saved the updated stamp and graph.");
        }
        [UnityTest] public IEnumerator UnchangedSource_NotDirtied()
        {
            byte[] before = File.ReadAllBytes(Path); string guid = AssetDatabase.AssetPathToGUID(Path);
            Assert.IsFalse(EditorUtility.IsDirty(asset));
            AssetDatabase.ImportAsset(Path, ImportAssetOptions.ForceUpdate);
            yield return null;
            var loaded = AssetDatabase.LoadAssetAtPath<LogicCompileFixtureDefinition>(Path);
            Assert.IsFalse(EditorUtility.IsDirty(loaded)); Assert.IsFalse(loaded.Condition.IsStale);
            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(Path)); CollectionAssert.AreEqual(before, File.ReadAllBytes(Path));
        }
        [Test] public void RecompileAll_ClearsStale()
        {
            using (var so = new SerializedObject(asset))
            {
                so.FindProperty("condition.compilerVersion").intValue = -1;
                so.FindProperty("progress.source").stringValue = "4 + 5";
                so.FindProperty("effects.source").GetArrayElementAtIndex(0).stringValue = "give_credits 2";
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            var encounter = ScriptableObject.CreateInstance<EncounterDefinition>();
            using (var so = new SerializedObject(encounter))
            { so.FindProperty("id").stringValue = "encounter_compile_test"; so.FindProperty("condition.source").stringValue = "true"; so.ApplyModifiedPropertiesWithoutUndo(); }
            AssetDatabase.CreateAsset(encounter, Root + "/encounter_compile_test.asset");
            var report = LogicCompilePostprocessor.RecompileAll(ContentDatabaseBuilder.Rebuild(), Root);
            Assert.AreEqual(0, report.ErrorCount, report.ToText());
            Assert.IsFalse(asset.Condition.IsStale); Assert.IsFalse(asset.Progress.IsStale); Assert.IsFalse(asset.Effects.IsStale);
            Assert.IsFalse(encounter.Condition.IsStale); Assert.IsFalse(asset.Effects.IsEmpty);
            using var compiled = new SerializedObject(asset);
            var effect = compiled.FindProperty("effects.effects").GetArrayElementAtIndex(0);
            Assert.IsInstanceOf<GiveCreditsEffect>(effect.managedReferenceValue);
            Assert.AreEqual(2, effect.FindPropertyRelative("arguments.Integers").GetArrayElementAtIndex(0).FindPropertyRelative("value").intValue);
            Assert.IsFalse(EditorUtility.IsDirty(asset)); Assert.IsFalse(EditorUtility.IsDirty(encounter));
        }
    }
}
