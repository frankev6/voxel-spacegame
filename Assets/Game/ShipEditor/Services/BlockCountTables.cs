using System;
using Game.ShipEditor.Data;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.ShipEditor.Services
{
    /// <summary>SPEC-51 §5.7: block counts, not occupied-cell counts. Owned by the session.</summary>
    internal sealed class BlockCountTables : IDisposable
    {
        readonly int[] intact, damaged;
        NativeHashMap<int, int> orientations;
        internal int TypeCount => intact.Length;
        internal NativeHashMap<int, int>.ReadOnly Orientations => orientations.AsReadOnly();
        internal int Intact(int block) => intact[block];
        internal int Damaged(int block) => damaged[block];
        internal BlockCountTables(int types)
        { intact = new int[types]; damaged = new int[types]; orientations = new NativeHashMap<int, int>(types, Allocator.Persistent); }
        internal void CopyFrom(BlockCountTables source)
        {
            Array.Copy(source.intact, intact, intact.Length); Array.Copy(source.damaged, damaged, damaged.Length);
            orientations.Clear(); foreach (var pair in source.orientations) orientations.Add(pair.Key, pair.Value);
        }
        internal void Initialize(VoxelStructure source, EditorContext context, Action<BlockRecord> onBlock = null)
        {
            Array.Clear(intact, 0, intact.Length); Array.Clear(damaged, 0, damaged.Length); orientations.Clear();
            var reader = new DraftReader(source, context);
            using var records = new NativeList<BlockRecord>(Allocator.TempJob);
            using var bytes = new NativeList<byte>(Allocator.Temp);
            reader.Enumerate(records);
            foreach (var record in records)
            {
                bytes.Clear(); bool isDamaged = reader.CaptureDamage(record, bytes) >= 0;
                Add(record, isDamaged, 1);
                onBlock?.Invoke(record);
            }
        }
        internal void Apply(DiffCommand command, bool forward, EditorContext context)
        {
            for (int i = 0; i < command.Ops.Length; i++)
            {
                var op = command.Ops[forward ? i : command.Ops.Length - 1 - i];
                bool add = (op.Kind == DiffOpKind.Add) == forward;
                Add(op.Record, IsDamaged(op.Record, command.Damage.AsArray(), context), add ? 1 : -1);
            }
        }
        static bool IsDamaged(BlockRecord record, NativeArray<byte> bytes, EditorContext context)
        {
            if (record.DamageOffset < 0) return false;
            int cells = context.Footprints.Get(record.Block, record.Orient).Cells.Length, offset = record.DamageOffset;
            for (int i = 0; i < cells; i++) if (bytes[offset + i] < 255) return true;
            ref var definition = ref context.Definitions.Value.Blocks[record.Block];
            if (!definition.RequiresInstance) return false;
            offset += cells;
            uint bits = (uint)(bytes[offset] | bytes[offset + 1] << 8 | bytes[offset + 2] << 16 | bytes[offset + 3] << 24);
            return math.asfloat(bits) < definition.FunctionHp;
        }
        public void Dispose() { if (orientations.IsCreated) orientations.Dispose(); }
        void Add(BlockRecord record, bool isDamaged, int delta)
        {
            var counts = isDamaged ? damaged : intact;
            counts[record.Block] += delta;
            int key = record.Block * 24 + record.Orient; orientations.TryGetValue(key, out int count); count += delta;
            if (count == 0) orientations.Remove(key); else orientations[key] = count;
        }
    }
}
