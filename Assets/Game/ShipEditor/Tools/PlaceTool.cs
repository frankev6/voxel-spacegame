using Game.ShipEditor.Data;
using Game.ShipEditor.Services;
using Unity.Mathematics;

namespace Game.ShipEditor.Tools
{
    internal sealed class PlaceTool : IEditorTool
    {
        // A held stroke never re-picks the draft: each placed block would become the next hit and the stroke would
        // grow a column toward the camera, one block per frame (QA-001). It stays on the first placement's plane.
        const int MaxStepsPerFrame = 64;
        int3 lastCell, planeCell, planeNormal;
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
            if (input.PrimaryPressed)
            {
                if (context.Hover.Kind == DraftPicker.HitKind.None) return;
                context.BeginStroke(); HasGesture = true; Version++;
                planeCell = lastCell = context.Hover.PlacementCell; planeNormal = context.Hover.Normal;
                context.Place(); return;
            }
            if (!HasGesture || !input.PrimaryHeld || !context.TryStrokePlaneCell(planeCell, planeNormal, out var cell) || cell.Equals(lastCell)) return;
            // Step one cell at a time so a fast drag leaves no gaps; every step attaches to the previous one.
            for (int i = 0; i < MaxStepsPerFrame && !lastCell.Equals(cell); i++)
            {
                int3 delta = cell - lastCell, size = math.abs(delta);
                int axis = size.x >= size.y && size.x >= size.z ? 0 : size.z >= size.y ? 2 : 1;
                lastCell[axis] += math.sign(delta[axis]);
                context.PlaceAt(lastCell, planeNormal);
            }
        }
        public void BuildPreview(ToolContext context, PreviewBuffer preview) => context.PreviewPlacement(preview);
        public bool Cancel()
        { if (!HasGesture) return false; HasGesture = false; Version++; return true; }
    }
}
