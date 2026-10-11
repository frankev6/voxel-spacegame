using Unity.Mathematics;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §5.5. Solid test used by VoxelRaycast.</summary>
    public interface ICellSolidQuery { bool IsSolid(int3 cell); bool BrickExists(int3 brick); }
}
