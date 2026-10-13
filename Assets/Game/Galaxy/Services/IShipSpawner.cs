namespace Game.Galaxy.Services
{
    /// <summary>Implements SPEC-31 §4.4/6. Galaxy ship spawn seam, implemented by SPEC-01.</summary>
    public interface IShipSpawner
    {
        PersistentIdValue Spawn(in GalaxyShipSpawnArgs args);
        bool TryGetRoot(ulong persistentId, out Unity.Entities.Entity root);
        bool Despawn(ulong persistentId);
    }
}
