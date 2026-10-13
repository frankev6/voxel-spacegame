namespace Game.Economy
{
    /// <summary>SPEC-50 §4.4/5.5: INVT order 60, version 1. ECO-006 owns binary capture/restore.</summary>
    public sealed class InventorySaveParticipant : Game.Core.Save.ISaveParticipant
    {
        public InventorySaveParticipant(EconomyService economy, Game.Data.Services.ContentCatalog catalog) => throw new System.NotImplementedException("ECO-006");
        public string Key => "INVT";
        public int Version => 1;
        public void Capture(Game.Core.Save.SaveWriter writer) => throw new System.NotImplementedException("ECO-006");
        public void Restore(Game.Core.Save.SaveReader reader, int fromVersion) => throw new System.NotImplementedException("ECO-006");
    }
}
