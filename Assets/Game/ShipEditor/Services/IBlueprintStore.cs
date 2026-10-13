namespace Game.ShipEditor.Services
{
    /// <summary>Implements SPEC-51 §4.2. Editor contract.</summary>
    public interface IBlueprintStore
    {
        void List(System.Collections.Generic.List<Game.ShipEditor.Data.BlueprintInfo> result);
        /// <summary>Caller owns and disposes the loaded structure; content blueprints are read-only.</summary>
        bool TryLoad(string blueprintId, out Game.Voxel.Model.VoxelStructure structure, out string error);
        bool TrySave(string displayName, Game.Voxel.Model.VoxelStructure structure, out string blueprintId, out string error);
        bool TryDelete(string blueprintId, out string error);
    }
}
