using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Commands
{
    /// <summary>Implements SPEC-20 §4.1.</summary>
    // FaceAim, FaceTarget: ADR-0123 experimental 3D flight; the nose follows the aim direction or the selected target.
    [System.Flags] public enum ShipCommandFlags : byte { None = 0, Boost = 1, Brake = 2, AimAssist = 4, FaceAim = 8, FaceTarget = 16 }   // add at the end only
}
