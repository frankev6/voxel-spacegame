using System.Collections.Generic;
using Game.Data.Definitions;

namespace Game.Data.Validation
{
    /// <summary>Shared SPEC-50 row/number checks; definitions supply their owning code and serialized field path.</summary>
    internal static class EconomyDefinitionValidation
    {
        internal static void LootEntries(IReadOnlyList<LootEntry> entries, ValidationScope scope, string field, string code)
        {
            if (entries == null) { scope.Error(code, field, "Loot entry list is null."); return; }
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                string path = field + "[" + i + "]";
                if (entry.item == null) scope.Error(code, path + ".item", "Loot item is not set.");
                if (entry.min < 1) scope.Error(code, path + ".min", "Loot minimum must be at least 1.");
                if (entry.max < entry.min || entry.max > 10000)
                    scope.Error(code, path + ".max", "Loot maximum must be at least the minimum and at most 10000.");
                if (!float.IsFinite(entry.chancePercent) || entry.chancePercent <= 0 || entry.chancePercent > 100)
                    scope.Error(code, path + ".chancePercent", "Loot chance must be finite and within (0, 100].");
            }
        }

        internal static void FloatRange(ValidationScope scope, string code, string field, float value, float minimum, float maximum, bool exclusiveMinimum = false)
        {
            if (!float.IsFinite(value) || (exclusiveMinimum ? value <= minimum : value < minimum) || value > maximum)
                scope.Error(code, field, $"{field} must be finite and within {(exclusiveMinimum ? "(" : "[")}{minimum}, {maximum}].");
        }

        internal static void Positive(ValidationScope scope, string field, int value)
        {
            if (value < 1) scope.Error("ECO-E005", field, "Economy cap must be positive.");
        }
    }
}
