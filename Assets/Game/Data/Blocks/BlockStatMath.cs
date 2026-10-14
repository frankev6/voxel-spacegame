namespace Game.Data.Blocks
{
    /// <summary>Editor stats of one block at one orientation: Mass from cells, then every module's ContributeStats in list order.
    /// Implements SPEC-04 §4.6.</summary>
    public static class BlockStatMath
    {
        public static BlockStatContribution Of(Game.Data.Definitions.BlockDefinition block, byte orient)
        {
            if (block == null) throw new System.ArgumentNullException(nameof(block));
            if (orient >= 24) throw new System.ArgumentOutOfRangeException(nameof(orient));
            var stats = new BlockStatContribution { Mass = block.Cells.Count * block.CellMass };
            foreach (var module in block.Modules) module.ContributeStats(ref stats, orient);
            return stats;
        }
    }
}
