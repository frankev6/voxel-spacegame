using Unity.Entities;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.4. On an existing root.</summary>
    public struct GridRebuildRequest : IComponentData { public int StructureHandle; public GridSpawnFlags Flags; }
}
