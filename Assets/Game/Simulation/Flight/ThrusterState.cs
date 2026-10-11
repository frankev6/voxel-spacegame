using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Flight
{
    /// <summary>On every thruster functional entity (registered for BlockModuleId.Thruster). Only writer: PropulsionSystem.
    /// Read by SPEC-63 (plumes) and SPEC-64 (engine loop). Implements SPEC-20 §4.3.</summary>
    public struct ThrusterState : IComponentData { public float Intensity; }   // 0..1, smoothed
}
