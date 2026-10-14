using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Game.Core;
using Game.Data;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Editor.Content;
using Game.Editor.Migration;
using Game.Editor.Migration.Converters;
using Game.Tests.Common.Perf;
using Game.Voxel.Data;
using NUnit.Framework;
using Unity.Collections;
using Unity.PerformanceTesting;
using UnityEngine.Profiling;
using UnityEditor;

namespace Game.Tests.EditMode.Editor.Migration
{
    /// <summary>SPEC-72 MIG-016: the actual shipped migration is error-free, deterministic and within its Editor budget.</summary>
    public sealed class MigrationGateTests
    {
        ValidationReport first, second;
        string firstReport, secondReport;
        byte[] firstManifest, secondManifest;
        Dictionary<string, byte[]> assets;
        readonly double[] fullTimes = new double[2];
        [OneTimeSetUp] public void RunRealExportTwice()
        {
            var clock = Stopwatch.StartNew(); first = LegacyMigrationRunner.RunAll(false); fullTimes[0] = clock.Elapsed.TotalMilliseconds;
            firstReport = File.ReadAllText("Import/Migration/report.md"); firstManifest = File.ReadAllBytes("Import/Migration/manifest.json");
            assets = AssetRows(firstReport).Select(r => r.Path).Distinct().ToDictionary(path => path, File.ReadAllBytes);
            clock.Restart(); second = LegacyMigrationRunner.RunAll(false); fullTimes[1] = clock.Elapsed.TotalMilliseconds;
            secondReport = File.ReadAllText("Import/Migration/report.md"); secondManifest = File.ReadAllBytes("Import/Migration/manifest.json");
        }
        static IEnumerable<(string Path, string Outcome)> AssetRows(string report)
        {
            string table = report.Split("## Assets\n")[1].Split("## Ships\n")[0];
            foreach (string line in table.Split('\n'))
                if (line.StartsWith("|Assets/", StringComparison.Ordinal)) { var cells = line.Split('|'); yield return (cells[1], cells[2]); }
        }
        [Test] public void RealExport_ZeroErrors()
        {
            Assert.AreEqual(0, first.ErrorCount, first.ToText()); Assert.AreEqual(0, second.ErrorCount, second.ToText());
            var report = ContentValidatorRunner.Validate(ContentDatabaseBuilder.Rebuild()); Assert.AreEqual(0, report.ErrorCount, report.ToText());
            Assert.AreEqual(129, AssetRows(secondReport).Select(r => r.Path).Distinct().Count()); // 35 items, 21 blocks, 13 ships, 20 audio cues, 9 weapons, 2 abilities, 2 loot tables, 3 factions, 20 bodies, 4 encounters.
        }
        [Test] public void SecondRun_IsIdempotent()
        {
            // AUD-007 adopted these exact MIG-011 cues; the legacy writer must preserve their authored playback fields.
            var protectedAudio = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in ("editor_place editor_remove explosion_large explosion_small impact_hull "
                + "music_combat music_exploration music_menu music_station ui_click ui_fail ui_hover "
                + "ui_slot_click ui_slot_hover ui_success ui_view_close ui_view_open "
                + "weapon_cannon_fire weapon_laser_fire weapon_plasma_fire").Split(' '))
                protectedAudio.Add("Assets/Game/Content/Audio/cue_" + id + ".asset");
            var audioOwnership = MigrationManifest.Load("Import/Migration/audio-content-manifest.json");
            foreach (var row in AssetRows(secondReport))
            {
                bool protectedPlasma = row.Path == "Assets/Game/Content/Weapons/wpn_plasma_turret_medium.asset"
                    || row.Path == "Assets/Game/Content/Weapons/wpn_plasma_turret_small.asset"
                    || row.Path == "Assets/Game/Content/Weapons/wpn_plasma_turret_small_fixed.asset";
                bool authoredAudio = protectedAudio.Contains(row.Path);
                if (authoredAudio)
                {
                    Assert.IsTrue(audioOwnership.TryGetHash(row.Path, out ulong expected), row.Path);
                    Assert.AreEqual(expected, StableHash.Hash64(assets[row.Path]), "Legacy migration changed AUD-007 content: " + row.Path
                        + " now " + StableHash.Hash64(assets[row.Path]).ToString("x16"));
                }
                // Later owners re-author migrated assets (bodies, encounters, blocks, ships; QA-001's NOCTURNE layers and repaint):
                // the migration keeps them as hand-edited and never rewrites them. Protected assets must always be skipped.
                if (protectedPlasma || authoredAudio) Assert.AreEqual("SkippedHandEdited", row.Outcome.ToString(), row.Path);
                else Assert.That(row.Outcome.ToString(), Is.EqualTo("Unchanged").Or.EqualTo("SkippedHandEdited"), row.Path);
                CollectionAssert.AreEqual(assets[row.Path], File.ReadAllBytes(row.Path), row.Path);
            }
            Assert.AreEqual(firstReport, secondReport); CollectionAssert.AreEqual(firstManifest, secondManifest);
        }
        [Test] public void ShipTable_MatchesSpec()
        {
            string table = secondReport.Split("## Ships\n")[1].Split("## Entries\n")[0];
            var rows = table.Split('\n').Where(line => line.StartsWith("|ship_", StringComparison.Ordinal)).ToArray();
            var expected = ShipConverterTests.Rows.Split('\n').Select(line => "|" + line.Trim().Replace(',', '|') + "|").ToArray();
            CollectionAssert.AreEqual(expected, rows);
        }
        [Test, Performance, Category("Performance")] public void RunTime_Under60Seconds()
        {
            PerfRun.Gate("MigrationFullRun", fullTimes, 60000);
            foreach (double time in fullTimes) Assert.LessOrEqual(time, 60000);
            MeasureIndividualShips();
        }
        static void RecordGate(string name, double value, double budget, string unit)
        {
            var run = new PerfRun(TestContext.CurrentContext.Test.FullName + "." + name).AddSamples(name, new[] { value }, unit);
            run.Write(name, budget); run.AssertMedian(name, budget);
        }
        static void MeasureIndividualShips()
        {
            // Copy one input at a time outside the timed region. The converter measures read, build, encode,
            // verification and first asset write; it uses the production block catalog and unmodified legacy bytes.
            const string target = "Assets/Game/Content/_MigrationPerf";
            Assert.IsFalse(AssetDatabase.IsValidFolder(target), "Never remove pre-existing content.");
            string input = Path.Combine(Path.GetTempPath(), "spaceship-migration-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(input, "content")); Directory.CreateDirectory(target + "/Ships"); AssetDatabase.Refresh();
            try
            {
                var report = new ValidationReport("MIG"); string legacyRoot = Path.GetFullPath("Import/Legacy");
                var index = LegacyContentIndex.Load(legacyRoot, report);
                var db = AssetDatabase.LoadAssetAtPath<ContentDatabase>(ContentDatabaseBuilder.DatabasePath);
                var catalog = ContentCatalog.Build(db.Definitions, report);
                var writer = new LegacyAssetWriter(target, new MigrationManifest(), false, report);
                var times = new List<double>(); long maxManaged = 0, maxNativeBound = 0;
                foreach (var file in index.OfType("ShipItem"))
                {
                    File.Copy(Path.Combine(legacyRoot, file.RelativePath), Path.Combine(input, "content/ship.json"), true);
                    var context = new LegacyMigrationContext(LegacyContentIndex.Load(input, report), LegacyIdMap.Default, writer, report, false, input) { Catalog = catalog };
                    // Unity's Mono allocation-by-thread API returns zero; GC.Alloc recorder values
                    // are durations, not bytes. Measure live + uncollected managed bytes and require
                    // that no collection invalidates the delta during this short synchronous call.
                    GC.Collect(); int collections = GC.CollectionCount(0);
                    long before = Profiler.GetMonoUsedSizeLong(); var clock = Stopwatch.StartNew();
                    new ShipConverter().Convert(context); clock.Stop();
                    long managed = Profiler.GetMonoUsedSizeLong() - before;
                    Assert.AreEqual(collections, GC.CollectionCount(0), "A collection invalidated the managed memory measurement.");
                    Assert.Greater(managed, 0, "Memory measurement must be available.");
                    times.Add(clock.Elapsed.TotalMilliseconds); maxManaged = Math.Max(maxManaged, managed);
                    Assert.IsFalse(report.HasErrors, report.ToText());
                    var asset = AssetDatabase.LoadAssetAtPath<ShipBlueprintDefinition>(writer.Outcomes.Last().AssetPath);
                    Assert.IsTrue(VoxsCodec.TryDecode(asset.Payload, catalog, Allocator.TempJob, out var decoded, out string error), error);
                    using (decoded)
                    {
                        // Conservative native working-set bound: source + decoded + RLE scratch, including
                        // worst-case run buffers (5 bytes/cell) and block/brick arrays. Managed allocation
                        // above is cumulative, hence an upper bound on that ship's managed live work.
                        long native = decoded.Cells.Length * 13L + decoded.Palette.Length * 12L + decoded.Instances.Length * 96L + decoded.BrickCoords.Length * 48L + Profiler.GetRuntimeMemorySizeLong(asset);
                        maxNativeBound = Math.Max(maxNativeBound, native);
                    }
                }
                RecordGate("MigrationSlowestShip", times.Max(), 200, "ms");
                RecordGate("MigrationShipMemoryUpperBound", maxManaged + maxNativeBound, 10 * 1024 * 1024, "bytes");
                UnityEngine.Debug.Log($"Migration: 13 first-write ships, slowest {times.Max():F2} ms; managed allocated <= {maxManaged} B, native working-set bound <= {maxNativeBound} B.");
            }
            finally { AssetDatabase.DeleteAsset(target); Directory.Delete(input, true); }
        }
    }
}
