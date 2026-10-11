using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Game.Voxel.Model;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>Implements SPEC-12 §4 (physics contract).</summary>
    public static class PhysicsVoxelConfigFactory
    {
        public static PhysicsVoxelConfig Create(Game.Data.Definitions.PhysicsVoxelConfigDefinition def) => throw new System.NotImplementedException("PHY-003");   // copies the fields
        public static Unity.Physics.PhysicsStep CreatePhysicsStep(Game.Data.Definitions.PhysicsVoxelConfigDefinition def) => throw new System.NotImplementedException("PHY-003"); // §5.13
    }
}
