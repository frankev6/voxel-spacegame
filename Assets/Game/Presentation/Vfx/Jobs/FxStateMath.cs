using Game.Simulation.Power;
using Unity.Mathematics;
namespace Game.Presentation.Vfx
{
    internal static class FxStateMath
    {
        internal static float4 Linear(uint c)
        {
            float4 f = new float4(c & 255, c >> 8 & 255, c >> 16 & 255, c >> 24) / 255f;
            f.xyz = math.select(math.pow((f.xyz + .055f) / 1.055f, 2.4f), f.xyz / 12.92f, f.xyz <= .04045f); return f;
        }
        internal static PlumeInstance Plume(float4x4 matrix, float3 nozzle, float3 direction, float radius, float intensity, float length, float4 color)
        {
            color.w = intensity;
            return new PlumeInstance { Nozzle = math.transform(matrix, nozzle), Dir = math.normalizesafe(math.rotate(matrix, direction)),
                Radius = radius, Length = length * intensity * radius / .75f, Color = color };
        }
        internal static float4 Ripple(in ShieldRipple ripple, in ShieldState shield, uint tick, float lifetime)
        {
            if (ripple.StartTick == 0 || tick < ripple.StartTick) return new float4(0, 0, 0, -1);
            float age = (tick - ripple.StartTick) / 60f;
            if (age > lifetime) return new float4(0, 0, 0, -1);
            // Transform into unit-ellipsoid coordinates before normalizing (SPEC-63 Q23).
            return new float4(math.normalizesafe((ripple.LocalPoint - shield.Center) / shield.Radii), age);
        }
        internal static float Dissolve(uint endTick, uint tick, float seconds) =>
            math.saturate(1 - (endTick > tick ? (endTick - tick) / 60f : 0) / math.max(.001f, seconds));
    }
}
