using System.Collections.Generic;
using Game.ShipEditor.Data;

namespace Game.ShipEditor.Services
{
    /// <summary>SPEC-51 reusable per-group validation result. PlacementValidator fills errors in EDT-007.</summary>
    public sealed class ApplyReport
    {
        readonly List<PlacementError> errors = new();
        public IReadOnlyList<PlacementError> GroupErrors => errors;
        public int AcceptedGroups { get; internal set; }
        public int OpsApplied { get; internal set; }
        public void Clear() { errors.Clear(); AcceptedGroups = OpsApplied = 0; }
        internal void AddGroup(PlacementError error) { errors.Add(error); if (error == PlacementError.None) AcceptedGroups++; }
    }
}
