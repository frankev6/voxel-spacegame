namespace Game.App.Boot
{
    /// <summary>Creates the configuration singletons at boot (SPEC-01 §5.3). Implements SPEC-01 §4.5 (shell; FND-005 adds the behaviour).</summary>
    internal static class BootSingletons
    {
        internal static void CreateAll(Unity.Entities.EntityManager em, Game.Data.Services.ContentCatalog catalog) { throw new System.NotImplementedException("FND-005"); }
    }
}
