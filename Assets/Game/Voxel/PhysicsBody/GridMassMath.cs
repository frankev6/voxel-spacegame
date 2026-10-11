using System.Threading;
using Game.Core;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>Exact accumulator-to-inertia conversion and mass-frame re-expression (SPEC-12 §5.11).</summary>
    public static class GridMassMath
    {
        struct ErrorContext { }
        static readonly SharedStatic<int> nonFiniteReported = SharedStatic<int>.GetOrCreate<ErrorContext>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetDiagnostics() => nonFiniteReported.Data = 0;

        /// <summary>False for empty/non-positive mass. Outputs are grid-local; the tensor is about the centre of mass.</summary>
        public static bool TryGetMassProperties(in StructureMassData d, bool planar,
            out float mass, out float3 centerOfMass, out quaternion inertiaOrientation, out float3 inertiaDiagonal)
        {
            mass = 0; centerOfMass = default; inertiaOrientation = quaternion.identity; inertiaDiagonal = default;
            if (d.CellCount == 0 || d.Mass <= 0) return false;
            double m = d.Mass;
            double3 c = d.FirstMoment / m;
            double3 diagonal = new double3(d.SecondDiag.y + d.SecondDiag.z, d.SecondDiag.x + d.SecondDiag.z,
                d.SecondDiag.x + d.SecondDiag.y) + d.CellInertia - m * new double3(c.y * c.y + c.z * c.z,
                c.x * c.x + c.z * c.z, c.x * c.x + c.y * c.y);
            if (planar) inertiaDiagonal = new float3(0, (float)diagonal.y, 0);
            else
            {
                double3 off = -d.SecondOff + m * new double3(c.x * c.y, c.y * c.z, c.z * c.x);
                var tensor = new double3x3(new double3(diagonal.x, off.x, off.z),
                    new double3(off.x, diagonal.y, off.y), new double3(off.z, off.y, diagonal.z));
                JacobiEigen(tensor, out var values, out var vectors);
                inertiaOrientation = math.normalize(new quaternion(new float3x3((float3)vectors.c0, (float3)vectors.c1, (float3)vectors.c2)));
                inertiaDiagonal = math.max((float3)values, (float)(1e-6 * math.cmax(values)));
            }
            mass = (float)m; centerOfMass = (float3)c;
            return true;
        }

        /// <summary>Requires non-empty, positive mass; bounds are inclusive cells.</summary>
        public static PhysicsMass CreateDynamicMass(in StructureMassData d, int3 cellMin, int3 cellMax, float cellSize, bool planar)
        {
            TryGetMassProperties(d, planar, out var mass, out var com, out var orientation, out var inertia);
            return new PhysicsMass
            {
                Transform = new RigidTransform(orientation, com),
                InverseMass = 1f / mass,
                InverseInertia = planar ? new float3(0, 1f / inertia.y, 0) : 1f / inertia,
                AngularExpansionFactor = AngularExpansion(com, cellMin, cellMax, cellSize),
            };
        }

        /// <summary>Infinite mass/inertia; retains the mass frame, or identity for an empty accumulator.</summary>
        public static PhysicsMass CreateKinematicMass(in StructureMassData d)
        {
            TryGetMassProperties(d, false, out _, out var com, out var orientation, out _);
            return new PhysicsMass { Transform = new RigidTransform(orientation, com) };
        }

        public static float AngularExpansion(float3 centerOfMass, int3 cellMin, int3 cellMax, float cellSize)
        {
            float3 lo = (float3)cellMin * cellSize;
            float3 hi = ((float3)cellMax + 1f) * cellSize;
            float farthestSquared = 0;
            for (int i = 0; i < 8; i++)
            {
                var corner = new float3((i & 1) == 0 ? lo.x : hi.x, (i & 2) == 0 ? lo.y : hi.y, (i & 4) == 0 ? lo.z : hi.z);
                farthestSquared = math.max(farthestSquared, math.distancesq(centerOfMass, corner));
            }
            return math.sqrt(farthestSquared);
        }

        /// <summary>Keeps point velocities when the centre of mass and inertia orientation change (SPEC-12 decision 12).</summary>
        public static void Reexpress(ref PhysicsVelocity v, in PhysicsMass oldMass, in PhysicsMass newMass, quaternion bodyRotation, bool planar)
        {
            float3 omega = math.rotate(math.mul(bodyRotation, oldMass.InertiaOrientation), v.Angular);
            float3 delta = math.rotate(bodyRotation, newMass.CenterOfMass - oldMass.CenterOfMass);
            var next = new PhysicsVelocity
            {
                Linear = v.Linear + math.cross(omega, delta),
                Angular = math.rotate(math.inverse(math.mul(bodyRotation, newMass.InertiaOrientation)), omega),
            };
            if (planar) { next.Angular.x = 0; next.Angular.z = 0; }
            if (math.all(math.isfinite(next.Linear)) && math.all(math.isfinite(next.Angular))) v = next;
            else if (Interlocked.CompareExchange(ref nonFiniteReported.Data, 1, 0) == 0)
            {
                FixedString128Bytes message = "GridMassMath.Reexpress: non-finite result; velocity unchanged.";
                GameLog.Burst.Error(message);
            }
        }

        /// <summary>Cyclic Jacobi, stable ascending eigenvalues and deterministic right-handed eigenvectors (§5.11).</summary>
        public static void JacobiEigen(double3x3 symmetric, out double3 values, out double3x3 vectors)
        {
            var a = symmetric; vectors = double3x3.identity;
            double trace = a.c0.x + a.c1.y + a.c2.z;
            for (int sweep = 0; sweep < 16; sweep++)
            {
                double offSquared = 2 * (a.c1.x * a.c1.x + a.c2.x * a.c2.x + a.c2.y * a.c2.y);
                if (offSquared < 1e-24 * trace * trace) break;
                Rotate(ref a, ref vectors, 0, 1);
                Rotate(ref a, ref vectors, 0, 2);
                Rotate(ref a, ref vectors, 1, 2);
            }
            values = new double3(a.c0.x, a.c1.y, a.c2.z);
            // Stable adjacent swaps retain original index order for equal eigenvalues.
            for (int pass = 0; pass < 2; pass++) for (int i = 0; i < 2 - pass; i++)
                if (values[i] > values[i + 1])
                {
                    double value = values[i]; values[i] = values[i + 1]; values[i + 1] = value;
                    double3 column = vectors[i]; vectors[i] = vectors[i + 1]; vectors[i + 1] = column;
                }
            vectors.c0 = CanonicalSign(vectors.c0); vectors.c1 = CanonicalSign(vectors.c1);
            vectors.c2 = math.cross(vectors.c0, vectors.c1);
        }

        static double3 CanonicalSign(double3 column)
        {
            int largest = 0;
            if (math.abs(column.y) > math.abs(column[largest])) largest = 1;
            if (math.abs(column.z) > math.abs(column[largest])) largest = 2;
            return column[largest] < 0 ? -column : column;
        }

        static void Rotate(ref double3x3 a, ref double3x3 vectors, int p, int q)
        {
            double off = a[q][p];
            if (off == 0) return;
            double pp = a[p][p], qq = a[q][q];
            double angle = 0.5 * math.atan2(2 * off, qq - pp);
            math.sincos(angle, out double s, out double c);
            for (int k = 0; k < 3; k++)
            {
                if (k == p || k == q) continue;
                double kp = a[p][k], kq = a[q][k];
                a[p][k] = a[k][p] = c * kp - s * kq;
                a[q][k] = a[k][q] = s * kp + c * kq;
            }
            a[p][p] = c * c * pp - 2 * s * c * off + s * s * qq;
            a[q][q] = s * s * pp + 2 * s * c * off + c * c * qq;
            a[p][q] = a[q][p] = 0;
            double3 vp = vectors[p], vq = vectors[q];
            vectors[p] = c * vp - s * vq; vectors[q] = s * vp + c * vq;
        }
    }
}
