using System;
using Unity.Entities;
using Unity.Mathematics;
using Game.Simulation.Spatial;

namespace Game.Simulation.Weapons
{
    /// <summary>Implements SPEC-21 weapon contract: CombatHitTest.</summary>
    public struct CombatHitTest   // job field; Create in OnCreate, Update first in OnUpdate
    {
        public static CombatHitTest Create(ref SystemState state) => throw new NotImplementedException();
        public void Update(ref SystemState state) => throw new NotImplementedException();
        /// First hit along p0→p1 (fraction 0..1), ignoring `ignore`. Deterministic: ties broken by lower Entity.Index.
        public CombatHit Segment(float3 p0, float3 p1, float inflate, Entity ignore, in SpatialHash hash, int maxCandidates) => throw new NotImplementedException();
    }
}
