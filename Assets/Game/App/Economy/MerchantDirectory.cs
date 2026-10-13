namespace Game.App.Economy
{
    /// <summary>SPEC-50 contract shell; ECO-008 owns behavior.</summary>
    public sealed class MerchantDirectory
    {
        public MerchantDirectory(Game.Data.Services.ContentCatalog catalog, Game.Galaxy.Services.GalaxyService galaxy) => throw new System.NotImplementedException("ECO-008");
        public Game.Data.Definitions.MerchantDefinition FindForBody(Game.Galaxy.Data.BodyId body) => throw new System.NotImplementedException("ECO-008");   // authored body first, then template; null if none
    }
}
