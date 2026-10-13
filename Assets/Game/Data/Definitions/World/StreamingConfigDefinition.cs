namespace Game.Data.Definitions
{
    /// <summary>Implements SPEC-31 §4.6. Authored streaming defaults; SYS-002 supplies getters, validation and the asset.</summary>
    [ContentIdPrefix("config_")]
    [UnityEngine.CreateAssetMenu(menuName = "Game/Content/World/Streaming Config", fileName = "config_streaming")]
    public sealed class StreamingConfigDefinition : ContentDefinition
    {
        [UnityEngine.SerializeField] private float t2UnloadFactor = 1.25f;
        [UnityEngine.SerializeField] private float bubbleRadiusM = 900f;
        [UnityEngine.SerializeField] private float bubbleUnloadFactor = 1.25f;
        [UnityEngine.SerializeField] private int maxGridSpawnsPerFrame = 2;
        [UnityEngine.SerializeField] private int maxAsteroidJobsInFlight = 4;
        [UnityEngine.SerializeField] private int maxAsteroidsPerSector = 8;
        [UnityEngine.SerializeField] private float maxRenderDistanceM = 25000f;
        [UnityEngine.SerializeField] private float interactionQueryRadiusM = 600f;
        [UnityEngine.SerializeField] private int persistDamageMaxCells = 65536;
        public float T2UnloadFactor => t2UnloadFactor;
        public float BubbleRadiusM => bubbleRadiusM;
        public float BubbleUnloadFactor => bubbleUnloadFactor;
        public int MaxGridSpawnsPerFrame => maxGridSpawnsPerFrame;
        public int MaxAsteroidJobsInFlight => maxAsteroidJobsInFlight;
        public int MaxAsteroidsPerSector => maxAsteroidsPerSector;
        public float MaxRenderDistanceM => maxRenderDistanceM;
        public float InteractionQueryRadiusM => interactionQueryRadiusM;
        public int PersistDamageMaxCells => persistDamageMaxCells;
        // TODO(SYS-002): validation SYS-E001..E003, factory and authored config asset.
        public override void Validate(Game.Data.Validation.ValidationScope scope, Game.Data.Validation.ContentValidationContext context)
            => throw new System.NotImplementedException("SYS-002");
    }
}
