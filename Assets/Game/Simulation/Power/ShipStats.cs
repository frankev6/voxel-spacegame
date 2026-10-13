using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Power
{
    /// <summary>Implements SPEC-22 §4.2. Shared ship stats on the grid root (architecture §6.4, ADR-0014). Only writer: ShipStatsAggregateSystem.
    /// Feature totals live in feature-owned components (PowerStats, ShieldStats here; SPEC-20 PropulsionStats; SPEC-21 WeaponStats).</summary>
    public struct ShipStats : IComponentData
    {
        // Mass part: recomputed when PhysicsMass changed
        public float Mass;              // kg = 1 / PhysicsMass.InverseMass; 0 until the body exists
        public float InitialMass;       // kg, first non-zero Mass seen; never changes afterwards
        public float MassFraction;      // Mass / InitialMass (1 at spawn), hull-health proxy for AI and HUD
        // Functional part: recomputed when the alive functional set changed
        public int AliveFunctionalCount;
        public float3 BoundsMin, BoundsMax;   // grid space, from VoxelGrid.CellMin/CellMax: CellMin·CellSize .. (CellMax+1)·CellSize
        public float3 BoundsCenter;     // (BoundsMin + BoundsMax) / 2
        public float BoundingRadius;    // length(BoundsMax - BoundsCenter)
        public ulong FunctionalSignature;     // §5.4 step 2
        public uint FunctionalRevision;       // +1 every time FunctionalSignature changes; feature stats and SPEC-24 resync on change
        public ShipStatsFlags Flags;
    }
}
