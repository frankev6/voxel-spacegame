using Unity.Mathematics;

namespace Game.Net.Replication
{
    /// <summary>Own-ship prediction on a guest (SPEC-90 NET-011). The guest flies its ship as a local dynamic body from its own input.
    /// Each fixed tick records the body's state under the command sequence that tick used; when a snapshot acknowledges sequence S,
    /// the host's state is compared with the recorded state for S and part of the difference is applied to the body and to every
    /// newer record, so a persistent error shrinks geometrically without a visible snap. No replay: a ship's motion between
    /// snapshots is smooth enough that the error measured at S still holds now. Main thread.</summary>
    public sealed class OwnShipPredictor
    {
        public struct BodyState
        {
            public uint Sequence;
            public double3 Position;       // universe space, so an origin shift between records is harmless
            public quaternion Rotation;
            public float3 Linear, Angular; // angular in world space
        }

        public const float PositionBlend = .3f, VelocityBlend = .5f;   // per acknowledged snapshot (30 Hz)
        public const float SnapDistance = 6f, SnapAngle = .5f;          // metres, radians: beyond these the body jumps to the host
        const int Capacity = 256;                                       // 4 s at 60 Hz
        readonly BodyState[] history = new BodyState[Capacity];
        int start, count;

        public uint LastAck { get; private set; }
        public uint AppliedSequence { get; set; }      // sequence of the command the last fixed tick used
        public bool HasApplied { get; set; }
        public float LastErrorM { get; private set; }
        public float PeakErrorM { get; private set; }
        public int Snaps { get; private set; }
        public int Corrections { get; private set; }

        public void Reset()
        {
            start = count = 0; LastAck = 0; AppliedSequence = 0; HasApplied = false;
            LastErrorM = PeakErrorM = 0; Snaps = Corrections = 0;
        }

        public void Record(in BodyState state)
        {
            int at = (start + count) % Capacity;
            if (count == Capacity) start = (start + 1) % Capacity; else count++;
            history[at] = state;
        }

        public int Count => count;
        BodyState At(int i) => history[(start + i) % Capacity];

        /// <summary>Applies the host's state for acknowledged command <paramref name="ack"/> to <paramref name="current"/>. Returns false
        /// when there is nothing to correct (an old ack, or no record for it yet).</summary>
        public bool Reconcile(uint ack, in BodyState host, ref BodyState current)
        {
            if (ack == 0 || unchecked((int)(ack - LastAck)) <= 0 && LastAck != 0) return false;
            int index = -1;
            for (int i = count - 1; i >= 0; i--)
            {
                uint seq = At(i).Sequence;
                if (seq == ack) { index = i; break; }
                if (unchecked((int)(seq - ack)) < 0) { index = i; break; }   // ack's own ticks fell out or never ran here: nearest older
            }
            if (index < 0) return false;
            LastAck = ack;
            var recorded = At(index);
            double3 dp = host.Position - recorded.Position;
            float3 dv = host.Linear - recorded.Linear, dw = host.Angular - recorded.Angular;
            quaternion dq = math.normalizesafe(math.mul(host.Rotation, math.inverse(recorded.Rotation)), quaternion.identity);
            if (dq.value.w < 0) dq.value = -dq.value;   // shortest arc
            float error = (float)math.length(dp), angle = 2 * math.acos(math.saturate(dq.value.w));
            LastErrorM = error; PeakErrorM = math.max(PeakErrorM, error); Corrections++;
            bool snap = error > SnapDistance || angle > SnapAngle;
            if (snap) Snaps++;
            float a = snap ? 1 : PositionBlend, b = snap ? 1 : VelocityBlend;
            quaternion turn = math.slerp(quaternion.identity, dq, a);
            Shift(ref current, dp * a, dv * b, dw * b, turn);
            for (int i = index; i < count; i++)
            {
                var s = At(i); Shift(ref s, dp * a, dv * b, dw * b, turn); history[(start + i) % Capacity] = s;
            }
            return true;
        }

        static void Shift(ref BodyState s, double3 dp, float3 dv, float3 dw, quaternion turn)
        {
            s.Position += dp; s.Linear += dv; s.Angular += dw;
            s.Rotation = math.normalizesafe(math.mul(turn, s.Rotation), quaternion.identity);
        }
    }
}
