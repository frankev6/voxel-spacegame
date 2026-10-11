using System;
using System.Collections.Generic;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Services;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using Unity.PerformanceTesting;
using UnityEditor;

namespace Game.Tests.EditMode.Data
{
    /// <summary>DATA-007 gates (SPEC-04 §8). TST-001 moves these onto PerfRun.</summary>
    public sealed class ContentServicePerfTests
    {
        [TearDown] public void TearDown() => TestContentFactory.DestroyAll();

        static double MedianConstructMs(IReadOnlyList<ContentDefinition> defs, int runs)
        {
            UnityEngine.Debug.unityLogger.logEnabled = false;   // the boot summary line per construction would dominate the timing
            try
            {
                for (int i = 0; i < 3; i++) ContentService.CreateForTests(defs).Dispose();
                var t = new double[runs];
                for (int i = 0; i < runs; i++)
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var s = ContentService.CreateForTests(defs);
                    t[i] = sw.Elapsed.TotalMilliseconds;
                    s.Dispose();
                }
                Array.Sort(t);
                return t[runs / 2];
            }
            finally { UnityEngine.Debug.unityLogger.logEnabled = true; }
        }

        [Test, Performance, Category("Performance")]
        public void Construct_ShippedContent()
        {
            // No ContentDatabase ships before DATA-009: until it exists this measures a 150-definition synthetic set of the expected size.
            var db = AssetDatabase.LoadAssetAtPath<Game.Data.ContentDatabase>("Assets/Game/Content/ContentDatabase.asset");
            IReadOnlyList<ContentDefinition> defs = db != null ? db.Definitions : SyntheticContent.Make(150, 40, 7);
            double ms = MedianConstructMs(defs, 11);
            Measure.Custom(new SampleGroup("Construct_Shipped", SampleUnit.Millisecond), ms);
            UnityEngine.Debug.Log($"ContentService ({(db != null ? "shipped" : "synthetic 150")}): median {ms:F2} ms");
            Assert.LessOrEqual(ms, 20.0);
        }

        [Test, Performance, Category("Performance")]
        public void Construct_Synthetic2000()
        {
            var defs = SyntheticContent.Make(2000, 500, 12345);
            double ms = MedianConstructMs(defs, 7);
            Measure.Custom(new SampleGroup("Construct_Synthetic2000", SampleUnit.Millisecond), ms);
            using var s = ContentService.CreateForTests(defs);
            long bytes = 0;
            ref var blob = ref s.BlockDefs.Value;
            for (int k = 0; k < blob.Blocks.Length; k++)
                bytes += UnsafeUtility.SizeOf<BlockDef>() + 12L * (blob.Blocks[k].Cells.Length + blob.Blocks[k].Clearance.Length) + 2L * blob.Blocks[k].AbilityIndices.Length;
            UnityEngine.Debug.Log($"ContentService synthetic 2000 / 500 blocks: median {ms:F2} ms, BlockDefsBlob ≈ {bytes / 1024.0:F1} KB");
            Assert.AreEqual(500, s.Catalog.BlockCount);
            Assert.LessOrEqual(ms, 100.0);
            Assert.LessOrEqual(bytes, 256 * 1024);
        }
    }
}
