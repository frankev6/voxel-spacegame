using System;
using Game.Data.Definitions;

namespace Game.Galaxy.Bodies
{
    /// <summary>SPEC-31 SYS-002: copies the validated config_streaming definition into its boot singleton value.</summary>
    public static class StreamingConfigFactory
    {
        public static StreamingConfig Create(StreamingConfigDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            return new StreamingConfig
            {
                T2UnloadFactor = definition.T2UnloadFactor,
                BubbleRadiusM = definition.BubbleRadiusM,
                BubbleUnloadFactor = definition.BubbleUnloadFactor,
                MaxGridSpawnsPerFrame = definition.MaxGridSpawnsPerFrame,
                MaxAsteroidJobsInFlight = definition.MaxAsteroidJobsInFlight,
                MaxAsteroidsPerSector = definition.MaxAsteroidsPerSector,
                MaxRenderDistanceM = definition.MaxRenderDistanceM,
                InteractionQueryRadiusM = definition.InteractionQueryRadiusM,
                PersistDamageMaxCells = definition.PersistDamageMaxCells,
            };
        }
    }
}
