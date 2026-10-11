using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Flight
{
    /// <summary>Static nozzle geometry, grid space. Only writer: ThrusterInitSystem (once per new or re-parented thruster). Implements SPEC-20 §4.3.</summary>
    public struct ThrusterVisual : IComponentData
    {
        public float3 NozzleLocal;       // grid-local metres: centre of the exhaust face
        public float3 ExhaustDirLocal;   // grid-local unit vector (= −force direction, full 3D)
        public float NozzleRadius;       // metres
    }
}
