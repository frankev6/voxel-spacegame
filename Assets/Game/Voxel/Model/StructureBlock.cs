namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.3. InstanceIndex -1 = palette cell.</summary>
    public struct StructureBlock { public BlockPlacement Placement; public int InstanceIndex; /* -1 = palette cell */ }
}
