using UnityEngine;

namespace Game.Data.Definitions
{

    /// <summary>Implements SPEC-10 §4.7. Single instance: config_voxel.</summary>
    [ContentIdPrefix("config_")]
    public sealed class VoxelConfigDefinition : ContentDefinition
    {
        [SerializeField, Min(64)] private int spawnBricksPerFrame = 1024;
        [SerializeField, Min(0.05f)] private float defaultCellSize = 0.5f;
        public int SpawnBricksPerFrame => spawnBricksPerFrame; public float DefaultCellSize => defaultCellSize;
    }
}
