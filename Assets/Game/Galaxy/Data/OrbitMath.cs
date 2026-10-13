using Game.Data.Definitions;
using Unity.Mathematics;

namespace Game.Galaxy.Data
{
    /// <summary>Pure, Burst-compatible orbit evaluation in the XZ plane. Implements SPEC-30 §4.5 and §5.5.5.</summary>
    public static class OrbitMath
    {
        const double Tau = 2.0 * math.PI;

        public static double3 LocalPosition(in OrbitElements o, double t)
        {
            if (o.Mode == OrbitMode.Fixed)
                return new double3(o.RadiusM * math.cos(o.PhaseRad), 0, o.RadiusM * math.sin(o.PhaseRad));
            double anomaly = MeanAnomaly(o, t);
            if (o.Mode == OrbitMode.Circular)
                return new double3(o.RadiusM * math.cos(anomaly), 0, o.RadiusM * math.sin(anomaly));
            double e = o.Eccentricity;
            double eccentricAnomaly = EccentricAnomaly(anomaly, e);
            var position = new double3(o.RadiusM * (math.cos(eccentricAnomaly) - e), 0,
                o.RadiusM * math.sqrt(1.0 - e * e) * math.sin(eccentricAnomaly));
            return RotatePeriapsis(position, o.ArgPeriapsisRad);
        }

        public static double3 LocalVelocity(in OrbitElements o, double t)
        {
            if (o.Mode == OrbitMode.Fixed) return double3.zero;
            double anomaly = MeanAnomaly(o, t);
            double meanMotion = Tau / o.PeriodS;
            if (o.Mode == OrbitMode.Circular)
                return o.RadiusM * meanMotion * new double3(-math.sin(anomaly), 0, math.cos(anomaly));
            double e = o.Eccentricity;
            double eccentricAnomaly = EccentricAnomaly(anomaly, e);
            // dE/dt = (2π/T) / (1 - e cos E), from differentiating Kepler's equation.
            double eccentricMotion = meanMotion / (1.0 - e * math.cos(eccentricAnomaly));
            var velocity = o.RadiusM * eccentricMotion * new double3(-math.sin(eccentricAnomaly), 0,
                math.sqrt(1.0 - e * e) * math.cos(eccentricAnomaly));
            return RotatePeriapsis(velocity, o.ArgPeriapsisRad);
        }

        public static double3 Position(in BodySpec b, double t)
            => LocalPosition(b.ParentOrbit, t) + LocalPosition(b.Orbit, t);

        public static double3 Velocity(in BodySpec b, double t)
            => LocalVelocity(b.ParentOrbit, t) + LocalVelocity(b.Orbit, t);

        static double MeanAnomaly(in OrbitElements o, double t)
        {
            double periods = t / o.PeriodS;
            return o.PhaseRad + Tau * (periods - math.floor(periods));
        }

        static double EccentricAnomaly(double meanAnomaly, double eccentricity)
        {
            double result = meanAnomaly;
            // A fixed count is part of the generator-version contract; never use convergence-based termination.
            for (int i = 0; i < 6; i++)
                result -= (result - eccentricity * math.sin(result) - meanAnomaly) / (1.0 - eccentricity * math.cos(result));
            return result;
        }

        static double3 RotatePeriapsis(double3 vector, double angle)
        {
            double c = math.cos(angle), s = math.sin(angle);
            // Unity's positive rotation about Y: +X turns toward -Z. Keep all math in double precision.
            return new double3(c * vector.x + s * vector.z, 0, -s * vector.x + c * vector.z);
        }
    }
}
