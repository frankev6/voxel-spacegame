using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Save;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Voxel.Data;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Unity.Collections;

namespace Game.App.Session
{
    /// <summary>FLET v1: ordered ship records with deferred VOXS snapshots and blueprint recovery (SPEC-01 §5.10).</summary>
    public sealed class FleetSaveParticipant : ISaveParticipant
    {
        readonly FleetModel fleet;
        readonly ContentCatalog catalog;
        readonly BlueprintLibrary blueprints;
        readonly GameConfigDefinition config;
        readonly IPersistentIdAllocator ids;
        readonly Action<string> warning;
        readonly Action<ulong> reserveRestoredIds;
        readonly VoxsBlockNames names;
        public FleetSaveParticipant(FleetModel fleet, ContentCatalog catalog, BlueprintLibrary blueprints,
            GameConfigDefinition config, IPersistentIdAllocator ids, Action<string> warning = null, Action<ulong> reserveRestoredIds = null)
        {
            this.fleet = fleet ?? throw new ArgumentNullException(nameof(fleet));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.blueprints = blueprints ?? throw new ArgumentNullException(nameof(blueprints));
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.ids = ids ?? throw new ArgumentNullException(nameof(ids));
            this.warning = warning; this.reserveRestoredIds = reserveRestoredIds; names = VoxsBlockNames.FromCatalog(catalog);
        }
        public string Key => "FLET";
        public int Version => 1;
        public void Capture(SaveWriter writer)
        {
            writer.WriteVarUInt32((uint)fleet.Ships.Count);
            foreach (var ship in fleet.Ships)
            {
                writer.WriteUInt64(ship.PersistentId); writer.WriteContentId(ship.SourceBlueprint);
                writer.WriteString(ship.Name); writer.WriteBool(ReferenceEquals(ship, fleet.Flagship));
                using var source = VoxelStructureCodec.ToSource(ship.Structure, Allocator.TempJob);
                var snapshot = VoxsSnapshotBlock.Capture(source.Source, names);
                try { writer.WriteDeferred(snapshot); }
                catch { snapshot.Dispose(); throw; }
            }
        }
        public void Restore(SaveReader reader, int fromVersion)
        {
            if (fromVersion != 0 && fromVersion != Version) throw new SaveFormatException("Unsupported FLET version", reader.Position);
            fleet.ResetToEmpty();
            if (fromVersion == 0) { fleet.Add(CreateStartShip(), true); return; }
            uint count = reader.ReadVarUInt32();
            // Even palette-free records need at least 12 bytes. Bound hostile counts by the supplied payload before allocation.
            if (count > reader.Remaining / 12) throw new SaveFormatException("Invalid FLET record count", reader.Position);
            var staged = new List<FleetShipRecord>();
            var seen = new HashSet<ulong>();
            ulong flagged = 0, highestId = 0;
            try
            {
                for (uint i = 0; i < count; i++)
                {
                    ulong id = reader.ReadUInt64(); var blueprint = reader.ReadContentId();
                    string name = reader.ReadString(); bool isFlagship = reader.ReadBool();
                    byte[] payload = reader.ReadByteArray();
                    if (id == 0 || id == ulong.MaxValue || !seen.Add(id) || string.IsNullOrWhiteSpace(name) || name.Length > 32)
                        throw new SaveFormatException("Invalid FLET identity or name", reader.Position);
                    highestId = Math.Max(highestId, id);
                    // The first flagged record wins, even if it needs the configured blueprint fallback.
                    bool chosenFlagship = isFlagship && flagged == 0;
                    if (chosenFlagship) flagged = id;
                    if (!VoxelStructureCodec.TryDecode(payload, catalog, out var structure, out string error))
                    {
                        Warn("Ship " + id + " structure could not be decoded: " + error + "; trying its blueprint.");
                        if (blueprints.TryGet(blueprint.Hash, out var original)) structure = original.Clone();
                        else if (chosenFlagship)
                        {
                            var fallback = CreateStartShip(id); fallback.Name = name;
                            staged.Add(fallback);
                            Warn("Flagship " + id + " was rebuilt from the configured start blueprint; its id was kept.");
                            continue;
                        }
                        else { Warn("Ship " + id + " has no usable source blueprint and was dropped."); continue; }
                    }
                    staged.Add(new FleetShipRecord { PersistentId = id, SourceBlueprint = blueprint, Name = name, Structure = structure });
                }
                // SESS can be missing/damaged independently of this chunk. Preserve the restored identity range before allocating defaults.
                reserveRestoredIds?.Invoke(highestId);
                staged.Sort((a, b) => a.PersistentId.CompareTo(b.PersistentId));
                if (staged.Count == 0)
                {
                    staged.Add(CreateStartShip());
                    Warn("Fleet contained no usable ships; the configured start ship was restored.");
                }
                if (flagged == 0) flagged = staged[0].PersistentId;
                foreach (var ship in staged) fleet.Add(ship, ship.PersistentId == flagged);
                staged.Clear(); // Ownership transferred to the persistent model.
            }
            finally
            {
                foreach (var ship in staged)
                    if (!ReferenceEquals(fleet.Find(ship.PersistentId), ship)) ship.Dispose();
            }
        }
        internal FleetShipRecord CreateStartShip(ulong id = 0)
        {
            var definition = config.StartBlueprint;
            if (definition == null || !blueprints.TryGet(definition.Id.Hash, out var source))
                throw new InvalidOperationException("The configured start blueprint cannot be decoded");
            string name = string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.IdString : definition.DisplayName;
            if (name.Length > 32) name = name.Substring(0, 32);
            return new FleetShipRecord { PersistentId = id == 0 ? ids.Allocate() : id, SourceBlueprint = definition.Id,
                Name = name, Structure = source.Clone() };
        }
        void Warn(string message)
        {
            if (warning != null) warning(message);
            else GameLog.Warn(LogCategories.Flow, message);
        }
    }
}
