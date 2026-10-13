using Game.Galaxy.Data;
using Unity.Mathematics;

namespace Game.Galaxy.Services
{
    /// <summary>One visible star system on the galaxy map (SPEC-30 §4.8, §5.12).</summary>
    public readonly struct StarSystemMapEntry
    {
        public readonly StarSystemId Id;
        public readonly double2 PositionLy;
        public readonly string Name;
        public readonly bool Known, Visited, Current, IsStory, IsRouteTarget;
        public readonly byte Danger;
        public readonly string OwnerColorToken;

        public StarSystemMapEntry(StarSystemId id, double2 positionLy, string name, bool known, bool visited,
            bool current, bool isStory, bool isRouteTarget, byte danger, string ownerColorToken)
        {
            Id = id;
            PositionLy = positionLy;
            Name = name;
            Known = known;
            Visited = visited;
            Current = current;
            IsStory = isStory;
            IsRouteTarget = isRouteTarget;
            Danger = danger;
            OwnerColorToken = ownerColorToken;
        }
    }
}
