namespace Game.ShipEditor.Services
{
    /// <summary>Implements SPEC-51 §4.2. Editor contract.</summary>
    public interface IEditCommitTarget
    {
        /// <summary>Takes ownership on success. Must not throw; returns false and an error on failure.</summary>
        bool TryCommitStructure(Game.Voxel.Model.VoxelStructure committed, out string error);
    }
}
