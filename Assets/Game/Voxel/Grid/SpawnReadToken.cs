using Unity.Entities;

namespace Game.Voxel.Grid
{
    /// <summary>ADR-0024: registry-owned read lease following a request/root until its final spawn batch.</summary>
    internal struct SpawnReadToken : IComponentData { public int Value; }
}
