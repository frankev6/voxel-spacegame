namespace Game.Data.Blocks
{
    /// <summary>DATA-E041 range checks shared by the v1 modules' Validate overrides (SPEC-04 §4.6 row comments). NaN fails every check.</summary>
    internal static class ModuleChecks
    {
        internal static void Positive(Game.Data.Validation.ValidationReport r, string field, float v)
        { if (!(v > 0f)) r.Error("DATA-E041", "", field, $"{field} is {v}; it must be greater than 0."); }

        internal static void NonNegative(Game.Data.Validation.ValidationReport r, string field, float v)
        { if (!(v >= 0f)) r.Error("DATA-E041", "", field, $"{field} is {v}; it must not be negative."); }

        internal static void AtLeastOne(Game.Data.Validation.ValidationReport r, string field, int v)
        { if (v < 1) r.Error("DATA-E041", "", field, $"{field} is {v}; it must be at least 1."); }
    }
}
