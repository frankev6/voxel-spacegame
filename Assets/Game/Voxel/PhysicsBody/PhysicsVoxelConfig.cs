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
    public struct PhysicsVoxelConfig : Unity.Entities.IComponentData
    {
        public int InstallDelayTicks;      // 7; must be > 6 (catch-up cap, architecture §5.1)
        public int InstallBucketTicks;     // 4; 1..8
        public int MaxBricksPerBatch;      // 256; non-urgent bricks per batch
        public int MaxAssembliesPerBatch;  // 32; non-urgent grid compounds per batch
        public int MaxBoxesPerBrick;       // 64; 1..4096
        public int MaxBatchAgeFrames;      // 2; 1..2 (must stay below DeferredDisposal's 3-frame delay)
        public float BevelFraction;        // 0.05; box bevel radius = BevelFraction × CellSize; 0..0.25
        public float GridLinearDamping;    // 0.0
        public float GridAngularDamping;   // 0.05
        public static PhysicsVoxelConfig Default => new PhysicsVoxelConfig {
            InstallDelayTicks = 7, InstallBucketTicks = 4, MaxBricksPerBatch = 256, MaxAssembliesPerBatch = 32,
            MaxBoxesPerBrick = 64, MaxBatchAgeFrames = 2, BevelFraction = 0.05f, GridLinearDamping = 0f, GridAngularDamping = 0.05f };
    }
}
