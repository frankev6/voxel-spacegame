using Unity.Entities;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.4. Enabled by GridSpawnSystem; SPEC-12 consumes.</summary>
    public struct GridRebuilt : IComponentData, IEnableableComponent { }
}
