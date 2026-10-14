using Unity.Entities;

namespace Game.Galaxy.Salvage
{
    /// <summary>QA-001: metres the player's intact salvage magnets add to salvage reach. Only writer: SalvageReachSystem.</summary>
    public struct SalvageReach : IComponentData { public uint Revision; public float BonusM; }   // Revision = VoxelGrid.Revision + 1; 0 = not computed
}
