using Game.ShipEditor.Data;
using Game.ShipEditor.Services;

namespace Game.ShipEditor.Tools
{
    /// <summary>Tools borrow one controller context; only the session writes the draft.</summary>
    internal interface IEditorTool
    {
        ToolId Id { get; }
        bool HasGesture { get; }
        bool ConsumesWheel { get; }
        int Version { get; }
        void Tick(ToolContext context, in EditorInputFrame input);
        void BuildPreview(ToolContext context, PreviewBuffer preview);
        bool Cancel();
    }
}
