using Game.Galaxy.Data;
using Unity.Mathematics;

namespace Game.Galaxy.Travel
{
    /// <summary>SPEC-32 §4.4 target star-system-frame arrival; yaw forward is (sin yaw, 0, cos yaw).</summary>
    public struct ArrivalPlan
    {
        public StarSystemId StarSystem;
        public double3 PositionM;
        public float YawRad;
        public ulong GateKey;
        public double3 GatePositionM;
        public float GateRadiusM;
        public byte Adjusted;
        public byte Obstructed;
    }
}
