using Unity.Entities;

namespace Game.Voxel.Grid
{
    /// <summary>Pending rebuild read, independent of an unfinished current spawn's read (ADR-0026).</summary>
    internal struct RebuildReadToken : IComponentData { public int Value; }
}
