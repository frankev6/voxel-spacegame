using System;
using System.Collections.Generic;
using System.Globalization;
using Game.App.EditorHost;
using Game.App.Session;
using Game.Core.Events;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Galaxy.Services;
using Game.ShipEditor.Data;
using Game.ShipEditor.Jobs;
using Game.UI.ViewModels;
using Game.UI.Views;
using Game.Voxel.Model;
using Unity.Collections;

namespace Game.App.Presenters
{
    /// <summary>SPEC-61 HUD-017: committed fleet projection, canonical block stats and docking-gated launcher commands.</summary>
    internal sealed class FleetPresenter : IFleetCommands, IDisposable
    {
        readonly FleetViewModel model;
        readonly ContentCatalog catalog;
        readonly ShipEditorLauncher launcher;
        readonly Action<string> notice;
        readonly List<FleetShipRowViewModel> rows = new(32);
        readonly List<StatRowViewModel> stats = new(12);
        FleetModel fleet;
        DockingService docking;
        bool disposed, launcherUnavailable;
        internal FleetPresenter(FleetViewModel model, ContentCatalog catalog, ShipEditorLauncher launcher = null, Action<string> notice = null)
        { this.model = model ?? throw new ArgumentNullException(nameof(model)); this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog)); this.launcher = launcher; this.notice = notice; }
        internal void Bind(FleetModel fleet, DockingService docking)
        {
            if (disposed) throw new ObjectDisposedException(nameof(FleetPresenter));
            if (fleet == null || docking == null) throw new ArgumentNullException("Fleet and docking owners are required.");
            Unbind(); this.fleet = fleet; this.docking = docking; launcherUnavailable = false;
            fleet.Changed += Refresh; EventBus<DockStateChanged>.Subscribe(DockChanged); Refresh();
        }
        internal void Unbind()
        {
            if (fleet != null) { fleet.Changed -= Refresh; EventBus<DockStateChanged>.Unsubscribe(DockChanged); }
            fleet = null; docking = null; model.Ships.Clear(); model.SelectedIndex = -1; model.NotifyShips(); Details();
        }
        void DockChanged(DockStateChanged _) => Gate();
        FleetShipRecord Selected() => fleet != null && model.SelectedIndex >= 0 && model.SelectedIndex < model.Ships.Count ? fleet.Find(model.Ships[model.SelectedIndex].PersistentId) : null;
        internal void Refresh()
        {
            ulong selected = Selected()?.PersistentId ?? 0; model.Ships.Clear(); int selection = -1;
            if (fleet != null) for (int i = 0; i < fleet.Ships.Count; i++)
            {
                if (rows.Count <= i) rows.Add(new FleetShipRowViewModel());
                var ship = fleet.Ships[i]; var row = rows[i]; Describe(ship, out _, out int blocks, out _, out float hull);
                row.PersistentId = ship.PersistentId; row.Name = ship.Name; row.HullFraction = hull; row.BlockCount = blocks;
                row.IsActive = ReferenceEquals(ship, fleet.Flagship); row.IsSelected = false; model.Ships.Add(row);
                if (ship.PersistentId == selected || selected == 0 && row.IsActive) selection = i;
            }
            model.SelectedIndex = selection; if (selection >= 0) model.Ships[selection].IsSelected = true;
            model.NotifyShips(); Details();
        }
        public void Select(int index)
        {
            if (fleet == null || (uint)index >= model.Ships.Count) return;
            int previous = model.SelectedIndex; model.SelectedIndex = index;
            if (previous >= 0 && previous < model.Ships.Count) model.Ships[previous].IsSelected = false;
            model.Ships[index].IsSelected = true; Details();
        }
        void Details()
        {
            var ship = Selected(); model.DetailName = ship?.Name ?? ""; model.DetailClass = ""; model.DetailStats.Clear();
            if (ship != null)
            {
                model.DetailClass = catalog.TryGet<ShipBlueprintDefinition>(ship.SourceBlueprint, out var definition) && !string.IsNullOrEmpty(definition.DisplayName) ? definition.DisplayName : "Custom ship";
                Describe(ship, out var total, out int blocks, out int cells, out _);
                Stat("mass", "Mass", total.Mass, " kg"); Stat("blocks", "Blocks", blocks, ""); Stat("cells", "Cells", cells, "");
                Stat("thrust", "Forward thrust", total.Thrust.z, " N"); Stat("power-generation", "Power generation", total.PowerGeneration, " E/s");
                Stat("power-use", "Power draw", total.PowerDraw, " E/s"); Stat("power-storage", "Power storage", total.PowerStorage, " E");
                Stat("dps", "Sustained damage", total.SustainedDps, " HP/s"); Stat("shields", "Shield capacity", total.ShieldCapacity, " HP");
                Stat("shield-regen", "Shield regeneration", total.ShieldRegen, " HP/s"); Stat("cargo", "Cargo capacity", total.CargoUnits, " units");
            }
            model.NotifyDetailStats(); Gate();
        }
        void Stat(string id, string label, double value, string unit)
        {
            int i = model.DetailStats.Count; if (stats.Count <= i) stats.Add(new StatRowViewModel());
            var row = stats[i]; row.StatId = id; row.Label = label; row.Value = value.ToString("0.#", CultureInfo.InvariantCulture) + unit;
            row.Delta = ""; row.DeltaSign = 0; model.DetailStats.Add(row);
        }
        void Gate()
        {
            var selected = Selected(); model.CanSetActive = false;
            model.EditBlockedReason = selected == null ? "Select a ship to edit" : docking?.IsDocked != true ? "Dock at a station to edit"
                : !ReferenceEquals(selected, fleet.Flagship) ? "Only the active ship can be edited" : launcher == null || launcherUnavailable ? "Ship editing is not available yet" : "";
            model.CanEdit = model.EditBlockedReason.Length == 0;
        }
        public void Edit()
        {
            Gate(); if (!model.CanEdit) { notice?.Invoke(model.EditBlockedReason); return; }
            try { if (!launcher.EditPlayerShip(out string reason)) { model.EditBlockedReason = reason ?? "Ship editing was refused"; notice?.Invoke(model.EditBlockedReason); } }
            catch (NotImplementedException) { launcherUnavailable = true; Gate(); notice?.Invoke(model.EditBlockedReason); }
        }
        public void SetActive() => notice?.Invoke("Active ship switching is not available yet");
        void Describe(FleetShipRecord ship, out BlockStatContribution total, out int blocks, out int cells, out float hull)
        {
            total = default; blocks = cells = 0; hull = 0; var structure = ship.Structure;
            structure.Pin();
            try
            {
                using var order = structure.SortedBrickOrder(Allocator.TempJob); using var records = new NativeList<BlockRecord>(Allocator.TempJob);
                var source = VoxelStructureReadView.From(structure); new BlockEnumerateJob { Source = source, BrickOrder = order, Blocks = records }.Execute();
                long hp = 0; int expectedCells = 0; blocks = records.Length; cells = structure.SolidCellCount;
                foreach (var record in records)
                {
                    var definition = catalog.GetBlock(record.Block); expectedCells += definition.Cells.Count; var contribution = BlockStatMath.Of(definition, record.Orient);
                    total.Mass += contribution.Mass; total.Thrust += contribution.Thrust; total.PowerGeneration += contribution.PowerGeneration;
                    total.PowerDraw += contribution.PowerDraw; total.PowerStorage += contribution.PowerStorage; total.SustainedDps += contribution.SustainedDps;
                    total.ShieldCapacity += contribution.ShieldCapacity; total.ShieldRegen += contribution.ShieldRegen; total.CargoUnits += contribution.CargoUnits;
                }
                for (int b = 0; b < source.Bricks.Length; b++) for (int c = 0; c < BrickMath.CellsPerBrick; c++) hp += source.Read(b, c).Hp;
                hull = expectedCells > 0 ? (float)(hp / (255d * expectedCells)) : 0;
            }
            finally { structure.Unpin(); }
        }
        public void Dispose() { if (disposed) return; Unbind(); disposed = true; }
    }
}
