using Unity.Entities;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>ADR-0063: pooled chunks arm parent contacts once clear after their minimum grace interval.</summary>
    internal struct ChunkContactState : IComponentData
    {
        public byte Armed;
    }
}
