using Game.Data.Validation;

namespace Game.Data.Definitions
{
    /// <summary>Implements SPEC-12 §4.5.</summary>
    [ContentIdPrefix("config_")]
    [UnityEngine.CreateAssetMenu(menuName = "Game/Content/Physics Config", fileName = "config_physics")]
    public sealed class PhysicsVoxelConfigDefinition : ContentDefinition      // single instance: config_physics
    {
        [UnityEngine.SerializeField, UnityEngine.Range(7, 30)] private int installDelayTicks = 7;
        [UnityEngine.SerializeField, UnityEngine.Range(1, 8)] private int installBucketTicks = 4;
        [UnityEngine.SerializeField, UnityEngine.Range(16, 4096)] private int maxBricksPerBatch = 256;
        [UnityEngine.SerializeField, UnityEngine.Range(1, 256)] private int maxAssembliesPerBatch = 32;
        [UnityEngine.SerializeField, UnityEngine.Range(1, 4096)] private int maxBoxesPerBrick = 64;
        [UnityEngine.SerializeField, UnityEngine.Range(1, 2)] private int maxBatchAgeFrames = 2;
        [UnityEngine.SerializeField, UnityEngine.Range(0f, 0.25f)] private float bevelFraction = 0.05f;
        [UnityEngine.SerializeField, UnityEngine.Min(0f)] private float gridLinearDamping = 0f;
        [UnityEngine.SerializeField, UnityEngine.Min(0f)] private float gridAngularDamping = 0.05f;
        [UnityEngine.SerializeField, UnityEngine.Range(2, 8)] private int solverIterations = 4;
        public int InstallDelayTicks => installDelayTicks;
        public int InstallBucketTicks => installBucketTicks;
        public int MaxBricksPerBatch => maxBricksPerBatch;
        public int MaxAssembliesPerBatch => maxAssembliesPerBatch;
        public int MaxBoxesPerBrick => maxBoxesPerBrick;
        public int MaxBatchAgeFrames => maxBatchAgeFrames;
        public float BevelFraction => bevelFraction;
        public float GridLinearDamping => gridLinearDamping;
        public float GridAngularDamping => gridAngularDamping;
        public int SolverIterations => solverIterations;
        public override void Validate(ValidationScope scope, ContentValidationContext context)
        {
            base.Validate(scope, context);
            if (installDelayTicks <= 6)
                scope.Error("PHY-E001", nameof(installDelayTicks), "Install delay must exceed the six-tick catch-up cap.");
            if (bevelFraction > 0.25f)
                scope.Error("PHY-E002", nameof(bevelFraction), "Bevel fraction must not exceed 0.25 of a cell.");
        }
    }
}
