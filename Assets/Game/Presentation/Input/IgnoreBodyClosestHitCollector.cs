using Unity.Entities;
using Unity.Physics;

namespace Game.Presentation.Input
{
    /// <summary>Closest picking hit excluding the player's own rigid body (SPEC-03 §4.4).</summary>
    public struct IgnoreBodyClosestHitCollector : ICollector<RaycastHit>
    {
        readonly Entity ignore;
        public bool EarlyOutOnFirstHit => false;
        public float MaxFraction { get; private set; }
        public int NumHits { get; private set; }
        public RaycastHit ClosestHit;
        public IgnoreBodyClosestHitCollector(Entity ignore, float maxFraction)
        { this.ignore = ignore; MaxFraction = maxFraction; NumHits = 0; ClosestHit = default; }
        public bool AddHit(RaycastHit hit)
        {
            if (hit.Entity == ignore || hit.Fraction > MaxFraction) return false;
            ClosestHit = hit; MaxFraction = hit.Fraction; NumHits = 1; return true;
        }
    }
}
