using System.Collections;
using Game.Core;
using Game.Voxel.Model;
using Game.Voxel.Damage;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.Combat
{
    /// <summary>DMG-015: ten elapsed minutes and 36,000 fixed ticks of the DMG-014 skirmish, with seeded despawns.</summary>
    [Category("Scenario")]
    public sealed class DamageSoakTests
    {
        [UnityTest, Timeout(900000)]
        public IEnumerator TenMinuteSoak()
        {
            using var leak = new DamageLeakScope();
            DamageScenarioProbe.WarmUp(); LogAssert.NoUnexpectedReceived(); UnsafeUtility.CheckForLeaks();
            // Flush the probe's diagnostic Log output before the gameplay log assertions (08-tools).
            yield return null;
            const int ticks = 600 * 60;
            const ulong seed = 0xD0155A0CUL;
            var random = RngStreams.For(seed, RngPurpose.Test, 15);
            int despawnTick = random.NextInt(360, 540), despawnSlot = random.NextInt(4);
            int dissolved = 0, silent = 0, chunksCreated = 0, hulksCreated = 0, rams = 0, maxLive = 0;
            double elapsed;
            using (var f = new DamageDeterminismTests.Fixture(true))
            {
                f.Begin(); uint start = f.Flight.Test.TicksRun; double began = Time.realtimeSinceStartupAsDouble;
                for (int i = 0; i < ticks; i++)
                {
                    int roundTick = i % 600;
                    if (i != 0 && roundTick == 0)
                    {
                        f.Respawn(); despawnTick = random.NextInt(360, 540); despawnSlot = random.NextInt(4);
                    }
                    if (roundTick == despawnTick)
                    {
                        var mode = random.NextBool() ? GridDissolveMode.Dissolve : GridDissolveMode.Silent;
                        var root = f.Roots[despawnSlot]; Assert.IsTrue(f.Em.Exists(root), "The seeded despawn must target a live original grid.");
                        DamageScenarioProbe.Request(f.Em, root, mode);
                        if (mode == GridDissolveMode.Dissolve) dissolved++; else silent++;
                    }
                    f.Flight.Tick(); var stats = DamageScenarioProbe.Observe(f.Flight.Test, out int live);
                    maxLive = System.Math.Max(maxLive, live); chunksCreated += stats.ChunksCreated; hulksCreated += stats.HulksCreated; rams += stats.RamEvents;
                    if (i % 60 != 59) continue;
                    LogAssert.NoUnexpectedReceived();
                    // Wall-clock pacing is only for soak exposure; every gameplay step still uses the fixed 1/60 tick.
                    double until = began + (i + 1) / 60.0;
                    while (Time.realtimeSinceStartupAsDouble < until) yield return null;
                    if ((i + 1) % 3600 == 0)
                        TestContext.WriteLine($"DMG-015 soak progress: {(i + 1) / 60}s / 600s, chunks created={chunksCreated}, max live={maxLive}, seeded despawns={dissolved + silent}.");
                }
                elapsed = Time.realtimeSinceStartupAsDouble - began;
                Assert.AreEqual((uint)ticks, f.Flight.Test.TicksRun - start); Assert.GreaterOrEqual(elapsed, 600);
                Assert.AreEqual(60, dissolved + silent, "One seeded random despawn in each repeated skirmish.");
                Assert.Greater(dissolved, 0); Assert.Greater(silent, 0);
                Assert.Greater(chunksCreated, 0, "The spur cut must exercise pooled chunks; the cap check cannot pass on an empty pool.");
                Assert.Greater(hulksCreated, 0, "The original scripted weapon-bearing split remains active."); Assert.Greater(rams, 0);
            }
            LogAssert.NoUnexpectedReceived(); Assert.AreEqual(0, UnsafeUtility.CheckForLeaks(), "All soak-owned native allocations must be released after World/content disposal.");
            TestContext.WriteLine($"DMG-015 soak: seed={seed:X}, ticks={ticks}, elapsed={elapsed:F2}s, dissolved={dissolved}, silent={silent}, chunks={chunksCreated}, hulks={hulksCreated}, rams={rams}, max live={maxLive}/64, native leaks=0.");
        }
    }
}
