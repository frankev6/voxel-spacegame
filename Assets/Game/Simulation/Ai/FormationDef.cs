using Game.Core;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Ai
{
    /// <summary>Immutable catalog-indexed follower offsets in the boot-owned AI blob (SPEC-23 AI-013, Q14).</summary>
    public struct FormationDef
    {
        public ContentHash IdHash;
        public BlobArray<float2> Slots; // leader-local metres: x right, y forward; FleetMember.Slot indexes this array
    }
}
