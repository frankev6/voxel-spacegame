using System;
using System.Diagnostics;
using System.Linq;
using Game.Data.Definitions;
using Game.Editor.Content;
using Game.Tests.Common.Perf;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode.Editor.Content
{
    /// <summary>DATA-014: imported membership matches the canonical full scan; paired real-catalog timings.</summary>
    public sealed class ContentImportPerformanceTests
    {
        static void Id(ContentDefinition definition, string value)
        {
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("id").stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void ImportedDefinitions_SubassetsAndIdChanges_MatchFullScan()
        {
            string path = ContentDatabaseBuilder.ContentRoot + "/_DATA014_" + Guid.NewGuid().ToString("N") + ".asset";
            var main = ScriptableObject.CreateInstance<ItemDefinition>();
            var child = ScriptableObject.CreateInstance<ItemDefinition>();
            try
            {
                ContentDatabaseBuilder.Rebuild();
                Id(main, "zz_data014_main"); Id(child, "aa_data014_child");
                AssetDatabase.CreateAsset(main, path);
                AssetDatabase.AddObjectToAsset(child, path);
                AssetDatabase.SaveAssetIfDirty(main);
                AssertParity(path);
                Id(main, "aa_data014_main"); Id(child, "zz_data014_child");
                AssetDatabase.SaveAssetIfDirty(main);
                AssertParity(path);
                UnityEngine.Object.DestroyImmediate(child, true);
                AssetDatabase.SaveAssetIfDirty(main);
                AssertParity(path);
                AssetDatabase.DeleteAsset(path);
                AssertParity(path);
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
                if (main != null && !AssetDatabase.Contains(main)) UnityEngine.Object.DestroyImmediate(main);
                if (child != null && !AssetDatabase.Contains(child)) UnityEngine.Object.DestroyImmediate(child);
                ContentDatabaseBuilder.Rebuild();
            }
        }

        static void AssertParity(string path)
        {
            var incremental = ContentDatabaseBuilder.RebuildImported(new[] { path }).Definitions.ToArray();
            var full = ContentDatabaseBuilder.Rebuild().Definitions.ToArray();
            CollectionAssert.AreEqual(full, incremental);
        }

        [Test, Category("Performance")]
        public void ExistingContentImport_ComparedWithFullScan()
        {
            var database = ContentDatabaseBuilder.Rebuild();
            var expected = database.Definitions.ToArray();
            Assert.That(expected.Length, Is.GreaterThan(0));
            string path = AssetDatabase.GetAssetPath(expected[expected.Length / 2]);
            var paths = new[] { path };
            var baseline = new double[300]; var optimized = new double[300];
            for (int i = -60; i < baseline.Length; i++)
            {
                // Alternate order to avoid assigning warm-cache bias to one implementation.
                double a, b;
                if ((i & 1) == 0) { a = Time(() => ContentDatabaseBuilder.Rebuild()); b = Time(() => ContentDatabaseBuilder.RebuildImported(paths)); }
                else { b = Time(() => ContentDatabaseBuilder.RebuildImported(paths)); a = Time(() => ContentDatabaseBuilder.Rebuild()); }
                if (i >= 0) { baseline[i] = a; optimized[i] = b; }
            }
            CollectionAssert.AreEqual(expected, database.Definitions);
            var run = new PerfRun("DATA014.ContentImport." + expected.Length)
                .AddSamples("FullScan", baseline).AddSamples("ImportedPath", optimized);
            run.Write();
            Assert.That(run.Get("FullScan").Median, Is.GreaterThan(0));
            Assert.That(run.Get("ImportedPath").Median, Is.GreaterThan(0));
            TestContext.WriteLine($"{expected.Length} definitions; full {run.Get("FullScan").Median:F4} ms / p95 {run.Get("FullScan").P95:F4}; imported {run.Get("ImportedPath").Median:F4} ms / p95 {run.Get("ImportedPath").P95:F4}; ratio {run.Get("FullScan").Median / run.Get("ImportedPath").Median:F2}x");
        }
        static double Time(Action action)
        {
            long start = Stopwatch.GetTimestamp(); action();
            return (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
        }
    }
}
