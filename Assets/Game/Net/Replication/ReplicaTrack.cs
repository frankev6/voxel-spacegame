using Game.Simulation.Commands;
using Unity.Mathematics;

namespace Game.Net.Replication
{
    /// <summary>One received transform sample, in universe space (local position + the host's frame origin at that tick).</summary>
    public struct ReplicaSample
    {
        public uint Tick;
        public double3 Position;
        public quaternion Rotation;
        public float3 Linear, Angular;
        public float Scale;
        public byte HasCommand, HasShield;
        public ShipCommand Command;
        public float ShieldHp, ShieldMax; public byte ShieldActive;
        public byte HasWeapons, Autofire, AimMode; public ulong Target;
        public byte HasAck; public uint Ack;
        public ulong AimAnchor; public float3 AimLocal;   // player ships' aim as a point on a grid (AimAnchor.cs)   // newest pilot command the host had applied (remote-piloted ships)
    }

    /// <summary>Interpolation buffer for one replicated body (SPEC-90 NET-005): newest samples by host tick, sampled 100 ms behind.</summary>
    public sealed class ReplicaTrack
    {
        const int Capacity = 16;
        readonly ReplicaSample[] samples = new ReplicaSample[Capacity];
        int count;
        public uint NewestTick => count == 0 ? 0 : samples[count - 1].Tick;
        public int Count => count;

        /// <summary>Adds a sample; older-or-equal ticks are ignored (unreliable packets may reorder). Returns false when dropped.</summary>
        public bool Add(in ReplicaSample sample)
        {
            if (count > 0 && sample.Tick <= samples[count - 1].Tick) return false;
            if (count == Capacity) { System.Array.Copy(samples, 1, samples, 0, Capacity - 1); count--; }
            samples[count++] = sample;
            return true;
        }

        public ReplicaSample Latest => samples[count - 1];

        /// <summary>Pose at <paramref name="tick"/> (fractional host ticks). Interpolates between neighbours, extrapolates up to
        /// <paramref name="maxExtrapolationTicks"/> past the newest, clamps before the oldest.</summary>
        public bool Sample(double tick, float maxExtrapolationTicks, out ReplicaSample result)
        {
            result = default;
            if (count == 0) return false;
            if (tick <= samples[0].Tick) { result = samples[0]; return true; }
            for (int i = count - 1; i >= 0; i--)
            {
                if (samples[i].Tick > tick) continue;
                var a = samples[i];
                if (i == count - 1)
                {
                    float dt = (float)math.min(tick - a.Tick, maxExtrapolationTicks) / 60f;
                    result = a;
                    result.Position = a.Position + (double3)(a.Linear * dt);
                    float angle = math.length(a.Angular) * dt;
                    if (angle > 1e-5f) result.Rotation = math.normalize(math.mul(quaternion.AxisAngle(math.normalize(a.Angular), angle), a.Rotation));
                    return true;
                }
                var b = samples[i + 1];
                float t = (float)((tick - a.Tick) / (b.Tick - a.Tick));
                result = t < .5f ? a : b;   // discrete fields (command, shield flags) from the nearer sample
                result.Tick = (uint)tick;
                result.Position = math.lerp(a.Position, b.Position, t);
                result.Rotation = math.slerp(a.Rotation, b.Rotation, t);
                result.Linear = math.lerp(a.Linear, b.Linear, t);
                result.Angular = math.lerp(a.Angular, b.Angular, t);
                result.Scale = math.lerp(a.Scale, b.Scale, t);
                result.ShieldHp = math.lerp(a.ShieldHp, b.ShieldHp, t);
                return true;
            }
            result = samples[0];
            return true;
        }

        public void Clear() => count = 0;

        // Thruster plumes (not interpolated): the newest snapshot's lit thrusters, by block instance, and what the guest last showed.
        public const int MaxPlumes = 64;
        public static byte EncodePlume(float intensity) => (byte)math.clamp((int)math.round(intensity * 100), 0, 255);
        public static float DecodePlume(byte level) => level / 100f;
        public readonly ushort[] PlumeInstances = new ushort[MaxPlumes];
        public readonly byte[] PlumeLevels = new byte[MaxPlumes];
        public int PlumeCount;
        public uint PlumeVersion;
        public readonly ushort[] ShownPlumes = new ushort[MaxPlumes];
        public int ShownCount;
        public uint ShownVersion;
    }
}
