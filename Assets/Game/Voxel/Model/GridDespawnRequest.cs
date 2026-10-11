using Unity.Entities;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.4. On every root, disabled; enable to despawn.</summary>
    public struct GridDespawnRequest : IComponentData, IEnableableComponent { }
}
