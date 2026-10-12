using Game.Core;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Voxel.Damage
{
    /// <summary>Pure Burst-compatible maths. Implements SPEC-13 §5.3.</summary>
    public static class DamageMath
    {
        public static int3 CellOf(float3 localMetres, float cellSize) => (int3)math.floor(localMetres / cellSize);
        public static float HpUnits(byte hp, float cellHp) => hp * (cellHp / 255f);
        public static byte QuantizeHp(float units, float cellHp, float dither01)
            => (byte)math.clamp(math.floor(units * (255f / cellHp) + dither01), 0f, 255f);
        internal static ulong Packed(int3 c) => ((ulong)(c.x & 0x1FFFFF) << 42) | ((ulong)(c.y & 0x1FFFFF) << 21) | (uint)(c.z & 0x1FFFFF);
        public static float Dither01(uint tick, int gridIndex, int3 cell, int eventOrdinal)
        {
            var random = Random.CreateFromIndex((uint)StableHash.Combine(StableHash.Combine(tick, (ulong)gridIndex),
                StableHash.Combine((uint)eventOrdinal, Packed(cell))));
            return random.NextFloat();
        }
        public static float BlastFalloff(float d, float r)
        {
            if (r <= 0f || d >= r) return 0f;
            float f = 1f - d / r; return f * f;
        }
        public static float3 VelocityAtPoint(in PhysicsVelocity v, in PhysicsMass m, in LocalTransform t, float3 worldPoint)
        {
            float3 angular = math.rotate(math.mul(t.Rotation, m.InertiaOrientation), v.Angular);
            return v.Linear + math.cross(angular, worldPoint - t.TransformPoint(m.CenterOfMass));
        }
        public static float RamEnergy(float impulse, float invMassA, float invMassB, float restitutionCorrection)
            => impulse * impulse * (invMassA + invMassB) / (2f * restitutionCorrection);
    }
}
