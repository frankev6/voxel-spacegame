using System;
using Game.App.Session;
using Game.ShipEditor.Services;
using Game.Voxel.Model;

namespace Game.App.EditorHost
{
    /// <summary>Transfers an editor clone to the actual fleet owner only after a successful replacement.</summary>
    public sealed class FleetCommitTarget : IEditCommitTarget
    {
        readonly FleetModel fleet;
        readonly ulong persistentId;
        public FleetCommitTarget(FleetModel fleet, ulong persistentId)
        { this.fleet = fleet ?? throw new ArgumentNullException(nameof(fleet)); this.persistentId = persistentId; }
        public bool TryCommitStructure(VoxelStructure committed, out string error)
        {
            error = null;
            try { fleet.ReplaceStructure(persistentId, committed); return true; }
            catch (Exception exception)
            {
                // Fleet observers run after ownership transfers. Never roll back cargo or dispose
                // the fleet's new structure when an observer, rather than replacement, failed.
                if (ReferenceEquals(fleet.Find(persistentId)?.Structure, committed) && committed != null)
                {
                    Game.Core.GameLog.Warn("EDT", "Fleet committed; observer failed: " + exception.Message);
                    return true;
                }
                error = exception.Message;
                return false;
            }
        }
    }
}
