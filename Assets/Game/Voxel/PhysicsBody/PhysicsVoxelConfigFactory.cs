using Game.Data.Definitions;
using Unity.Mathematics;
using Unity.Physics;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>Copies validated content to boot configuration (SPEC-12 §4.5, §5.13).</summary>
    public static class PhysicsVoxelConfigFactory
    {
        public static PhysicsVoxelConfig Create(PhysicsVoxelConfigDefinition def) => new PhysicsVoxelConfig
        {
            InstallDelayTicks = def.InstallDelayTicks,
            InstallBucketTicks = def.InstallBucketTicks,
            MaxBricksPerBatch = def.MaxBricksPerBatch,
            MaxAssembliesPerBatch = def.MaxAssembliesPerBatch,
            MaxBoxesPerBrick = def.MaxBoxesPerBrick,
            MaxBatchAgeFrames = def.MaxBatchAgeFrames,
            BevelFraction = def.BevelFraction,
            GridLinearDamping = def.GridLinearDamping,
            GridAngularDamping = def.GridAngularDamping,
        };

        public static PhysicsStep CreatePhysicsStep(PhysicsVoxelConfigDefinition def)
        {
            var step = PhysicsStep.Default;
            step.SimulationType = SimulationType.UnityPhysics;
            step.Gravity = float3.zero;
            step.SolverIterationCount = def.SolverIterations;
            step.MultiThreaded = 1;
            step.SynchronizeCollisionWorld = 1;
            step.CollisionTolerance = 0.1f;
            step.SolverStabilizationHeuristicSettings.EnableSolverStabilization = false;
            step.IncrementalDynamicBroadphase = false;
            step.IncrementalStaticBroadphase = true;
            return step;
        }
    }
}
