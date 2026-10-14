using Unity.Entities;
using Unity.Physics;
namespace Game.Presentation.Services
{
    /// <summary>Nearest physical obstruction, excluding every collider leaf of the followed grid.</summary>
    internal struct IgnoreEntityClosestHitCollector : ICollector<RaycastHit>
    {
        readonly Entity ignored;
        public bool EarlyOutOnFirstHit => false;
        public float MaxFraction { get; private set; }
        public int NumHits { get; private set; }
        internal RaycastHit ClosestHit { get; private set; }
        internal IgnoreEntityClosestHitCollector(Entity ignored)
        { this.ignored = ignored; MaxFraction = 1; NumHits = 0; ClosestHit = default; }
        public bool AddHit(RaycastHit hit)
        {
            if (hit.Entity == ignored || hit.Fraction > MaxFraction) return false;
            ClosestHit = hit; MaxFraction = hit.Fraction; NumHits = 1; return true;
        }
    }
}
