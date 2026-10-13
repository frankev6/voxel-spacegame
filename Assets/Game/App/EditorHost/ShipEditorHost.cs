namespace Game.App.EditorHost
{
    /// <summary>Implements SPEC-51 §6. Contract shell; no host or scene object is created in EDT-001.</summary>
    public sealed class ShipEditorHost : UnityEngine.MonoBehaviour
    {
        // TODO(EDT-022): compose the existing EditorInputSampler, presenter, session and renderers.
        public static ShipEditorHost Create(Game.ShipEditor.Services.ShipEditorEntryArgs args, EditorHostServices services)
            => throw new System.NotImplementedException("EDT-022");
    }
}
