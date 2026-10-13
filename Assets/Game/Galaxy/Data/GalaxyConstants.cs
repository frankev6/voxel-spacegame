using Game.Data.Definitions;
using Unity.Mathematics;

namespace Game.Galaxy.Data
{
    /// <summary>Implements SPEC-30 §4.2.</summary>
    public static class GalaxyConstants
    {
        public const float OriginRebaseThresholdM = 2000f;   // architecture §9 (binding, not tunable)
        public const int CurrentGeneratorVersion = 1;
        public const ushort NoIndex = 0xFFFF;                 // "no template / no lane" in ushort index fields
        public const int MaxAuthoredStarSystems = 256;
        public const int MaxProceduralStarSystems = 100000;
        public const int MaxBodiesPerStarSystem = 512;
    }
}
