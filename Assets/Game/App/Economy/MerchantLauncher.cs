namespace Game.App.Economy
{
    /// <summary>SPEC-50 §4.6 app entry-point shell; ECO-008 owns routing/UI wiring.</summary>
    public sealed class MerchantLauncher
    {
        // TODO(ECO-008 / ENC-001): attach the exact §4.6 constructor after FactionService,
        // TerritoryService and EncounterDirectorService exist. Do not invent substitute services.

        public bool TryOpenForBody(Game.Galaxy.Data.BodyId body, System.Action onClosed) => throw new System.NotImplementedException("ECO-008");          // false when no merchant
        public void Open(Game.Data.Definitions.MerchantDefinition merchant, System.Action onClosed, ulong encounterMemberPersistentId = 0) => throw new System.NotImplementedException("ECO-008");
    }
}
