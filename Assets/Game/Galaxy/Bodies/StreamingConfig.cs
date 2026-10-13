using Unity.Entities;
using Game.Galaxy.Data;
using Game.Data.Definitions;

namespace Game.Galaxy.Bodies
{
    /// <summary>Implements SPEC-31 §4.2. StreamingConfig contract. Created by BootSingletons from config_streaming; never changed at runtime.</summary>
    public struct StreamingConfig : IComponentData
    {
        public float T2UnloadFactor;
        public float BubbleRadiusM;
        public float BubbleUnloadFactor;
        public int MaxGridSpawnsPerFrame;
        public int MaxAsteroidJobsInFlight;
        public int MaxAsteroidsPerSector;
        public float MaxRenderDistanceM;
        public float InteractionQueryRadiusM;
        public int PersistDamageMaxCells;
    }
}
