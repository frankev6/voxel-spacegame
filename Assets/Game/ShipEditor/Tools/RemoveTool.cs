using System.Collections.Generic;
using Game.ShipEditor.Data;
using Game.ShipEditor.Services;
using Unity.Mathematics;

namespace Game.ShipEditor.Tools
{
    internal sealed class RemoveTool : IEditorTool
    {
        readonly HashSet<BlockRecord> selected = new();
        readonly List<BlockRecord> ordered = new();
        bool box;
        int3 start, end, normal;
        int height = 1;
        public ToolId Id => ToolId.Remove;
        public bool HasGesture { get; private set; }
        public bool ConsumesWheel => HasGesture && box;
        public int Version { get; private set; }
        public void Tick(ToolContext context, in EditorInputFrame input)
        {
            if (!input.PointerAllowed) { if (input.PrimaryReleased) Cancel(); return; }
            if (input.PrimaryPressed)
            {
                if (context.Hover.Kind == DraftPicker.HitKind.None) return;
                context.BeginStroke(); HasGesture = true; box = input.Shift; height = 1;
                start = end = context.Hover.Cell; normal = context.Hover.Normal; Version++;
            }
            if (!HasGesture) return;
            if (box)
            {
                int axis = normal.x != 0 ? 0 : normal.y != 0 ? 1 : 2;
                float direction = context.GridRay.direction[axis];
                if (direction != 0)
                {
                    float distance = (start[axis] + 0.5f - context.GridRay.origin[axis]) / direction;
                    if (distance >= 0 && math.isfinite(distance))
                    {
                        float3 point = (float3)context.GridRay.GetPoint(distance);
                        if (math.all(math.isfinite(point)))
                        {
                            int3 next = (int3)math.clamp(math.floor(point), (float3)start - 255, (float3)start + 255);
                            next[axis] = start[axis]; if (!next.Equals(end)) { end = next; Version++; }
                        }
                    }
                }
                int nextHeight = math.clamp(height + (int)input.ToolWheel, 1, 256);
                if (nextHeight != height) { height = nextHeight; Version++; }
                if (input.PrimaryReleased) { RemoveBox(context); Cancel(); }
            }
            else
            {
                if (input.PrimaryPressed || input.PrimaryHeld) context.RemoveHovered(Id);
                if (input.PrimaryReleased) Cancel();
            }
        }
        void Bounds(out int3 min, out int3 max)
        { int3 far = end + normal * (height - 1); min = math.min(start, far); max = math.max(start, far); }
        void RemoveBox(ToolContext context)
        {
            Bounds(out var min, out var max); int3 size = max - min + 1;
            if ((long)size.x * size.y * size.z > context.Content.Config.MaxBatchCells)
            { context.SetError(PlacementError.OverBudget); return; }
            selected.Clear(); ordered.Clear(); var reader = new DraftReader(context.Session.Draft, context.Content);
            for (int x = min.x; x <= max.x; x++)
                for (int y = min.y; y <= max.y; y++)
                    for (int z = min.z; z <= max.z; z++)
                        if (reader.TryGetBlockAt(new int3(x, y, z), out var record) && selected.Add(record)) ordered.Add(record);
            foreach (var record in ordered) context.Remove(record, Id);
        }
        public void BuildPreview(ToolContext context, PreviewBuffer preview)
        {
            if (HasGesture && box) { Bounds(out var min, out var max); preview.SetBounds(min, max); }
            else preview.Clear();
        }
        public bool Cancel()
        { if (!HasGesture) return false; HasGesture = false; box = false; Version++; return true; }
    }
}
