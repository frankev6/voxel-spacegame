using Game.Data.Definitions;
using Game.Voxel.Model;

namespace Game.Voxel.Grid
{

    /// <summary>Implements SPEC-10 §4.7. Copies the definition's values into the ECS singleton and sets RequireRenderer = 1.</summary>
    public static class VoxelConfigFactory
    {
        public static VoxelConfig Create(VoxelConfigDefinition def) => new VoxelConfig
        {
            SpawnBricksPerFrame = def.SpawnBricksPerFrame,
            DefaultCellSize = def.DefaultCellSize,
            RequireRenderer = 1
        };
    }
}
