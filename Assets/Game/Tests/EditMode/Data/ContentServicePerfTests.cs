using System;
using System.Collections.Generic;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Tests.Common.Perf;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using Unity.PerformanceTesting;
using UnityEditor;

namespace Game.Tests.EditMode.Data
{
    /// <summary>DATA-007 gates (SPEC-04 §8).</summary>
    public sealed class ContentServicePerfTests
    {
        [TearDown] public void TearDown() => TestContentFactory.DestroyAll();

        static double[] ConstructMs(IReadOnlyList<ContentDefinition> defs, int runs)
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
                return t;
            }
            finally { UnityEngine.Debug.unityLogger.logEnabled = true; }
        }

        [Test, Performance, Category("Performance")]
        public void Construct_ShippedContent()
        {
            // No ContentDatabase ships before DATA-009: until it exists this measures a 150-definition synthetic set of the expected size.
            var db = AssetDatabase.LoadAssetAtPath<Game.Data.ContentDatabase>("Assets/Game/Content/ContentDatabase.asset");
            IReadOnlyList<ContentDefinition> defs = db != null ? db.Definitions : SyntheticContent.Make(150, 40, 7);
            double ms = PerfRun.Gate("Construct_Shipped", ConstructMs(defs, 11), 20.0).Median;
            UnityEngine.Debug.Log($"ContentService ({(db != null ? "shipped" : "synthetic 150")}): median {ms:F2} ms");
        }

        [Test, Performance, Category("Performance")]
        public void Construct_Synthetic2000()
        {
            var defs = SyntheticContent.Make(2000, 500, 12345);
            double ms = PerfRun.Gate("Construct_Synthetic2000", ConstructMs(defs, 7), 100.0).Median;
            using var s = ContentService.CreateForTests(defs);
            long bytes = 0;
            ref var blob = ref s.BlockDefs.Value;
            for (int k = 0; k < blob.Blocks.Length; k++)
                bytes += UnsafeUtility.SizeOf<BlockDef>() + 12L * (blob.Blocks[k].Cells.Length + blob.Blocks[k].Clearance.Length) + blob.Blocks[k].Shapes.Length + 2L * blob.Blocks[k].AbilityIndices.Length;
            UnityEngine.Debug.Log($"ContentService synthetic 2000 / 500 blocks: median {ms:F2} ms, BlockDefsBlob ≈ {bytes / 1024.0:F1} KB");
            Assert.AreEqual(500, s.Catalog.BlockCount);
            if (bytes > 256 * 1024) Game.Tests.Common.Perf.PerfRun.WarnBudget($"Content construction allocations {bytes} B; target <= 262144 B");
        }
    }
}
