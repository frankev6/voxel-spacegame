using Unity.Entities;

namespace Game.Voxel.Damage
{
    /// <summary>ADR-0117: a grid becoming a hulk. ShipDeathSystem decides it on the authority (core loss); a co-op guest receives it from
    /// the host. <see cref="HulkTransitionSystem"/> applies it the same way on every machine: hulk components, kind, and for a core loss
    /// the breach blast and sound at <see cref="HulkInfo.CoreLocal"/>.</summary>
    public struct HulkTransition
    {
        public Entity Grid;
        public HulkInfo Info;
        public byte Blast;   // 1: the core breach blast and sound (a core loss as it happens; not a join or a split piece)
    }

    /// <summary>The one place a hulk root gets its hulk components, for split pieces (RecordHulkJob) and transitions alike.</summary>
    public static class HulkSetup
    {
        public static void Add(ref EntityCommandBuffer ecb, Entity root, in HulkInfo info)
        {
            ecb.AddComponent<Hulk>(root); ecb.AddComponent(root, info);
        }
    }
}
