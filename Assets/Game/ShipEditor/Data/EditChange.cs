namespace Game.ShipEditor.Data
{
    /// <summary>Implements SPEC-51 §4.2. Editor contract.</summary>
    public struct EditChange
    {
        public EditChangeKind Kind;
        public int OpsApplied;
        public int Revision;
    }
}
