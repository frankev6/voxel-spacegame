using Game.Simulation.Power;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
namespace Game.Presentation.Vfx
{
    /// <summary>SPEC-63 §5.5; reads analytic state and eight impacts without changing shield simulation.</summary>
    [BurstCompile] internal partial struct ShieldGatherJob : IJobEntity
    {
        public NativeArray<ShieldInstance> Output;
        public NativeReference<int> Count;
        public int Cap;
        public uint Tick;
        public float RippleSeconds, FlashSeconds;
        public float4 Color;
        void Execute(in ShieldState shield, in LocalToWorld transform, in DynamicBuffer<ShieldRipple> ripples)
        {
            if (Count.Value >= Cap || shield.Active == 0 || shield.MaxHp <= 0 || math.any(shield.Radii <= 0)) return;
            var value = new ShieldInstance { EllipsoidToWorld = math.mul(transform.Value, float4x4.TRS(shield.Center, quaternion.identity, shield.Radii)),
                HpFraction = math.saturate(shield.Hp / shield.MaxHp), Color = Color };
            float youngest = float.MaxValue;
            for (int i = 0; i < 8; i++)
            {
                float4 ripple = i < ripples.Length ? FxStateMath.Ripple(ripples[i], shield, Tick, RippleSeconds) : new float4(0, 0, 0, -1);
                if (ripple.w >= 0) youngest = math.min(youngest, ripple.w);
                switch (i) { case 0: value.Ripple0 = ripple; break; case 1: value.Ripple1 = ripple; break; case 2: value.Ripple2 = ripple; break;
                    case 3: value.Ripple3 = ripple; break; case 4: value.Ripple4 = ripple; break; case 5: value.Ripple5 = ripple; break;
                    case 6: value.Ripple6 = ripple; break; default: value.Ripple7 = ripple; break; }
            }
            value.Flash = 1 - math.saturate(youngest / math.max(.001f, FlashSeconds)); Output[Count.Value++] = value;
        }
    }
}
