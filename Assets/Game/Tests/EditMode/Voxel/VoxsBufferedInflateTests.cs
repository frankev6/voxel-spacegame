using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Game.Data;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Tests.Common.Perf;
using Game.Tests.EditMode.Data;
using Game.Voxel.Data;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.PerformanceTesting;
using UnityEditor;

namespace Game.Tests.EditMode.Voxel
{
    public sealed class VoxsBufferedInflateTests
    {
        [TearDown] public void Cleanup() => TestContentFactory.DestroyAll();
        static void Compare(byte[] bytes, ContentCatalog catalog)
        {
            bool oldOk = VoxsCodec.TryDecodeLegacyInput(bytes, catalog, Allocator.TempJob, out var old, out var oldError);
            using (old)
            {
                bool newOk = VoxsCodec.TryDecode(bytes, catalog, Allocator.TempJob, out var current, out var error);
                using (current)
                {
                    Assert.AreEqual(oldOk, newOk, "acceptance differs for " + BitConverter.ToString(bytes.Take(32).ToArray()));
                    if (!oldOk) return; // Inflater errors can differ in text; rejection must not differ.
                    Assert.AreEqual(old.CellSize, current.CellSize); Assert.AreEqual(old.AnchorInstance, current.AnchorInstance);
                    Assert.AreEqual(old.Partial, current.Partial); Assert.AreEqual(old.DriftWarningCount, current.DriftWarningCount);
                    Equal(old.Cells, current.Cells); Equal(old.Palette, current.Palette); Equal(old.Instances, current.Instances);
                    Equal(old.BrickCoords, current.BrickCoords); Equal(old.RemovedBricks, current.RemovedBricks);
                }
            }
        }
        static void Equal<T>(NativeList<T> a, NativeList<T> b) where T : unmanaged
        {
            Assert.AreEqual(a.Length, b.Length);
            var comparer = EqualityComparer<T>.Default;
            for (int i = 0; i < a.Length; i++) if (!comparer.Equals(a[i], b[i])) Assert.Fail("Decoded " + typeof(T).Name + " differs at " + i);
        }
        [Test]
        public void BufferedInput_MatchesLegacyForTruncationTrailingAndMutationCorpus()
        {
            var catalog = VoxsCodecTests.Catalog();
            using var source = new VoxsTestStructures(1);
            source.Source.Palette[1] = new PaletteEntry { Block = VoxsTestStructures.Block(catalog, "block_hull") };
            for (int i = 0; i < 64; i++) source.Source.Cells[i] = Cell.Palette(1);
            byte[] valid = VoxsCodec.Encode(source.Source, VoxsBlockNames.FromCatalog(catalog), VoxsEncodeOptions.ForAsset);
            Compare(valid, catalog);
            for (int n = 0; n < valid.Length; n++) Compare(valid.Take(n).ToArray(), catalog);
            foreach (int count in new[] { 1, 2, 7, 8191, 8192, 8193 })
            {
                var trailing = new byte[valid.Length + count]; valid.CopyTo(trailing, 0); Compare(trailing, catalog);
                Array.Fill(trailing, (byte)255, valid.Length, count); Compare(trailing, catalog);
            }
            var rng = new Unity.Mathematics.Random(0x153ab49u);
            for (int n = 0; n < 512; n++)
            {
                var mutated = (byte[])valid.Clone();
                int at = rng.NextInt(8, mutated.Length); mutated[at] ^= (byte)(1 << rng.NextInt(8));
                Compare(mutated, catalog);
            }
            // Concatenated complete streams are trailing input, not a second payload.
            Compare(valid.Concat(valid.Skip(8)).ToArray(), catalog);
        }
        static (ContentCatalog catalog, ShipBlueprintDefinition[] ships) Hollow()
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabase>("Assets/Game/Content/ContentDatabase.asset");
            Assert.IsNotNull(database);
            var report = new ValidationReport("Buffered inflate fixture");
            var catalog = ContentCatalog.Build(database.Definitions, report); Assert.IsFalse(report.HasErrors, report.ToText());
            var ships = catalog.All<ShipBlueprintDefinition>().Where(s => s.IdString.StartsWith("ship_hollow_", StringComparison.Ordinal))
                .OrderBy(s => s.IdString, StringComparer.Ordinal).ToArray();
            Assert.Greater(ships.Length, 0); return (catalog, ships);
        }
        [Test]
        public void ShippedHollow_AllDecodedFieldsMatchLegacy()
        {
            var (catalog, ships) = Hollow();
            foreach (var ship in ships) Compare(ship.Payload.ToArray(), catalog);
        }
        [Test, Performance, Category("Performance")]
        public void HollowLargest_FullStructureDecodePaired()
        {
            using var settings = new ReferenceSettings(); PerfSettings.AssertReference();
            var (catalog, ships) = Hollow();
            var ship = ships.OrderByDescending(s => s.Payload.Length).First(); byte[] bytes = ship.Payload.ToArray();
            Compare(bytes, catalog);
            var baseline = new double[300]; var buffered = new double[300];
            for (int i = -60; i < 300; i++)
            {
                double a, b;
                if ((i & 1) == 0) { a = Measure(true); b = Measure(false); }
                else { b = Measure(false); a = Measure(true); }
                if (i >= 0) { baseline[i] = a; buffered[i] = b; }
            }
            TestContext.WriteLine($"{ship.IdString}: {bytes.Length} compressed bytes, {ship.CellCount} solid cells; full decode+structure storage, disposal excluded; 60 warmup/300 paired samples");
            new PerfRun(TestContext.CurrentContext.Test.FullName).AddSamples("LegacyDecodeMs", baseline)
                .AddSamples("BufferedDecodeMs", buffered).Write();
            double Measure(bool legacy)
            {
                var timer = Stopwatch.StartNew();
                VoxsDecoded decoded; string error;
                bool ok = legacy ? VoxsCodec.TryDecodeLegacyInput(bytes, catalog, Allocator.TempJob, out decoded, out error)
                    : VoxsCodec.TryDecode(bytes, catalog, Allocator.TempJob, out decoded, out error);
                using (decoded)
                {
                    Assert.IsTrue(ok, error);
                    using var structure = VoxelStructureCodec.FromDecoded(decoded); timer.Stop();
                    Assert.AreEqual(ship.CellCount, structure.SolidCellCount);
                    return timer.Elapsed.TotalMilliseconds;
                }
            }
        }
        sealed class ReferenceSettings : IDisposable
        {
            readonly bool burst = BurstCompiler.Options.EnableBurstCompilation, safety = BurstCompiler.Options.EnableBurstSafetyChecks,
                synchronous = BurstCompiler.Options.EnableBurstCompileSynchronously, debugger = JobsUtility.JobDebuggerEnabled;
            readonly NativeLeakDetectionMode leaks = NativeLeakDetection.Mode;
            internal ReferenceSettings()
            {
                BurstCompiler.Options.EnableBurstCompilation = true; BurstCompiler.Options.EnableBurstSafetyChecks = false;
                BurstCompiler.Options.EnableBurstCompileSynchronously = true; JobsUtility.JobDebuggerEnabled = false;
                NativeLeakDetection.Mode = NativeLeakDetectionMode.Disabled;
            }
            public void Dispose()
            {
                BurstCompiler.Options.EnableBurstCompilation = burst; BurstCompiler.Options.EnableBurstSafetyChecks = safety;
                BurstCompiler.Options.EnableBurstCompileSynchronously = synchronous; JobsUtility.JobDebuggerEnabled = debugger;
                NativeLeakDetection.Mode = leaks;
            }
        }
    }
}
