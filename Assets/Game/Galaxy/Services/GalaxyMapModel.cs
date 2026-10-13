using System.Collections.Generic;

namespace Game.Galaxy.Services
{
    /// <summary>Read-only galaxy map contract for the UI (SPEC-30 §4.8, §5.12).</summary>
    public sealed class GalaxyMapModel
    {
        public GalaxyMapModel(GalaxyService galaxy, ILaneAccess lanes, IStarSystemOwnership ownership)
            => throw new System.NotImplementedException("GAL-011");
        public uint Revision { get; }
        public IReadOnlyList<StarSystemMapEntry> StarSystems { get; }
        public IReadOnlyList<LaneMapEntry> Lanes { get; }

        // TODO(GAL-011): rebuild visible entries when the world/map stamps change.
        public void Refresh() => throw new System.NotImplementedException("GAL-011");
    }
}
