using Unity.Mathematics;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §5.5.</summary>
    public struct VoxelRayHit { public int3 Cell; public byte EnteredFace; /* BlockFace, 255 = started inside */ public float DistanceCells; }
}
