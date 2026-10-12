namespace Game.Voxel.Rendering
{
    /// <summary>Exact element range inside a shared GPU pool (ADR-0032); Count zero is empty.</summary>
    public struct PoolRange
    {
        public int Start, Count;
        public int End => Start + Count;
    }
}
