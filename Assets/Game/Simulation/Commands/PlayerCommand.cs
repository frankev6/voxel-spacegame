using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Commands
{
    /// <summary>Singleton written once per frame by PlayerInputSampleSystem (SPEC-03), with the dev-only replay override of ADR-0051. Implements SPEC-20 §4.1.</summary>
    public struct PlayerCommand : IComponentData
    {
        public float2 Thrust;
        public float Turn;
        public float3 AimWorld;
        public byte AimValid;
        public ShipCommandFlags Flags;
        public uint FireGroups;
        public PlayerEdgeCounters Edges;
        public uint FrameIndex;          // UnityEngine.Time.frameCount of the sample (diagnostics, replay)
    }
}
