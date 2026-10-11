using System;
using System.Collections.Generic;
using Game.Core.Flow;
using Game.Voxel.Model;

namespace Game.App.Session
{
    /// <summary>Ordered ownership of fleet ships, reset in place with one notification per mutation (SPEC-01 §4.3).</summary>
    public sealed class FleetModel : ISessionModel, IDisposable
    {
        readonly List<FleetShipRecord> ships = new List<FleetShipRecord>();
        readonly IReadOnlyList<FleetShipRecord> view;
        public FleetModel() => view = ships.AsReadOnly();
        public IReadOnlyList<FleetShipRecord> Ships => view;
        public FleetShipRecord Flagship { get; private set; }
        public uint Revision { get; private set; }
        public event Action Changed;
        public FleetShipRecord Find(ulong persistentId)
        {
            foreach (var ship in ships) if (ship.PersistentId == persistentId) return ship;
            return null;
        }
        public void Add(FleetShipRecord record, bool makeFlagship)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (record.PersistentId == 0 || record.PersistentId == ulong.MaxValue || record.Structure == null || !record.Structure.Data.IsCreated ||
                string.IsNullOrWhiteSpace(record.Name) || record.Name.Length > 32)
                throw new ArgumentException("Ship requires a valid id, name and live structure", nameof(record));
            if (Find(record.PersistentId) != null) throw new InvalidOperationException("Duplicate ship id");
            int at = 0; while (at < ships.Count && ships[at].PersistentId < record.PersistentId) at++;
            ships.Insert(at, record);
            if (makeFlagship || Flagship == null) Flagship = record;
            Notify();
        }
        public bool Remove(ulong persistentId)
        {
            var ship = Find(persistentId);
            if (ship == null || ReferenceEquals(ship, Flagship)) return false;
            ship.Dispose(); ships.Remove(ship); Notify(); return true;
        }
        public void ReplaceStructure(ulong persistentId, VoxelStructure structure)
        {
            var ship = Find(persistentId) ?? throw new ArgumentException("Unknown ship id", nameof(persistentId));
            if (ship.StructureHandle >= 0) throw new InvalidOperationException("A spawned ship cannot be edited");
            if (structure == null || !structure.Data.IsCreated || structure.IsPinned || structure.IsReadOnly)
                throw new ArgumentException("Replacement must be a live, mutable, unpinned structure", nameof(structure));
            if (!ReferenceEquals(ship.Structure, structure)) { ship.Structure.Dispose(); ship.Structure = structure; }
            Notify();
        }
        public void ResetToEmpty()
        {
            foreach (var ship in ships)
                if (ship.Structure != null && ship.Structure.IsPinned) throw new InvalidOperationException("Cannot reset a spawning fleet");
            foreach (var ship in ships) ship.Dispose();
            ships.Clear(); Flagship = null; Notify();
        }
        void Notify() { Revision++; Changed?.Invoke(); }
        public void Dispose() { ResetToEmpty(); Changed = null; }
    }
}
