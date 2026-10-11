using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Game.Data.Definitions;

namespace Game.Data.Validation
{
    /// <summary>Content ID format and prefix rules (SPEC-04 §3 decisions 1–2, §4.3). Managed, main thread.</summary>
    public static class ContentIdRules
    {
        public const int MinLength = 3, MaxLength = 64;

        [Game.Core.StaticStateOk("immutable compiled regex")]
        static readonly Regex Format = new Regex("^[a-z][a-z0-9]*(_[a-z0-9]+)+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // Nearest [ContentIdPrefix] per definition type, built on first use; reset for Enter Play Mode without domain reload.
        static Dictionary<Type, string[]> prefixCache = new Dictionary<Type, string[]>();

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { prefixCache = new Dictionary<Type, string[]>(); }

        /// <summary>True if the ID is lowercase snake_case with at least one '_' and 3–64 characters (DATA-E002 otherwise).</summary>
        public static bool IsValidFormat(string id)
            => id != null && id.Length >= MinLength && id.Length <= MaxLength && Format.IsMatch(id);

        /// <summary>True if the ID starts with one of the prefixes declared by the nearest [ContentIdPrefix] of defType.
        /// A type without any declaration returns false with an empty <paramref name="allowed"/> (DATA-E003).</summary>
        public static bool HasAllowedPrefix(Type defType, string id, out string[] allowed)
        {
            allowed = PrefixesOf(defType);
            if (string.IsNullOrEmpty(id)) return false;
            foreach (var p in allowed)
                if (id.Length > p.Length && id.StartsWith(p, StringComparison.Ordinal)) return true;
            return false;
        }

        static string[] PrefixesOf(Type t)
        {
            if (t == null) return Array.Empty<string>();
            if (prefixCache.TryGetValue(t, out var cached)) return cached;
            var attr = t.GetCustomAttribute<ContentIdPrefixAttribute>(inherit: true);   // AttributeUsage Inherited: the nearest declaration wins
            var result = attr?.Prefixes ?? Array.Empty<string>();
            prefixCache[t] = result;
            return result;
        }
    }
}
