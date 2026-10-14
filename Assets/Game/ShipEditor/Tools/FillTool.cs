using System;
using Game.ShipEditor.Data;
using Game.ShipEditor.Services;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.ShipEditor.Tools
{
    internal sealed class FillTool : IEditorTool, IDisposable
    {
        NativeList<int3> cells = new(Allocator.Persistent);
        NativeHashSet<int3> visited = new(64, Allocator.Persistent);
        public ToolId Id => ToolId.Fill;
        public bool HasGesture => false;
        public bool ConsumesWheel => false;
        public int Version { get; private set; }
        public void Tick(ToolContext context, in EditorInputFrame input)
        {
            if (!input.PointerAllowed || !input.PrimaryPressed) return;
            context.SetStatus(""); Version++;
            if (!context.AllowsArea() || context.Hover.Kind != DraftPicker.HitKind.Block) return;
            var draft = context.Session.Draft; int3 seed = context.Hover.PlacementCell, normal = context.Hover.Normal;
            if (!draft.TryGetCellBounds(out var min, out var max) || !math.all(min <= max))
            { Abort(context, "Region not enclosed"); return; }
            int axis = normal.x != 0 ? 0 : normal.y != 0 ? 1 : 2;
            if (!draft.GetCell(seed).IsAir) { context.SetError(PlacementError.Occupied); return; }
            cells.Clear(); visited.Clear(); cells.Add(seed); visited.Add(seed);
            int limit = math.max(0, math.min(context.Content.Config.FillMaxCells, context.Content.Config.MaxBatchCells));
            for (int i = 0; i < cells.Length; i++)
            {
                var cell = cells[i];
                for (int a = 0; a < 3; a++)
                    if (a != axis && (cell[a] <= min[a] || cell[a] >= max[a]))
                    { Abort(context, "Region not enclosed"); return; }
                if (cells.Length > limit) { Abort(context, "Region too large"); return; }
                // X/Y/Z and positive/negative order is fixed; only the two in-plane axes participate.
                for (int a = 0; a < 3; a++)
                    if (a != axis)
                        for (int sign = 1; sign >= -1; sign -= 2)
                        {
                            int3 neighbour = cell; neighbour[a] += sign;
                            if (draft.GetCell(neighbour).IsAir && visited.Add(neighbour)) cells.Add(neighbour);
                        }
            }
            context.BeginStroke(); context.ApplyArea(cells.AsArray(), normal, Id, "Fill region");
        }
        void Abort(ToolContext context, string message)
        { cells.Clear(); context.SetStatus(message, message == "Region too large" ? PlacementError.OverBudget : PlacementError.OutOfBounds); }
        public void BuildPreview(ToolContext context, PreviewBuffer preview)
        { preview.Clear(); preview.SetMessage(context.StatusText, context.Error); }
        public bool Cancel() => false;
        public void Dispose()
        { if (cells.IsCreated) cells.Dispose(); if (visited.IsCreated) visited.Dispose(); cells = default; visited = default; }
    }
}
