namespace Game.Data.Definitions
{
    /// <summary>Public narrative contract (SPEC-40 §4.2); later task implementations are explicitly labelled.</summary>
    [System.Serializable]
    public struct FactDeclaration
    {
        public string Name;                         // content-ID format, unique across all catalogs
        public Game.Data.Logic.FactKind Kind;
        public Game.Data.Logic.FactScope Scope;
        public Game.Data.Logic.FactFlags Flags;
        public int Default;                         // Bool: 0 or 1
        public int Min;                             // Int only; Min == Max == 0 means unbounded
        public int Max;
        public string[] FormerNames;                // renamed facts: values saved under these names load into this fact
        [UnityEngine.TextArea(1, 4)] public string Note;           // designer note; required when Flags has ReadByCode/WrittenByCode
        [UnityEngine.TextArea(1, 4)] public string JournalText;    // required when Flags has Journal; shown in the Ship Log
    }
}
