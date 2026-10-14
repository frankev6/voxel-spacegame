using System.Diagnostics;
using Game.Tests.Common.Perf;
using Game.Tests.EditMode.Data;
using Game.Voxel.Damage;
using NUnit.Framework;
using Unity.Entities;

namespace Game.Tests.EditMode.Simulation.Factions
{
    /// <summary>Advisory end-to-end scheduling plus completion cost; never part of the default suite.</summary>
    public sealed class FactionStandingPerfTests
    {
        [TearDown] public void Cleanup() => TestContentFactory.DestroyAll();
        [Test, Category("Performance")] public void Tick_200Grids()
        {
            using var f = new FactionStandingTests.Fixture();
            var grids = new Entity[200];
            for (int i = 0; i < grids.Length; i++) grids[i] = f.Damage(f.Index, uint.MaxValue, Entity.Null);
            var samples = new double[300];
            for (uint tick = 1; tick <= 360; tick++)
            {
                for (int i = 0; i < 10; i++) f.Em.SetComponentData(grids[i], new GridDamageStats { LastDamageTick = tick - 1, LastDamageInstigator = f.Player });
                long started = Stopwatch.GetTimestamp(); f.Tick(tick);
                if (tick > 60) samples[tick - 61] = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
            }
            Assert.AreEqual(-30, f.Standing(f.Index).Relation);
            PerfRun.Gate("FactionStandingSystem.Tick_200Grids", samples, .02);
        }
    }
}
