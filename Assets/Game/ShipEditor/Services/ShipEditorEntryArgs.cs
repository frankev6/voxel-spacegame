namespace Game.ShipEditor.Services
{
    /// <summary>Implements SPEC-51 §4.2. Editor contract.</summary>
    public sealed class ShipEditorEntryArgs
    {
        public Game.ShipEditor.Data.EditSessionOptions Options;
        public Game.Voxel.Model.VoxelStructure Source; // caller keeps ownership; session clones
        public IEditCommitTarget CommitTarget;
        public Game.Economy.InventoryModel Inventory; // null when creative
        public ulong PersistentId;                   // fleet ship id; 0 for blueprints
    }
}
