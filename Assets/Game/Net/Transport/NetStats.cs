namespace Game.Net.Transport
{
    public enum NetChannel : byte { Reliable = 0, Unreliable = 1 }

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

        public void Sent(NetChannel c, int bytes) { outBytes[(int)c] += bytes; outWindow[(int)c] += bytes; }
        public void SentMessage(NetChannel c) => outMessages[(int)c]++;
        public void Received(NetChannel c, int bytes) { inBytes[(int)c] += bytes; inWindow[(int)c] += bytes; inMessages[(int)c]++; }
        public void Tick(double now)
        {
            double span = now - windowStart;
            if (span < 1) return;
            for (int i = 0; i < 2; i++)
            {
                outRate[i] = (float)(outWindow[i] * 8 / 1000.0 / span); inRate[i] = (float)(inWindow[i] * 8 / 1000.0 / span);
                outWindow[i] = 0; inWindow[i] = 0;
            }
            windowStart = now;
        }
        public void Reset()
        {
            for (int i = 0; i < 2; i++) { outBytes[i] = inBytes[i] = outMessages[i] = inMessages[i] = outWindow[i] = inWindow[i] = 0; outRate[i] = inRate[i] = 0; }
            Desyncs = 0; DroppedSnapshots = 0; SnapshotAgeMs = 0; ReplicatedGrids = 0; ReplicatedChunks = 0; PeakReliableBacklog = 0;
        }
    }
}
