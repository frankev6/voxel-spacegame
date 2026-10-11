using Unity.Entities;

namespace Game.Voxel.Damage
{
    /// <summary>Per-grid counters. Writer: DamageResolveSystem. On every grid root (SPEC-10 adds it via DamageSetup).</summary>
    public struct GridDamageStats : IComponentData
    {
        public uint LastDamageTick;
        public Entity LastDamageSource;   // Source of the last event of the latest damaging tick (resolver order)
        public Entity LastDamageInstigator; // Instigator of that same event (review X6); SPEC-33 provocation and SPEC-21 kill credit read it
        public int CellsDestroyed;     // cumulative, reason Destroyed
        public int CellsDetached;      // cumulative, reasons DetachedParticle/DetachedBody
        public int InstancesDestroyed; // cumulative
        public float DamageTaken;      // cumulative effective damage applied to cells
    }
}
