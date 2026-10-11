using Game.Core;
using Unity.Entities;

namespace Game.Voxel.PresentationStreams
{
    /// <summary>Singleton created by AudioService.BindTo from AudioConfigDefinition. Producers read it; Value 0 = no cue. Implements SPEC-64 §4.1.</summary>
    public struct CombatAudioCues : IComponentData
    {
        public ContentHash ImpactKinetic, ImpactEnergy, ImpactExplosive, ImpactRam;
        public ContentHash ExplosionSmall, ExplosionLarge, ShieldHit;
        public ContentHash CellBreak, BreakOff, ShipDeath;
    }
}
