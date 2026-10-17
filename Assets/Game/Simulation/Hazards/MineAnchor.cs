using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Hazards
{
    /// <summary>Optional capped spring that holds a mine near its anchor; read by MineAnchorJob on every machine (SPEC-91 §4.4).</summary>
    public struct MineAnchor : IComponentData
    {
        public float2 Point;        // local-frame XZ; shifted by OriginRebaseSystem
        public float Stiffness;     // 1/s²
        public float Damping;       // 1/s
        public float MaxAccel;      // m/s²
    }
}
