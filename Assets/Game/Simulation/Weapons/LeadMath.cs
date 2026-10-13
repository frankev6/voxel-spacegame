using Unity.Mathematics;

namespace Game.Simulation.Weapons
{
    /// <summary>SPEC-21 §5.4.1 intercept point for shots inheriting shooter velocity.</summary>
    internal static class LeadMath
    {
        internal static float3 Point(float3 muzzle, float3 shooterVelocity, float3 center,
            float3 targetVelocity, float speed, float lifetimeMax)
        {
            float3 offset = center - muzzle, velocity = targetVelocity - shooterVelocity;
            float a = math.lengthsq(velocity) - speed * speed;
            float b = 2f * math.dot(offset, velocity), k = math.lengthsq(offset);
            float time = float.PositiveInfinity;
            if (math.abs(a) < 1e-6f)
            {
                if (b < 0f) time = -k / b;
            }
            else
            {
                float discriminant = b * b - 4f * a * k;
                if (discriminant >= 0f)
                {
                    float root = math.sqrt(discriminant);
                    float first = (-b - root) / (2f * a), second = (-b + root) / (2f * a);
                    if (first > 0f) time = first;
                    if (second > 0f) time = math.min(time, second);
                }
            }
            return time > 0f && time <= lifetimeMax ? center + velocity * time : center;
        }
    }
}
