using System.Collections.Generic;
using Game.ShipEditor.Data;
using Game.ShipEditor.Services;
using Unity.Mathematics;

namespace Game.ShipEditor.Tools
{
    internal sealed class PaintTool : IEditorTool
    {
        readonly HashSet<BlockRecord> painted = new();
        readonly HashSet<BlockRecord> discovered = new();
        readonly List<BlockRecord> flood = new();
        public ToolId Id => ToolId.Paint;
        public bool HasGesture { get; private set; }
        public bool ConsumesWheel => false;
        public int Version { get; private set; }
        public void Tick(ToolContext context, in EditorInputFrame input)
        {
            if (input.PrimaryReleased) Cancel();
            if (!input.PointerAllowed) return;
            if (input.PrimaryPressed) { context.BeginStroke(); painted.Clear(); HasGesture = true; Version++; }
            if (!HasGesture || !(input.PrimaryPressed || input.PrimaryHeld) || !context.TryHovered(out var record)) return;
            if (input.PrimaryPressed && input.Shift) PaintConnected(context, record);
            else if (painted.Add(record)) context.Paint(record);
        }
        void PaintConnected(ToolContext context, BlockRecord start)
        {
            discovered.Clear(); flood.Clear(); discovered.Add(start); flood.Add(start);
            var reader = new DraftReader(context.Session.Draft, context.Content);
            const int limit = 65536;
            for (int i = 0; i < flood.Count; i++)
            {
                var block = flood[i];
                foreach (var offset in context.Content.Footprints.Get(block.Block, block.Orient).Cells)
                {
                    int3 cell = block.Origin + offset;
                    if (!reader.TryGetBlockAt(cell, out var owner) || !owner.Equals(block)) continue;
                    for (int axis = 0; axis < 3; axis++)
                        for (int sign = -1; sign <= 1; sign += 2)
                        {
                            int3 adjacent = cell; adjacent[axis] += sign;
                            if (!reader.TryGetBlockAt(adjacent, out var found) || found.Block != start.Block || !discovered.Add(found)) continue;
                            if (flood.Count >= limit) return; // No partial paint when the flood exceeds its limit.
                            flood.Add(found);
                        }
                }
            }
            foreach (var block in flood) if (painted.Add(block)) context.Paint(block);
        }
        public void BuildPreview(ToolContext context, PreviewBuffer preview) => preview.Clear();
        public bool Cancel()
        { if (!HasGesture) return false; HasGesture = false; painted.Clear(); Version++; return true; }
    }
}
