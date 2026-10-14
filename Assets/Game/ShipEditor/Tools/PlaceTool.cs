using Game.ShipEditor.Data;
using Game.ShipEditor.Services;
using Unity.Mathematics;

namespace Game.ShipEditor.Tools
{
    internal sealed class PlaceTool : IEditorTool
    {
        bool hasCell;
        int3 lastCell;
        public ToolId Id => ToolId.Place;
        public bool HasGesture { get; private set; }
        public bool ConsumesWheel => false;
        public int Version { get; private set; }
        public void Tick(ToolContext context, in EditorInputFrame input)
        {
            if (input.PrimaryReleased) Cancel();
            if (!input.PointerAllowed) return;
            if (input.SecondaryPressed) { context.BeginStroke(); context.RemoveHovered(Id); }
            if (input.PrimaryPressed && input.Alt)
            { if (context.TryHovered(out var block)) context.Sample(block); return; }
            if (input.PrimaryPressed) { context.BeginStroke(); HasGesture = true; hasCell = false; Version++; }
            if (HasGesture && (input.PrimaryPressed || input.PrimaryHeld) && context.Hover.Kind != DraftPicker.HitKind.None &&
                (!hasCell || !lastCell.Equals(context.Hover.PlacementCell)))
            { lastCell = context.Hover.PlacementCell; hasCell = true; context.Place(); }
        }
        public void BuildPreview(ToolContext context, PreviewBuffer preview) => context.PreviewPlacement(preview);
        public bool Cancel()
        { if (!HasGesture) return false; HasGesture = false; hasCell = false; Version++; return true; }
    }
}
