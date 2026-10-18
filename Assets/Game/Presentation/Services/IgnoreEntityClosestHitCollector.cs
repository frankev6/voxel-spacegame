using Unity.Entities;
using Unity.Physics;
namespace Game.Presentation.Services
{
    /// <summary>Nearest physical obstruction, excluding every collider leaf of the followed grid.</summary>
    internal struct IgnoreEntityClosestHitCollector : ICollector<RaycastHit>
    {
        readonly Entity ignored;
        // DEV-023: grids the camera sees through (CameraSeeThrough, GridRenderHidden); unset lookups ignore nothing more
        readonly ComponentLookup<Game.Voxel.Rendering.CameraSeeThrough> seeThrough;
        readonly ComponentLookup<Game.Voxel.Rendering.GridRenderHidden> hidden;
        readonly bool filter;
        public bool EarlyOutOnFirstHit => false;
        public float MaxFraction { get; private set; }
        public int NumHits { get; private set; }
        internal RaycastHit ClosestHit { get; private set; }
        internal IgnoreEntityClosestHitCollector(Entity ignored)
        { this.ignored = ignored; MaxFraction = 1; NumHits = 0; ClosestHit = default; seeThrough = default; hidden = default; filter = false; }
        internal IgnoreEntityClosestHitCollector(Entity ignored, ComponentLookup<Game.Voxel.Rendering.CameraSeeThrough> seeThrough,
            ComponentLookup<Game.Voxel.Rendering.GridRenderHidden> hidden)
        { this.ignored = ignored; MaxFraction = 1; NumHits = 0; ClosestHit = default; this.seeThrough = seeThrough; this.hidden = hidden; filter = true; }
        public bool AddHit(RaycastHit hit)
        {
            if (hit.Entity == ignored || hit.Fraction > MaxFraction) return false;
            if (filter && (seeThrough.HasComponent(hit.Entity) || hidden.HasComponent(hit.Entity))) return false;
            ClosestHit = hit; MaxFraction = hit.Fraction; NumHits = 1; return true;
        }
    }
}
