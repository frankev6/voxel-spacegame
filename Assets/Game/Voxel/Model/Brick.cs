using Unity.Entities;
using Unity.Mathematics;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.5. One 16³ brick entity, 28 bytes.</summary>
    public struct Brick : IComponentData
    {
        public Entity Grid;
        public int3 Coord;
        public ushort SolidCount;          // 1..4096 while the entity lives; a writer that reaches 0 destroys the brick (SPEC-13, via ECB)
        public ushort Reserved;
        public uint ChangedRevision;
    }
}
