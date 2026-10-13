namespace Game.Narrative.Dialog
{
    /// <summary>Node visit counts (Yarn "visited" semantics). Saved in DLGS. Implements SPEC-41 §4.7.</summary>
    public sealed class DialogMemory
    {
        public int GetVisitCount(string node) => throw new System.NotImplementedException("DLG-006");                 // 0 if never completed
        public void IncrementVisit(string node) => throw new System.NotImplementedException("DLG-006");
        public int GetRaw(string key) => throw new System.NotImplementedException("DLG-006");                         // Yarn internal variables ($Yarn.Internal.*), Yarn path
        public void SetRaw(string key, int value) => throw new System.NotImplementedException("DLG-006");
        public void Clear() => throw new System.NotImplementedException("DLG-006");
        public void CopySorted(System.Collections.Generic.List<(string key, int value)> into) => throw new System.NotImplementedException("DLG-006");   // ordinal by key
        // TODO(DLG-006): implement SPEC-41 service, command lookup and visit memory behavior.
    }
}
