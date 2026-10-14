using System;
using Game.ShipEditor.Data;
using Game.ShipEditor.Services;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.ShipEditor.Tools
{
    internal sealed class LineTool : IEditorTool, IDisposable
    {
        NativeList<int3> cells = new(Allocator.Persistent);
        int3 start, end, normal;
        public ToolId Id => ToolId.Line;
        public bool HasGesture { get; private set; }
        public bool ConsumesWheel => false;
        public int Version { get; private set; }
        public void Tick(ToolContext context, in EditorInputFrame input)
        {
            if (!input.PointerAllowed) { if (input.PrimaryReleased) Cancel(); return; }
            if (input.PrimaryPressed)
            {
                context.SetStatus(""); Version++;
                if (!context.AllowsArea() || context.Hover.Kind == DraftPicker.HitKind.None) return;
                context.BeginStroke(); start = end = context.Hover.PlacementCell; normal = context.Hover.Normal;
                HasGesture = true;
            }
            if (!HasGesture) return;
            if (context.Hover.Kind != DraftPicker.HitKind.None && !end.Equals(context.Hover.PlacementCell))
            { end = context.Hover.PlacementCell; Version++; }
            if (input.PrimaryReleased)
            { Build(context); context.ApplyArea(cells.AsArray(), normal, Id, "Place line"); Cancel(); }
        }
        void Build(ToolContext context)
        {
            cells.Clear();
            long x = Math.Abs((long)end.x - start.x), y = Math.Abs((long)end.y - start.y), z = Math.Abs((long)end.z - start.z);
            int axis = x >= z && x >= y ? 0 : z >= y ? 2 : 1;
            int limit = math.clamp(context.Content.Config.LineMaxCells, 1, 256);
            int steps = (int)Math.Min(Math.Abs((long)end[axis] - start[axis]), limit - 1);
            int sign = end[axis] >= start[axis] ? 1 : -1;
            for (int i = 0; i <= steps; i++) { var cell = start; cell[axis] += sign * i; cells.Add(cell); }
        }
        public void BuildPreview(ToolContext context, PreviewBuffer preview)
        {
            if (!HasGesture) { preview.Clear(); preview.SetMessage(context.StatusText, context.Error); return; }
            Build(context); context.PreviewArea(cells.AsArray(), normal, preview);
        }
        public bool Cancel() { if (!HasGesture) return false; HasGesture = false; Version++; return true; }
        public void Dispose() { if (cells.IsCreated) cells.Dispose(); cells = default; }
    }
}
