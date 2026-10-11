using UnityEngine;

namespace Game.Data.Definitions
{
    /// <summary>SPEC-13 §4.5 authoring values. Game.App sets DamageConfig at boot (DMG-003).
    /// The native table and cue hashes are runtime projections, not serialized authoring fields.</summary>
    [ContentIdPrefix("config_")]
    [CreateAssetMenu(menuName = "Game/Content/Damage Config", fileName = "config_damage")]
    public sealed class DamageConfigDefinition : ContentDefinition
    {
        [SerializeField] private float entryBacktrackCells = 0.5f;
        [SerializeField] private int maxDdaCells = 256;
        [SerializeField] private float maxBlastRadiusCells = 12f;
        [SerializeField] private float maxOcclusionRadiusCells = 8f;
        [SerializeField] private float occlusionFactor = 1.0f;
        [SerializeField] private float instanceDestroyCellFraction = 0.5f;
        [SerializeField] private float kickDetach = 1.5f;
        [SerializeField] private float kickDissolve = 2f;
        [SerializeField] private float kickRandomFraction = 0.4f;
        [SerializeField] private float minRamEnergyJ = 20000f;
        [SerializeField] private float ramHpPerJoule = 0.001f;
        [SerializeField] private float ramRestitutionCorrection = 1.21f;
        [SerializeField] private float ramRadiusBaseCells = 0.75f;
        [SerializeField] private float ramRadiusCellsPerSqrtMJ = 1.5f;
        [SerializeField] private float ramRadiusMaxCells = 4f;
        [SerializeField] private int ramPairCooldownTicks = 6;
        [SerializeField] private int maxRestoreCellsPerTick = 4096;
        [SerializeField] private int integrityNodeBudgetPerGrid = 131072;
        [SerializeField] private int maxIntegrityGridsPerTick = 64;
        [SerializeField] private int maxSeedsPerGrid = 8192;
        [SerializeField] private int integrityForceAfterDeferrals = 4;
        [SerializeField] private int brickWarmupPerTick = 64;
        [SerializeField] private int particleMaxCells = 8;
        [SerializeField] private int chunkMaxCells = 500;
        [SerializeField] private int chunkMaxExtentCells = 16;
        [SerializeField] private int maxLiveChunks = 64;
        [SerializeField] private int splitCellBudgetPerTick = 16384;
        [SerializeField] private int maxNewHulksPerTick = 4;
        [SerializeField] private int maxNewChunksPerTick = 8;
        [SerializeField] private int splitGraceTicks = 60;
        [SerializeField] private int maxDebrisParticlesPerTick = 2000;
        [SerializeField] private int chunkMaxBoxes = 32;
        [SerializeField] private float chunkLifetimeMinSeconds = 10f;
        [SerializeField] private float chunkLifetimeMaxSeconds = 30f;
        [SerializeField] private float chunkDissolveSeconds = 1.5f;
        [SerializeField] private float hulkSeparationSpeed = 0.75f;
        [SerializeField] private float chunkSeparationSpeed = 1.5f;
        [SerializeField] private float chunkTumbleMin = 0.5f;
        [SerializeField] private float chunkTumbleMax = 1.5f;
        [SerializeField] private float coreBreachRadiusCells = 3f;
        [SerializeField] private float coreBreachDamage = 150f;
        [SerializeField] private int dissolveParticlesPerTick = 1500;
        [SerializeField] private int dissolveParticleCap = 12000;
        [SerializeField] private AudioCueDefinition ramImpactCue, cellBreakCue, splitCue, coreLostCue;
        public float EntryBacktrackCells => entryBacktrackCells;
        public int MaxDdaCells => maxDdaCells;
        public float MaxBlastRadiusCells => maxBlastRadiusCells;
        public float MaxOcclusionRadiusCells => maxOcclusionRadiusCells;
        public float OcclusionFactor => occlusionFactor;
        public float InstanceDestroyCellFraction => instanceDestroyCellFraction;
        public float KickDetach => kickDetach;
        public float KickDissolve => kickDissolve;
        public float KickRandomFraction => kickRandomFraction;
        public float MinRamEnergyJ => minRamEnergyJ;
        public float RamHpPerJoule => ramHpPerJoule;
        public float RamRestitutionCorrection => ramRestitutionCorrection;
        public float RamRadiusBaseCells => ramRadiusBaseCells;
        public float RamRadiusCellsPerSqrtMJ => ramRadiusCellsPerSqrtMJ;
        public float RamRadiusMaxCells => ramRadiusMaxCells;
        public int RamPairCooldownTicks => ramPairCooldownTicks;
        public int MaxRestoreCellsPerTick => maxRestoreCellsPerTick;
        public int IntegrityNodeBudgetPerGrid => integrityNodeBudgetPerGrid;
        public int MaxIntegrityGridsPerTick => maxIntegrityGridsPerTick;
        public int MaxSeedsPerGrid => maxSeedsPerGrid;
        public int IntegrityForceAfterDeferrals => integrityForceAfterDeferrals;
        public int BrickWarmupPerTick => brickWarmupPerTick;
        public int ParticleMaxCells => particleMaxCells;
        public int ChunkMaxCells => chunkMaxCells;
        public int ChunkMaxExtentCells => chunkMaxExtentCells;
        public int MaxLiveChunks => maxLiveChunks;
        public int SplitCellBudgetPerTick => splitCellBudgetPerTick;
        public int MaxNewHulksPerTick => maxNewHulksPerTick;
        public int MaxNewChunksPerTick => maxNewChunksPerTick;
        public int SplitGraceTicks => splitGraceTicks;
        public int MaxDebrisParticlesPerTick => maxDebrisParticlesPerTick;
        public int ChunkMaxBoxes => chunkMaxBoxes;
        public float ChunkLifetimeMinSeconds => chunkLifetimeMinSeconds;
        public float ChunkLifetimeMaxSeconds => chunkLifetimeMaxSeconds;
        public float ChunkDissolveSeconds => chunkDissolveSeconds;
        public float HulkSeparationSpeed => hulkSeparationSpeed;
        public float ChunkSeparationSpeed => chunkSeparationSpeed;
        public float ChunkTumbleMin => chunkTumbleMin;
        public float ChunkTumbleMax => chunkTumbleMax;
        public float CoreBreachRadiusCells => coreBreachRadiusCells;
        public float CoreBreachDamage => coreBreachDamage;
        public int DissolveParticlesPerTick => dissolveParticlesPerTick;
        public int DissolveParticleCap => dissolveParticleCap;
        public AudioCueDefinition RamImpactCue => ramImpactCue;
        public AudioCueDefinition CellBreakCue => cellBreakCue;
        public AudioCueDefinition SplitCue => splitCue;
        public AudioCueDefinition CoreLostCue => coreLostCue;
    }
}
