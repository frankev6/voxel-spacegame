namespace Game.Galaxy.Salvage
{
    /// <summary>SPEC-50 contract shell; ECO-007 owns behavior.</summary>
    public sealed class SalvageService : System.IDisposable
    {
        public SalvageService(Unity.Entities.World world, Game.Data.Services.ContentCatalog catalog, Game.Data.Definitions.EconomyConfigDefinition config,
                              Game.Core.Audio.IAudioService audio) => throw new System.NotImplementedException("ECO-007");
        public bool TrySalvage(Unity.Entities.Entity hulkRoot, out string message) => throw new System.NotImplementedException("ECO-007");   // also called by the BodyInteracted handler
        public static void ComputeYields(Unity.Entities.DynamicBuffer<Game.Voxel.Model.BlockInstance> instances, uint solidCells, ulong seed,
                                         Game.Data.Services.ContentCatalog catalog, Game.Data.Definitions.EconomyConfigDefinition config,
                                         System.Collections.Generic.List<SalvageYield> into) => throw new System.NotImplementedException("ECO-007");   // pure, deterministic
        public int PendingWrecks => throw new System.NotImplementedException("ECO-007");             // hulks with leftover yields
        public void Dispose() => throw new System.NotImplementedException("ECO-007");                        // unsubscribes
    }
}
