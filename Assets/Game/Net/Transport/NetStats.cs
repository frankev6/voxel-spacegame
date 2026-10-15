namespace Game.Net.Transport
{
    public enum NetChannel : byte { Reliable = 0, Unreliable = 1 }

    /// <summary>Main-thread work the co-op layer adds to a frame, timed for `net status` (SPEC-90 §10a gap 1).</summary>
    public enum NetCost : byte { Capture = 0, Snapshot = 1, Hash = 2, Apply = 3, BodySync = 4 }

    /// <summary>Rolling one-second traffic counters for the overlay and `net status` (SPEC-90 §4). Main thread.</summary>
    public sealed class NetStats
    {
        readonly long[] outBytes = new long[2], inBytes = new long[2], outMessages = new long[2], inMessages = new long[2];
        readonly long[] outWindow = new long[2], inWindow = new long[2];
        double windowStart;
        public float OutKbps(NetChannel c) => outRate[(int)c];
        public float InKbps(NetChannel c) => inRate[(int)c];
        readonly float[] outRate = new float[2], inRate = new float[2];
        public long TotalOut(NetChannel c) => outBytes[(int)c];
        public long TotalIn(NetChannel c) => inBytes[(int)c];
        public long MessagesOut(NetChannel c) => outMessages[(int)c];
        public long MessagesIn(NetChannel c) => inMessages[(int)c];
        public int Desyncs;            // cell hash mismatches and dropped deltas for unknown grids
        public int DroppedSnapshots;   // unreliable snapshots older than the newest applied
        public float SnapshotAgeMs;    // guest: age of the newest snapshot when last sampled
        public int ReplicatedGrids, ReplicatedChunks;
        public long PeakReliableBacklog;

        // Costs: per-frame average and worst single call over the last one-second window.
        public const int CostCount = 5;
        readonly double[] costWindow = new double[CostCount], costPeakWindow = new double[CostCount];
        readonly float[] costAvg = new float[CostCount], costPeak = new float[CostCount], costWorst = new float[CostCount];
        int framesWindow;
        public float CostAvgMs(NetCost c) => costAvg[(int)c];
        public float CostPeakMs(NetCost c) => costPeak[(int)c];
        public float CostWorstMs(NetCost c) => costWorst[(int)c];   // whole session
        public static long Stamp() => System.Diagnostics.Stopwatch.GetTimestamp();
        public void AddCost(NetCost c, long startStamp)
        {
            double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - startStamp) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            costWindow[(int)c] += ms; if (ms > costPeakWindow[(int)c]) costPeakWindow[(int)c] = ms;
            if (ms > costWorst[(int)c]) costWorst[(int)c] = (float)ms;
        }

        public void Sent(NetChannel c, int bytes) { outBytes[(int)c] += bytes; outWindow[(int)c] += bytes; }
        public void SentMessage(NetChannel c) => outMessages[(int)c]++;
        public void Received(NetChannel c, int bytes) { inBytes[(int)c] += bytes; inWindow[(int)c] += bytes; inMessages[(int)c]++; }
        public void Tick(double now)
        {
            framesWindow++;
            double span = now - windowStart;
            if (span < 1) return;
            for (int i = 0; i < CostCount; i++)
            {
                costAvg[i] = (float)(costWindow[i] / framesWindow); costPeak[i] = (float)costPeakWindow[i];
                costWindow[i] = 0; costPeakWindow[i] = 0;
            }
            framesWindow = 0;
            for (int i = 0; i < 2; i++)
            {
                outRate[i] = (float)(outWindow[i] * 8 / 1000.0 / span); inRate[i] = (float)(inWindow[i] * 8 / 1000.0 / span);
                outWindow[i] = 0; inWindow[i] = 0;
            }
            windowStart = now;
        }
        public void ResetCosts() { for (int i = 0; i < CostCount; i++) costWorst[i] = 0; }
        public void Reset()
        {
            for (int i = 0; i < 2; i++) { outBytes[i] = inBytes[i] = outMessages[i] = inMessages[i] = outWindow[i] = inWindow[i] = 0; outRate[i] = inRate[i] = 0; }
            Desyncs = 0; DroppedSnapshots = 0; SnapshotAgeMs = 0; ReplicatedGrids = 0; ReplicatedChunks = 0; PeakReliableBacklog = 0;
        }
    }
}
