using System.Collections.Generic;
using Game.Data.Validation;

namespace Game.Data.Definitions
{
    /// <summary>Local SPEC-30 §5.9 field checks shared by world definitions; database bounds stay in Game.Editor.</summary>
    internal static class WorldDefinitionValidation
    {
        internal static void Require(ValidationScope scope, string code, string field, bool valid, string message)
        { if (!valid) scope.Error(code, field, message); }

        internal static void Positive(ValidationScope scope, string field, double value)
            => Require(scope, "GAL-E013", field, double.IsFinite(value) && value > 0, "Value must be finite and positive.");

        internal static void Range(ValidationScope scope, string field, double min, double max)
            => Require(scope, "GAL-E013", field, double.IsFinite(min) && double.IsFinite(max) && min > 0 && max >= min, "Range must be finite, positive and ordered.");

        internal static void Counts(ValidationScope scope, string field, int min, int max)
            => Require(scope, "GAL-E012", field, min >= 0 && max >= min, "Counts must be nonnegative and ordered.");

        internal static void Pool(ValidationScope scope, string field, IReadOnlyList<WeightedBodyTemplate> pool, BodyRole role, int minimum)
        {
            Require(scope, "GAL-E011", field, minimum <= 0 || (pool != null && pool.Count > 0), "A required pool must not be empty.");
            if (pool == null) return;
            for (int i = 0; i < pool.Count; i++)
            {
                var entry = pool[i]; string path = field + "[" + i + "]";
                Require(scope, "GAL-E010", path + ".template", entry.template != null && entry.template.Role == role, "Template must match the pool's body role.");
                Require(scope, "GAL-E011", path + ".weight", float.IsFinite(entry.weight) && entry.weight > 0, "Weight must be finite and positive.");
                Require(scope, "GAL-E012", path + ".maxCount", entry.maxCount >= 0, "Maximum count must be nonnegative; zero is unlimited.");
            }
        }

        internal static bool BodyKey(string key)
        {
            if (string.IsNullOrEmpty(key) || key[0] < 'a' || key[0] > 'z') return false;
            for (int i = 1; i < key.Length; i++)
            {
                char c = key[i];
                if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '_') return false;
            }
            return true;
        }
    }
}
