using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Commands
{
    /// <summary>Per-ship control input (architecture §6.4). On every ship root (FlightSetup).
    /// Writers: PlayerCommandApplySystem for the PlayerShip; SPEC-23 AI steering for AI ships. Implements SPEC-20 §4.1.</summary>
    public struct ShipCommand : IComponentData
    {
        public float2 Thrust;            // x = strafe (+ right), y = forward (+) / reverse (−); each −1..1
        public float Turn;               // −1..1; + = turn right (decision 12)
        public float3 AimWorld;          // local-frame aim point for weapons (SPEC-21)
        public byte AimValid;            // 1 if AimWorld is meaningful this tick
        public ShipCommandFlags Flags;
        public ShipCommandEdges Edges;   // valid only during the tick it was written (decision 8)
        public uint FireGroups;          // bit g = fire group g held
    }
}
