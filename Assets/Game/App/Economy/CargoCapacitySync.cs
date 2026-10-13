namespace Game.App.Economy
{
    /// <summary>SPEC-50 contract shell; ECO-008 owns behavior.</summary>
    public sealed class CargoCapacitySync : Game.Core.Flow.INewGameInitializer, System.IDisposable
    {
        public CargoCapacitySync(Game.Economy.InventoryModel inventory, Game.App.Session.FleetModel fleet, Game.Data.Services.ContentCatalog catalog,
                                 Game.Data.Definitions.EconomyConfigDefinition config) => throw new System.NotImplementedException("ECO-008");
        public int NewGameOrder => 40;                                    // before EconomyService (50)
        public void Recompute() => throw new System.NotImplementedException("ECO-008");                                          // also on SessionReset and FleetModel.Changed
        public static int ComputeCapacity(Game.Voxel.Model.VoxelStructure flagship, Game.Data.Services.ContentCatalog catalog,
                                          Game.Data.Definitions.EconomyConfigDefinition config) => throw new System.NotImplementedException("ECO-008");   // pure
        public void InitializeNewGame(in Game.Core.Flow.NewGameArgs args) => throw new System.NotImplementedException("ECO-008");
        public void Dispose() => throw new System.NotImplementedException("ECO-008");
    }
}
