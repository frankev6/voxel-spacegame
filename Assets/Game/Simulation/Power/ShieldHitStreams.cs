using Unity.Entities;

namespace Game.Simulation.Power
{
    /// <summary>Implements SPEC-22 §4.3. Singleton holding the ShieldHit channel (SPEC-02 §4.12). Created by ShieldSystem.OnCreate (SimEventChannel<ShieldHit>.Create("shield_hits"))
    /// and disposed in its OnDestroy. Taken with SystemAPI.GetSingleton (read-only) by producers and the consumer: no sync points.
    /// Producers (SPEC-21 projectiles, beams, explosions, and any later spec): Hits.BeginWrite(ref state, ProducerKind.X, n) → writer job →
    /// Hits.EndWrite(handle). The single consumer is ShieldSystem (Hits.BeginTick, BeginRead, EndRead).</summary>
    public struct ShieldHitStreams : IComponentData
    {
        public Game.Data.Streams.SimEventChannel<ShieldHit> Hits;
    }
}
