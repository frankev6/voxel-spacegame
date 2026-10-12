namespace Game.Voxel.Rendering
{
    /// <summary>Stable applied-brick identity. Slot zero is unallocated; generation prevents stale reuse.</summary>
    public struct BrickPoolHandle
    {
        public int Slot;
        public uint Generation;
    }
}
