namespace Game.Data.Validation
{
    /// <summary>SPEC-33 finite-value and inclusive count-range checks; no runtime encounter behavior.</summary>
    internal static class EncounterDefinitionValidation
    {
        internal static void Nonnegative(ValidationScope scope, string code, string field, float value)
        {
            if (!float.IsFinite(value) || value < 0) scope.Error(code, field, "Value must be finite and nonnegative.");
        }
        internal static void Positive(ValidationScope scope, string field, float value)
        {
            if (!float.IsFinite(value) || value <= 0) scope.Error("ENC-E020", field, "Value must be finite and positive.");
        }
        internal static void Positive(ValidationScope scope, string field, int value)
        {
            if (value < 1) scope.Error("ENC-E020", field, "Value must be positive.");
        }
        internal static bool ShipRange(ValidationScope scope, string field, int minimum, int maximum)
        {
            if (minimum >= 1 && minimum <= maximum && maximum <= 10) return true;
            scope.Error("ENC-E012", field, "Ship counts must satisfy 1 <= minimum <= maximum <= 10.");
            return false;
        }
    }
}
