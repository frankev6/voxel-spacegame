namespace Game.Economy
{
    /// <summary>SPEC-50 contract shell; ECO-005 owns behavior.</summary>
    public sealed class MerchantService
    {
        public MerchantService(InventoryModel inventory, Wallet wallet, Game.Data.Services.ContentCatalog catalog,
                               Game.Data.Definitions.EconomyConfigDefinition config, System.Func<double> universalTime) => throw new System.NotImplementedException("ECO-005");
        public MerchantSession OpenSession(Game.Data.Definitions.MerchantDefinition merchant, int relation, bool refuse, string refusalReason) => throw new System.NotImplementedException("ECO-005");
        public MerchantSession Current => throw new System.NotImplementedException("ECO-005");         // null when none is open
        public void ResetToEmpty() => throw new System.NotImplementedException("ECO-005");                     // forgets every stock state
        // TODO(ECO-006): the spec leaves INVT restore parameters unspecified; define them with the participant.
        internal void RestoreState() => throw new System.NotImplementedException("ECO-006");
    }
}
