using Game.ShipEditor.Data;
using Game.ShipEditor.Services;

namespace Game.ShipEditor.Tools
{
    internal sealed class EyedropperTool : IEditorTool
    {
        public ToolId Id => ToolId.Eyedropper;
        public bool HasGesture => false;
        public bool ConsumesWheel => false;
        public int Version => 0;
        public void Tick(ToolContext context, in EditorInputFrame input)
        { if (input.PointerAllowed && input.PrimaryPressed && context.TryHovered(out var record)) context.Sample(record); }
        public void BuildPreview(ToolContext context, PreviewBuffer preview) => preview.Clear();
        public bool Cancel() => false;
    }
}
