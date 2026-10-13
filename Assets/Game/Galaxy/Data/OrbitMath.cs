using Game.Data.Definitions;
using Unity.Mathematics;

namespace Game.Galaxy.Data
{
    /// <summary>Implements SPEC-30 §4.5.</summary>
    public static class OrbitMath   // Burst-compatible static methods
    {
        public static double3 LocalPosition(in OrbitElements o, double t) => throw new System.NotImplementedException("GAL-002");   // relative to parent, Y = 0
        public static double3 LocalVelocity(in OrbitElements o, double t) => throw new System.NotImplementedException("GAL-002");
        public static double3 Position(in BodySpec b, double t) => throw new System.NotImplementedException("GAL-002");             // star-system frame: parent + local
        public static double3 Velocity(in BodySpec b, double t) => throw new System.NotImplementedException("GAL-002");
        // TODO(GAL-002): implement the behavior specified by SPEC-30.
    }
}
