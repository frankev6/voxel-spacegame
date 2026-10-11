using Unity.Entities;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.5 (architecture §6.3).</summary>
    public struct FunctionalBlock : IComponentData { public Entity Grid; public int InstanceIndex; }
}
