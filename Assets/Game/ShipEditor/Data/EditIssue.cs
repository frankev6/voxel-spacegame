namespace Game.ShipEditor.Data
{
    /// <summary>Implements SPEC-51 §4.2. Editor contract.</summary>
    public struct EditIssue
    {
        public EditIssueKind Kind;
        public int Count;
        public Unity.Mathematics.int3 FocusCell;
        public bool IsError => (byte)Kind < 100;
    }
}
