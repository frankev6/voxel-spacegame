using System;
using System.Globalization;
using Game.ShipEditor.Data;
using Game.ShipEditor.Services;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.ShipEditor.Tools
{
    internal sealed class BoxTool : IEditorTool, IDisposable
    {
        NativeList<int3> cells = new(Allocator.Persistent);
        int3 start, end, normal;
        int height = 1;
        BoxMode mode;
        public ToolId Id => ToolId.Box;
        public bool HasGesture { get; private set; }
        public bool ConsumesWheel => HasGesture;
        public int Version { get; private set; }
        public void Tick(ToolContext context, in EditorInputFrame input)
        {
            if (mode != context.BoxMode) { mode = context.BoxMode; Version++; }
            if (!input.PointerAllowed) { if (input.PrimaryReleased) Cancel(); return; }
            if (input.PrimaryPressed)
            {
                context.SetStatus(""); Version++;
                if (!context.AllowsArea() || context.Hover.Kind == DraftPicker.HitKind.None) return;
                context.BeginStroke(); start = end = context.Hover.PlacementCell; normal = context.Hover.Normal;
                height = 1; HasGesture = true;
            }
            if (!HasGesture) return;
            int axis = normal.x != 0 ? 0 : normal.y != 0 ? 1 : 2;
            var ray = context.GridRay; float direction = ray.direction[axis];
            if (direction != 0)
            {
                double distance = ((double)start[axis] + .5 - ray.origin[axis]) / direction;
                double3 point = (float3)ray.origin + (double3)(float3)ray.direction * distance;
                if (distance >= 0 && math.all(math.isfinite(point)) &&
                    math.all(point >= int.MinValue) && math.all(point < int.MaxValue))
                {
                    int3 next = (int3)math.floor(point); next[axis] = start[axis];
                    if (!end.Equals(next)) { end = next; Version++; }
                }
            }
            int wheel = math.isfinite(input.ToolWheel) ? (int)math.clamp((double)input.ToolWheel, -256, 256) : 0;
            int nextHeight = math.clamp(height + wheel, 1, 256);
            if (height != nextHeight) { height = nextHeight; Version++; }
            if (input.PrimaryReleased)
            {
                if (Build(context, out _, out _, out _)) context.ApplyArea(cells.AsArray(), normal, Id, "Place box");
                Cancel();
            }
        }
        bool Bounds(ToolContext context, out int3 min, out int3 max, out int3 size)
        {
            int3 far = end;
            for (int i = 0; i < 3; i++)
            {
                long coordinate = (long)far[i] + normal[i] * (height - 1);
                if (coordinate < int.MinValue || coordinate > int.MaxValue)
                { min = max = size = default; context.SetStatus("Region too large", PlacementError.OverBudget); return false; }
                far[i] = (int)coordinate;
            }
            min = math.min(start, far); max = math.max(start, far); size = default;
            for (int i = 0; i < 3; i++)
            {
                long extent = (long)max[i] - min[i] + 1;
                if (extent > int.MaxValue)
                { context.SetStatus("Region too large", PlacementError.OverBudget); return false; }
                size[i] = (int)extent;
            }
            // Budget the cells this mode creates, not the unfilled interior of a hollow/frame box.
            decimal count = (decimal)size.x * size.y * size.z;
            if (mode == BoxMode.Hollow)
                count -= (decimal)math.max(0, size.x - 2) * math.max(0, size.y - 2) * math.max(0, size.z - 2);
            else if (mode == BoxMode.Frame)
            {
                int3 boundary = math.min(size, 2), interior = math.max(size - 2, 0);
                count = (decimal)boundary.x * boundary.y * boundary.z + (decimal)interior.x * boundary.y * boundary.z +
                    (decimal)boundary.x * interior.y * boundary.z + (decimal)boundary.x * boundary.y * interior.z;
            }
            if (count > context.Content.Config.MaxBatchCells)
            { context.SetStatus("Region too large", PlacementError.OverBudget); return false; }
            return true;
        }
        bool Build(ToolContext context, out int3 min, out int3 max, out int3 size)
        {
            cells.Clear(); if (!Bounds(context, out min, out max, out size)) return false;
            int3 far = end + normal * (height - 1), step = (int3)math.sign(far - start);
            for (int x = 0; x < size.x; x++)
                for (int y = 0; y < size.y; y = NextIndex(y, size.y, mode == BoxMode.Frame && x != 0 && x != size.x - 1))
                    for (int z = 0; z < size.z;)
                    {
                        cells.Add(start + new int3(x, y, z) * step);
                        bool xEdge = x == 0 || x == size.x - 1, yEdge = y == 0 || y == size.y - 1;
                        bool allZ = mode == BoxMode.Solid || mode == BoxMode.Hollow && (xEdge || yEdge) || mode == BoxMode.Frame && xEdge && yEdge;
                        z = NextIndex(z, size.z, !allZ);
                    }
            return true;
        }
        static int NextIndex(int index, int size, bool endsOnly)
            => endsOnly && index == 0 && size > 1 ? size - 1 : index + 1;
        public void BuildPreview(ToolContext context, PreviewBuffer preview)
        {
            if (!HasGesture) { preview.Clear(); preview.SetMessage(context.StatusText, context.Error); return; }
            if (!Bounds(context, out var min, out var max, out var size))
            { preview.Clear(); preview.SetMessage(context.StatusText, context.Error); return; }
            decimal volume = (decimal)size.x * size.y * size.z;
            string label = string.Format(CultureInfo.InvariantCulture, "{0} × {1} × {2} = {3}", size.x, size.y, size.z, volume);
            if (volume > context.Content.Config.PreviewOutlineThreshold) preview.SetBounds(min, max);
            else if (Build(context, out _, out _, out _)) context.PreviewArea(cells.AsArray(), normal, preview);
            preview.SetMessage(label, preview.Error);
        }
        public bool Cancel() { if (!HasGesture) return false; HasGesture = false; Version++; return true; }
        public void Dispose() { if (cells.IsCreated) cells.Dispose(); cells = default; }
    }
}
