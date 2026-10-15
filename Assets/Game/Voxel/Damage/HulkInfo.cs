using Game.Voxel.Model;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Voxel.Damage
{
    /// <summary>Salvage and narrative hook on every hulk root.</summary>
    public struct HulkInfo : IComponentData
    {
        public HulkCause Cause;
        public uint Tick;              // SimClock.Tick when it became a hulk
        public ulong OriginPersistentId; // CoreLost: own id; Split: the parent's id
        public GridKind FormerKind;    // kind before it became a hulk
        public float3 CoreLocal;       // CoreLost: where the lost core was, in the grid's frame (the breach blast's origin)
    }
}
