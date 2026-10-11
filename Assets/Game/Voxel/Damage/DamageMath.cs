using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Voxel.Damage
{
    /// <summary>Pure Burst-compatible maths. Implements SPEC-13 §5.3.</summary>
    public static class DamageMath
    {
        public static int3 CellOf(float3 localMetres, float cellSize) { throw new System.NotImplementedException("DMG-004"); }                        // floor(p / s)
        public static float HpUnits(byte hp, float cellHp) { throw new System.NotImplementedException("DMG-004"); }                                   // hp * cellHp / 255
        public static byte QuantizeHp(float units, float cellHp, float dither01) { throw new System.NotImplementedException("DMG-004"); }             // §5.3.5; 0 = destroyed
        public static float Dither01(uint tick, int gridIndex, int3 cell, int eventOrdinal) { throw new System.NotImplementedException("DMG-004"); }  // §5.3.5
        public static float BlastFalloff(float d, float r) { throw new System.NotImplementedException("DMG-004"); }                                   // (1 − d/r)², 0 when d >= r
        public static float3 VelocityAtPoint(in PhysicsVelocity v, in PhysicsMass m, in LocalTransform t, float3 worldPoint) { throw new System.NotImplementedException("DMG-004"); }
        public static float RamEnergy(float impulse, float invMassA, float invMassB, float restitutionCorrection) { throw new System.NotImplementedException("DMG-004"); } // §5.2
    }
}
