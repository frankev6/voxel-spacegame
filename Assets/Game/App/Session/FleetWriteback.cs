using Game.Core.Flow;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Unity.Entities;

namespace Game.App.Session
{
    /// <summary>Copies live player damage into its owned structure before captures and World exit (SPEC-01 §5.7).</summary>
    public sealed class FleetWriteback : ISessionWriteback
    {
        readonly World world;
        readonly VoxelStructureRegistry registry;
        readonly PlayerShipSpawner player;
        internal FleetWriteback(World world, VoxelStructureRegistry registry, PlayerShipSpawner player)
        { this.world = world; this.registry = registry; this.player = player; }
        public int WritebackOrder => 100;
        public void WriteBackFromWorld()
        {
            if (!player.TryGetRoot(out var root)) return;
            var em = world.EntityManager;
            em.CompleteAllTrackedJobs();
            if (!em.HasComponent<VoxelGrid>(root) || em.IsComponentEnabled<GridSpawning>(root))
                throw new System.InvalidOperationException("The player spawn has not finished");
            GridStructureSync.WriteBack(em, root, registry);
            // TODO(GAL-008): capture the canonical galaxy player location after structure writeback.
        }
    }
}
