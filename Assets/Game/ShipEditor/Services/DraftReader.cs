using System;
using Game.ShipEditor.Data;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.ShipEditor.Services
{
    /// <summary>SPEC-51 EDT-003: read-only adapter and exact canonical damage capture.</summary>
    public sealed class DraftReader
    {
        readonly VoxelStructure structure;
        readonly EditorContext context;
        public DraftReader(VoxelStructure structure, EditorContext context)
        {
            this.structure = structure ?? throw new ArgumentNullException(nameof(structure));
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }
        public bool TryGetBlockAt(int3 cell, out BlockRecord record)
        {
            record = default;
            if (!structure.TryGetBlockAt(cell, out var block, out _)) return false;
            record = new BlockRecord { Origin = block.Origin, Block = block.Block, Orient = block.Orient,
                Color = block.Color, DamageOffset = -1 };
            return true;
        }
        /// <summary>Returns -1 for pristine; otherwise appends HP bytes and little-endian FunctionHp bits.</summary>
        public int CaptureDamage(in BlockRecord record, NativeList<byte> damage)
        {
            ref var definition = ref context.Definitions.Value.Blocks[record.Block];
            var footprint = context.Footprints.Get(record.Block, record.Orient).Cells;
            int offset = damage.Length, instanceIndex = -1;
            bool damaged = false;
            for (int i = 0; i < footprint.Length; i++)
            {
                int3 position = record.Origin + footprint[i];
                var cell = structure.GetCell(position);
                byte hp = 0;
                if (!cell.IsAir && structure.TryGetBlockAt(position, out var placed, out int index) &&
                    placed.Block == record.Block && placed.Origin.Equals(record.Origin) &&
                    placed.Orient == record.Orient && placed.Color == record.Color)
                { hp = cell.Hp; if (index >= 0) instanceIndex = index; }
                damage.Add(hp); damaged |= hp < 255;
            }
            float functionHp = float.NaN;
            if (definition.RequiresInstance)
            {
                if (instanceIndex < 0)
                {
                    damage.ResizeUninitialized(offset);
                    throw new InvalidOperationException("EDT: cannot capture an instance without any surviving cell.");
                }
                functionHp = structure.Data.Instances[instanceIndex].FunctionHp;
                damaged |= functionHp < definition.FunctionHp;
            }
            if (!damaged) { damage.ResizeUninitialized(offset); return -1; }
            uint bits = math.asuint(functionHp);
            damage.Add((byte)bits); damage.Add((byte)(bits >> 8)); damage.Add((byte)(bits >> 16)); damage.Add((byte)(bits >> 24));
            return offset;
        }
    }
}
