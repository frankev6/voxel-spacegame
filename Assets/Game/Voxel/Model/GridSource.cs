using Game.Core;
using Unity.Entities;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.4. On the root.</summary>
    public struct GridSource : IComponentData
    { public int StructureHandle; /* -1 for blueprint spawns */ public ContentHash Blueprint; public uint SyncedRevision; public GridSpawnFlags Flags; }
}
