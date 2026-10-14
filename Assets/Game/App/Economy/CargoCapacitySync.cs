using System;
using Game.App.Session;
using Game.Core.Events;
using Game.Core.Flow;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Economy;
using Game.Voxel.Model;

namespace Game.App.Economy
{
    /// <summary>ECO-011: capacity follows the committed flagship design through owning model events.</summary>
    public sealed class CargoCapacitySync : INewGameInitializer, IDisposable
    {
        readonly InventoryModel inventory;
        readonly FleetModel fleet;
        readonly ContentCatalog catalog;
        readonly EconomyConfigDefinition config;
        bool disposed;
        public CargoCapacitySync(InventoryModel inventory, FleetModel fleet, ContentCatalog catalog, EconomyConfigDefinition config)
        {
            this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            this.fleet = fleet ?? throw new ArgumentNullException(nameof(fleet));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.config = config != null ? config : throw new ArgumentNullException(nameof(config));
            Recompute();
            fleet.Changed += Recompute; EventBus<SessionReset>.Subscribe(OnReset);
        }
        public int NewGameOrder => 40;
        public void Recompute()
        {
            if (!disposed) inventory.SetCapacity(ComputeCapacity(fleet.Flagship?.Structure, catalog, config));
        }
        public static int ComputeCapacity(VoxelStructure flagship, ContentCatalog catalog, EconomyConfigDefinition config)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (!float.IsFinite(config.UnitsPerSlot) || config.UnitsPerSlot <= 0)
                throw new ArgumentException("Cargo units per slot must be finite and positive.", nameof(config));
            double units = 0;
            if (flagship != null && flagship.Data.IsCreated)
                foreach (var instance in flagship.Data.Instances)
                {
                    if (instance.Block == 0 || (instance.Flags & BlockInstanceFlags.Destroyed) != 0) continue;
                    var definition = catalog.GetBlock(instance.Block);
                    if (definition == null || !definition.TryGetModule(out CargoModule cargo)) continue;
                    if (!float.IsFinite(cargo.Params.Capacity) || cargo.Params.Capacity < 0)
                        throw new ArgumentException("Cargo module capacity must be finite and nonnegative.", nameof(flagship));
                    units += cargo.Params.Capacity;
                }
            double slots = config.BaseCargoSlots + Math.Floor(units / config.UnitsPerSlot);
            return (int)Math.Clamp(slots, Math.Clamp(config.BaseCargoSlots, 1, InventoryModel.MaxSlots), InventoryModel.MaxSlots);
        }
        void OnReset(SessionReset value) => Recompute();
        public void InitializeNewGame(in NewGameArgs args) => Recompute();
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; fleet.Changed -= Recompute; EventBus<SessionReset>.Unsubscribe(OnReset);
        }
    }
}
