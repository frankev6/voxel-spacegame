using Game.Core.Components;
using Game.Simulation.Weapons;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
namespace Game.Presentation.Vfx
{
    /// <summary>SPEC-63 §5.3: one output index per projectile row, no scatter or duplicate writes.</summary>
    [BurstCompile] internal struct TracerGatherJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<ProjectileRow> Rows;
        public NativeArray<TracerInstance> Output;
        public float3 Correction;
        public float Alpha, LengthSeconds, MinLength, MaxLength, Width;
        public float4 Color0, Color1, Color2, Color3;
        public void Execute(int i)
        {
            var row = Rows[i]; float speed = math.length(row.Velocity);
            if ((row.Flags & ProjectileFlags.Dead) != 0 || speed < .01f) { Output[i] = default; return; }
            Output[i] = new TracerInstance { Head = row.Position + Correction + row.Velocity * Alpha, Dir = -row.Velocity / speed,
                Length = math.clamp(speed * LengthSeconds, MinLength, MaxLength), Width = Width,
                Color = (int)row.Type == 0 ? Color0 : (int)row.Type == 1 ? Color1 : (int)row.Type == 2 ? Color2 : Color3 };
        }
        internal static bool PendingShift(in OriginShiftRecord record, uint applied, out float3 correction)
        {
            correction = float3.zero;
            if (applied == record.Sequence) return true;
            if (record.StarSystemChanged != 0 || record.Sequence - applied != 1) return false;
            correction = record.Delta; return true;
        }
    }
}
