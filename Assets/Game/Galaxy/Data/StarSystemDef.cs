using Game.Data.Definitions;
using Unity.Mathematics;

namespace Game.Galaxy.Data
{
    /// <summary>One star system in the galaxy frame. Blittable. Implements SPEC-30 §4.4.</summary>
    public struct StarSystemDef
    {
        public StarSystemId Id;
        public Unity.Mathematics.double2 PositionLy;
        public ulong Seed;                 // StableHash.Combine(galaxySeed, Id.Value)
        public ushort TemplateIndex;       // index into GalaxyDef.Templates; NoIndex for authored
        public ushort AuthoredIndex;       // 0 = procedural; else 1 + index into GalaxyDef.Authored
        public ushort StarTemplateIndex;   // index into GalaxyDef.BodyTemplates (rolled or authored star)
        public StarSystemFlags Flags;
        public byte Danger;                // 0..3
        public int FirstAdjacency;         // first index into GalaxyDef.Adjacency
        public ushort LaneCount;           // number of adjacency entries
    }
}
