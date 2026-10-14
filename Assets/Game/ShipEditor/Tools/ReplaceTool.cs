using Game.ShipEditor.Data;
using Game.ShipEditor.Services;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.ShipEditor.Tools
{
    internal sealed class ReplaceTool : IEditorTool
    {
        public ToolId Id => ToolId.Replace;
        public bool HasGesture => false;
        public bool ConsumesWheel => false;
        public int Version => 0;
        public void Tick(ToolContext context, in EditorInputFrame input)
        {
            if (!input.PointerAllowed || !input.PrimaryPressed || !context.TryHovered(out var hovered) || context.SelectedBlock == 0) return;
            using var records = new NativeList<BlockRecord>(Allocator.TempJob);
            using var damage = new NativeList<byte>(Allocator.TempJob);
            bool selected = context.Selection.Contains(hovered.Origin);
            if (selected) context.Selection.Capture(records, damage);
            else
            {
                var error = context.Mapper.BuildRemovals(hovered, context.Symmetry, context.Session.Draft, records);
                if (error != PlacementError.None) { context.SetError(error); return; }
            }
            context.Batch.Clear(); context.BeginStroke();
            foreach (var source in records)
            {
                if (selected && source.Block != hovered.Block) continue;
                uint allowed = context.Content.Definitions.Value.Blocks[context.SelectedBlock].AllowedOrientations;
                byte orientation = source.Orient;
                if ((allowed & (1u << orientation)) == 0)
                { if ((allowed & 1) == 0) continue; orientation = 0; }
                var before = context.Content.Footprints.Get(source.Block, source.Orient);
                var after = context.Content.Footprints.Get(context.SelectedBlock, orientation);
                if (!Compatible(before, after)) continue;
                int start = context.Batch.Ops.Length;
                var original = source; original.DamageOffset = -1;
                context.Batch.Ops.Add(new DiffOp { Kind = DiffOpKind.Remove, Record = original });
                var replacement = original; replacement.Block = context.SelectedBlock; replacement.Orient = orientation;
                context.Batch.Ops.Add(new DiffOp { Kind = DiffOpKind.Add, Record = replacement });
                context.Batch.Groups.Add(new BatchGroup { OpStart = start, OpCount = 2 });
            }
            context.Session.TryApply(context.Batch, AtomicityMode.PerGroup, context.StrokeId, Id, "Replace blocks", context.Report);
            context.SetError(PlacementError.None);
            foreach (var error in context.Report.GroupErrors) if (error != PlacementError.None) { context.SetError(error); break; }
        }
        static bool Compatible(FootprintCache.Footprint a, FootprintCache.Footprint b)
        {
            if (a.Cells.Length == 1 && b.Cells.Length == 1) return true;
            if (a.Cells.Length != b.Cells.Length || !a.Min.Equals(b.Min) || !a.Max.Equals(b.Max)) return false;
            using var cells = new NativeHashSet<int3>(a.Cells.Length, Allocator.Temp);
            foreach (var cell in a.Cells) cells.Add(cell);
            foreach (var cell in b.Cells) if (!cells.Contains(cell)) return false;
            return true;
        }
        public void BuildPreview(ToolContext context, PreviewBuffer preview) => preview.Clear();
        public bool Cancel() => false;
    }
}
