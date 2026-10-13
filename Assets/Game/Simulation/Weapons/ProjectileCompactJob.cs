using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Simulation.Weapons
{
    /// <summary>SPEC-21 §5.6: retain fire order and reduce independently written batch counters.</summary>
    [BurstCompile]
    internal struct ProjectileCompactJob : IJob
    {
        public NativeList<ProjectileRow> Rows;
        public NativeReference<ProjectileStoreStats> Stats;
        [ReadOnly] public NativeArray<int2> BatchCounts; // hits, expired; one entry per 64-row stream lane

        public void Execute()
        {
            int live = 0;
            for (int i = 0; i < Rows.Length; i++)
            {
                var row = Rows[i];
                if ((row.Flags & ProjectileFlags.Dead) == 0) Rows[live++] = row;
            }
            Rows.ResizeUninitialized(live);
            var stats = Stats.Value;
            for (int i = 0; i < BatchCounts.Length; i++)
            {
                stats.Hits += BatchCounts[i].x;
                stats.Expired += BatchCounts[i].y;
            }
            stats.Live = live;
            stats.PeakLive = math.max(stats.PeakLive, live);
            Stats.Value = stats;
        }
    }
}
