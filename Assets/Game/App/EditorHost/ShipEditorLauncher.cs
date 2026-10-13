namespace Game.App.EditorHost
{
    /// <summary>Implements SPEC-51 §6. Contract shell for the canonical App launcher.</summary>
    public sealed class ShipEditorLauncher
    {
        // TODO(EDT-023): Lab/ship-edit scene flow, docking and dialog coordination.
        public void OpenLab() => throw new System.NotImplementedException("EDT-023");
        public bool EditPlayerShip(out string reason) => throw new System.NotImplementedException("EDT-023");
    }
}
