namespace Game.ShipEditor.Data
{
    /// <summary>Implements SPEC-51 §4.2. Editor contract.</summary>
    public struct EditSessionOptions
    {
        public bool Creative; // no inventory; commit saves a blueprint
        public EditTargetKind TargetKind;
        public string Title;  // toolbar title
    }
}
