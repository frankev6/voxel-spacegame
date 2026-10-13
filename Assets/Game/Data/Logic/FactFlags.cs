namespace Game.Data.Logic
{
    /// <summary>Public narrative contract (SPEC-40 §4.2); later task implementations are explicitly labelled.</summary>
    [System.Flags] public enum FactFlags : byte
    {
        None = 0,
        WrittenByCode = 1,   // validator: counts as written (e.g. debug_menus set by the debugger)
        ReadByCode = 2,      // validator: counts as read (a C# reader exists; name it in `note`)
        Journal = 4,         // shown in the Ship Log journal when non-zero (SPEC-42 §4.9); counts as read
        Debug = 8            // debug-only content; excluded from release validation rules NAR-V003/V004
    }
}
