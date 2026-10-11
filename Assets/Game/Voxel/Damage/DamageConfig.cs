using Game.Data.Combat;
using Unity.Entities;

namespace Game.Voxel.Damage
{
    /// <summary>Singleton. Created with Default by DamageStreamsSystem.OnCreate; overwritten once at boot by Game.App from the definition.</summary>
    public struct DamageConfig : IComponentData
    {
        // Per-type and per-armour values: the content table (decision 19, §4.7)
        public BlobAssetReference<DamageTableBlob> Table;
        // Point and blast
        public float EntryBacktrackCells;       // 0.5
        public int   MaxDdaCells;               // 256
        public float MaxBlastRadiusCells;       // 12
        public float MaxOcclusionRadiusCells;   // 8 (blasts above this radius skip occlusion)
        public float OcclusionFactor;           // 1.0 (0 disables occlusion)
        public float InstanceDestroyCellFraction; // 0.5
        // Kicks (m/s)
        public float KickDetach;                // 1.5
        public float KickDissolve;              // 2
        public float KickRandomFraction;        // 0.4
        // Rams
        public float MinRamEnergyJ;             // 20000
        public float RamHpPerJoule;             // 0.001
        public float RamRestitutionCorrection;  // 1.21  ((1 + e)², e = 0.1)
        public float RamRadiusBaseCells;        // 0.75
        public float RamRadiusCellsPerSqrtMJ;   // 1.5
        public float RamRadiusMaxCells;         // 4
        public int   RamPairCooldownTicks;      // 6
        // Restores (ADR-0016)
        public int   MaxRestoreCellsPerTick;    // 4096
        // Integrity
        public int IntegrityNodeBudgetPerGrid;  // 131072
        public int MaxIntegrityGridsPerTick;    // 64
        public int MaxSeedsPerGrid;             // 8192
        public int IntegrityForceAfterDeferrals;// 4
        public int BrickWarmupPerTick;          // 64
        // Split
        public int ParticleMaxCells;            // 8
        public int ChunkMaxCells;               // 500
        public int ChunkMaxExtentCells;         // 16
        public int MaxLiveChunks;               // 64
        public int SplitCellBudgetPerTick;      // 16384
        public int MaxNewHulksPerTick;          // 4
        public int MaxNewChunksPerTick;         // 8
        public int SplitGraceTicks;             // 60
        public int MaxDebrisParticlesPerTick;   // 2000
        public int ChunkMaxBoxes;               // 32
        public float ChunkLifetimeMinSeconds;   // 10
        public float ChunkLifetimeMaxSeconds;   // 30
        public float ChunkDissolveSeconds;      // 1.5
        public float HulkSeparationSpeed;       // 0.75
        public float ChunkSeparationSpeed;      // 1.5
        public float ChunkTumbleMin;            // 0.5 rad/s
        public float ChunkTumbleMax;            // 1.5 rad/s
        // Death and despawn
        public float CoreBreachRadiusCells;     // 3
        public float CoreBreachDamage;          // 150
        public int   DissolveParticlesPerTick;  // 1500
        public int   DissolveParticleCap;       // 12000
        // Audio cues (Game.Core.ContentHash of AudioCueDefinition IDs; default = 0 = no sound)
        public Game.Core.ContentHash CueRamImpact, CueCellBreak, CueSplit, CueCoreLost;

        public static DamageConfig Default => new DamageConfig
        {
            EntryBacktrackCells = 0.5f,
            MaxDdaCells = 256,
            MaxBlastRadiusCells = 12f,
            MaxOcclusionRadiusCells = 8f,
            OcclusionFactor = 1.0f,
            InstanceDestroyCellFraction = 0.5f,
            KickDetach = 1.5f,
            KickDissolve = 2f,
            KickRandomFraction = 0.4f,
            MinRamEnergyJ = 20000f,
            RamHpPerJoule = 0.001f,
            RamRestitutionCorrection = 1.21f,
            RamRadiusBaseCells = 0.75f,
            RamRadiusCellsPerSqrtMJ = 1.5f,
            RamRadiusMaxCells = 4f,
            RamPairCooldownTicks = 6,
            MaxRestoreCellsPerTick = 4096,
            IntegrityNodeBudgetPerGrid = 131072,
            MaxIntegrityGridsPerTick = 64,
            MaxSeedsPerGrid = 8192,
            IntegrityForceAfterDeferrals = 4,
            BrickWarmupPerTick = 64,
            ParticleMaxCells = 8,
            ChunkMaxCells = 500,
            ChunkMaxExtentCells = 16,
            MaxLiveChunks = 64,
            SplitCellBudgetPerTick = 16384,
            MaxNewHulksPerTick = 4,
            MaxNewChunksPerTick = 8,
            SplitGraceTicks = 60,
            MaxDebrisParticlesPerTick = 2000,
            ChunkMaxBoxes = 32,
            ChunkLifetimeMinSeconds = 10f,
            ChunkLifetimeMaxSeconds = 30f,
            ChunkDissolveSeconds = 1.5f,
            HulkSeparationSpeed = 0.75f,
            ChunkSeparationSpeed = 1.5f,
            ChunkTumbleMin = 0.5f,
            ChunkTumbleMax = 1.5f,
            CoreBreachRadiusCells = 3f,
            CoreBreachDamage = 150f,
            DissolveParticlesPerTick = 1500,
            DissolveParticleCap = 12000
        };   // exactly the values in the comments above; Table not created (Multiplier → 1, kicks 0) until boot sets it
        public float Multiplier(DamageType type, byte armorClass)
        {
            if (!Table.IsCreated) return 1f;
            ref var table = ref Table.Value;
            int index = (byte)type;
            if (index >= table.Types.Length || table.ArmorClassCount <= 0) return 1f;
            int armor = armorClass < table.ArmorClassCount ? armorClass : 0;
            return table.ArmorMultipliers[table.Types[index].ArmorOffset + armor];
        }  // Table lookup (decision 19); unknown type → 1.0, armour class out of range → class 0
        public static DamageConfig FromDefinition(Game.Data.Definitions.DamageConfigDefinition def) { throw new System.NotImplementedException("DMG-003"); } // cue hashes via ContentId.Hash
    }
}
