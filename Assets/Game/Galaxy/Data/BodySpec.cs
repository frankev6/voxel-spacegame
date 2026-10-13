using Game.Data.Definitions;
using Unity.Mathematics;

namespace Game.Galaxy.Data
{
    /// <summary>Implements SPEC-30 §4.5.</summary>
    public struct BodySpec
    {
        public BodyId Id;
        public ulong ParentKey;         // 0 = star-system root; Moon: LocalKey of its planet; StorySpawn: anchor key or 0
        public ushort TemplateIndex;    // GalaxyDef.BodyTemplates (after replacements)
        public ushort LaneIndex;        // LaneGate: index into GalaxyDef.Lanes; else NoIndex
        public OrbitElements Orbit;
        public OrbitElements ParentOrbit; // copy of the parent's orbit (Moon/StorySpawn with parent), Mode Fixed + Radius 0 otherwise
        public float RadiusM;           // celestial radius or voxel-body bounding radius
        public float YawRad;
        public float BeltInnerRadiusM, BeltOuterRadiusM;  // Belt only (ring around the star), else 0
        public ulong Seed;              // StableHash.Combine(StarSystemDef.Seed, Id.LocalKey)
        public BodyStateFlags State;
    }
}
