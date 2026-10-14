using System;
using Game.ShipEditor.Data;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.ShipEditor.Services
{
    /// <summary>SPEC-51 §5.3: forward/inverse translation; the sole draft write call is ApplyEdits.</summary>
    public sealed class DraftWriter
    {
        readonly VoxelStructure structure;
        readonly EditorContext context;
        public DraftWriter(VoxelStructure structure, EditorContext context)
        {
            this.structure = structure ?? throw new ArgumentNullException(nameof(structure));
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }
        // EDT-004/005 own DiffCommand/EditBatch storage and pass these native views without copying.
        public void Apply(NativeArray<DiffOp> operations, NativeArray<byte> damage, bool forward, NativeList<int3> touchedBricks)
        {
            using var edits = new NativeList<VoxelEdit>(Allocator.Temp);
            for (int n = 0; n < operations.Length; n++)
            {
                var op = operations[forward ? n : operations.Length - 1 - n];
                if (op.Kind != DiffOpKind.Add && op.Kind != DiffOpKind.Remove) throw new ArgumentException("EDT: unknown diff operation.", nameof(operations));
                bool add = (op.Kind == DiffOpKind.Add) == forward;
                var record = op.Record;
                var edit = new VoxelEdit { Kind = add ? VoxelEditKind.PlaceBlock : VoxelEditKind.RemoveBlock,
                    Block = record.Block, Orient = record.Orient, Color = record.Color, Cell = record.Origin };
                edits.Add(edit);
                if (!add || record.DamageOffset == -1) continue;
                var footprint = context.Footprints.Get(record.Block, record.Orient).Cells;
                int offset = record.DamageOffset;
                if (offset < 0 || offset > damage.Length - footprint.Length - 4)
                    throw new ArgumentException("EDT: damage bytes are truncated.", nameof(damage));
                for (int i = 0; i < footprint.Length; i++)
                {
                    byte hp = damage[offset + i]; if (hp == 255) continue;
                    edit.Kind = hp == 0 ? VoxelEditKind.ClearCell : VoxelEditKind.SetCellHp;
                    edit.Cell = record.Origin + footprint[i]; edit.Hp = hp; edits.Add(edit);
                }
                offset += footprint.Length;
                uint bits = (uint)(damage[offset] | damage[offset + 1] << 8 | damage[offset + 2] << 16 | damage[offset + 3] << 24);
                float functionHp = math.asfloat(bits);
                if (!float.IsNaN(functionHp))
                { edit.Kind = VoxelEditKind.SetFunctionHp; edit.Cell = record.Origin; edit.FunctionHp = functionHp; edits.Add(edit); }
            }
            var content = new VoxelContent { Blocks = context.Definitions, Orientations = context.Orientations };
            if (!structure.ApplyEdits(edits.AsArray(), content, touchedBricks))
                throw new InvalidOperationException("EDT: ApplyEdits rejected a validated batch");
        }
    }
}
