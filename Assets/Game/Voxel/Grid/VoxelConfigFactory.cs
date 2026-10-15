using Game.Data.Definitions;
using Game.Voxel.Model;

namespace Game.Voxel.Grid
{

    /// <summary>Implements SPEC-10 §4.7. Copies the definition's values into the ECS singleton and sets RequireRenderer = 1, except in
    /// a player started with -nographics (headless co-op guests for automated checks), where no renderer ever becomes ready.</summary>
    public static class VoxelConfigFactory
    {
        public static VoxelConfig Create(VoxelConfigDefinition def) => new VoxelConfig
        {
            SpawnBricksPerFrame = def.SpawnBricksPerFrame,
            DefaultCellSize = def.DefaultCellSize,
            RequireRenderer = (byte)(UnityEngine.SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null ? 0 : 1)
        };
    }
}
