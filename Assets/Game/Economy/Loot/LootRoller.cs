namespace Game.Economy
{
    /// <summary>SPEC-50 contract shell; ECO-004 owns behavior.</summary>
    public static class LootRoller   // §5.3; pure
    {
        public static void Roll(Game.Data.Definitions.LootTableDefinition table, int picks, ulong seed,
                                System.Collections.Generic.List<(Game.Data.Definitions.ItemDefinition item, int count)> into) => throw new System.NotImplementedException("ECO-004");
    }
}
