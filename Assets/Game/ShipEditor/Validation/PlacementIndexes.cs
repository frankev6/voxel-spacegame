using System;
using Game.ShipEditor.Data;
using Game.ShipEditor.Services;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.ShipEditor.Validation
{
    /// <summary>Incremental placement state. No query rescans the entire draft.</summary>
    internal sealed class PlacementIndexes : IDisposable
    {
        internal readonly ClearanceIndex Clearance = new();
        internal readonly AxisHistogram X = new(), Y = new(), Z = new();
        NativeHashSet<int3> reservedMissing = new(64, Allocator.Persistent);
        NativeHashMap<int3, BlockRecord> origins = new(64, Allocator.Persistent);
        internal int Cores { get; private set; }
        internal int SolidCells => X.Total;
        internal bool IsReservedMissing(int3 cell) => reservedMissing.Contains(cell);
        internal bool TryGetBlock(int3 origin, out BlockRecord block) => origins.TryGetValue(origin, out block);
        internal bool TryGetBounds(out int3 min, out int3 max)
        {
            bool exists = X.TryGetBounds(out int x0, out int x1);
            Y.TryGetBounds(out int y0, out int y1); Z.TryGetBounds(out int z0, out int z1);
            min = new int3(x0, y0, z0); max = new int3(x1, y1, z1); return exists;
        }
        internal void Initialize(VoxelStructure source, EditorContext context)
        {
            Clearance.Clear(); reservedMissing.Clear(); origins.Clear(); X.Clear(); Y.Clear(); Z.Clear(); Cores = 0;
            var reader = new DraftReader(source, context);
            using var records = new NativeList<BlockRecord>(Allocator.TempJob);
            using var bytes = new NativeList<byte>(Allocator.Temp);
            reader.Enumerate(records);
            foreach (var block in records)
            {
                bytes.Clear(); var record = block; record.DamageOffset = reader.CaptureDamage(record, bytes);
                Change(record, bytes.AsArray(), context, 1);
            }
        }
        internal void Apply(DiffCommand command, bool forward, EditorContext context)
        {
            for (int i = 0; i < command.Ops.Length; i++)
            {
                var op = command.Ops[forward ? i : command.Ops.Length - 1 - i];
                Change(op.Record, command.Damage.AsArray(), context, (op.Kind == DiffOpKind.Add) == forward ? 1 : -1);
            }
        }
        void Change(BlockRecord record, NativeArray<byte> bytes, EditorContext context, int delta)
        {
            var footprint = context.Footprints.Get(record.Block, record.Orient);
            if (delta > 0) { var identity = record; identity.DamageOffset = -1; origins.Add(record.Origin, identity); }
            else origins.Remove(record.Origin);
            if (context.Definitions.Value.Blocks[record.Block].IsCore) Cores += delta;
            foreach (var clearance in footprint.Clearance) Clearance.Add(record.Origin + clearance, delta);
            for (int i = 0; i < footprint.Cells.Length; i++)
            {
                int3 cell = record.Origin + footprint.Cells[i];
                bool missing = record.DamageOffset >= 0 && bytes[record.DamageOffset + i] == 0;
                if (missing)
                { if (delta > 0) reservedMissing.Add(cell); else reservedMissing.Remove(cell); }
                else { X.Add(cell.x, delta); Y.Add(cell.y, delta); Z.Add(cell.z, delta); }
            }
        }
        public void Dispose()
        {
            Clearance.Dispose();
            if (reservedMissing.IsCreated) reservedMissing.Dispose();
            if (origins.IsCreated) origins.Dispose();
        }
    }
}
