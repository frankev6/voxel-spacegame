using Game.Galaxy.Data;
using Unity.Mathematics;

namespace Game.Galaxy.Travel
{
    /// <summary>SPEC-32 §4.4 first route lane and its loaded-star-system gate.</summary>
    public struct NextHop
    {
        public StarSystemId Next;
        public int LaneIndex;
        public ulong GateKey;
        public double3 GatePositionM;
    }
}
