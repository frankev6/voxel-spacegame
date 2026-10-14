using Game.ShipEditor.Data;
using Game.ShipEditor.Services;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.ShipEditor.Tools
{
    internal sealed class SelectTool : IEditorTool
    {
        int3 start, end, normal;
        int height = 1;
        bool additive, hasBlock, dragged;
        int3 clickedOrigin;
        public ToolId Id => ToolId.Select;
        public bool HasGesture { get; private set; }
        public bool ConsumesWheel => HasGesture;
        public int Version { get; private set; }
        public void Tick(ToolContext context, in EditorInputFrame input)
        {
            if (!input.PointerAllowed) { if (input.PrimaryReleased) Cancel(); return; }
            if (input.PrimaryPressed)
            {
                if (context.Hover.Kind == DraftPicker.HitKind.None) return;
                HasGesture = true; height = 1; dragged = false; additive = input.Ctrl;
                start = end = context.Hover.Cell; normal = context.Hover.Normal;
                hasBlock = context.TryHovered(out var record); clickedOrigin = record.Origin; Version++;
            }
            if (!HasGesture) return;
            int axis = normal.x != 0 ? 0 : normal.y != 0 ? 1 : 2;
            float direction = context.GridRay.direction[axis];
            if (direction != 0)
            {
                float distance = (start[axis] + .5f - context.GridRay.origin[axis]) / direction;
                if (distance >= 0 && math.isfinite(distance))
                {
                    float3 point = (float3)context.GridRay.GetPoint(distance);
                    if (math.all(math.isfinite(point)))
                    {
                        var next = (int3)math.clamp(math.floor(point), (float3)start - 255, (float3)start + 255);
                        next[axis] = start[axis];
                        if (!next.Equals(end)) { end = next; dragged = true; Version++; }
                    }
                }
            }
            int nextHeight = math.clamp(height + (int)input.ToolWheel, 1, 256);
            if (nextHeight != height) { height = nextHeight; dragged = true; Version++; }
            if (!input.PrimaryReleased) return;
            if (dragged)
            { Bounds(out var min, out var max); context.Selection.Box(min, max, additive); }
            else
            {
                if (!additive) context.Selection.Clear();
                if (hasBlock) { if (additive) context.Selection.Toggle(clickedOrigin); else context.Selection.Add(clickedOrigin); }
            }
            context.SetError(context.Selection.Error); Cancel();
        }
        void Bounds(out int3 min, out int3 max)
        { var far = end + normal * (height - 1); min = math.min(start, far); max = math.max(start, far); }
        public void BuildPreview(ToolContext context, PreviewBuffer preview)
        {
            if (HasGesture && dragged) { Bounds(out var min, out var max); preview.SetBounds(min, max); return; }
            using var records = new NativeList<BlockRecord>(Allocator.Temp);
            context.Selection.CopyRecords(records); preview.Set(records.AsArray(), context.Selection.Error);
        }
        public bool Cancel()
        { if (!HasGesture) return false; HasGesture = false; Version++; return true; }
    }
}
