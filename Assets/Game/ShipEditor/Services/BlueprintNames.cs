using System;
using System.Globalization;
using System.Text;

namespace Game.ShipEditor.Services
{
    /// <summary>SPEC-51 §7: bounded portable blueprint IDs and collision suffixes.</summary>
    public static class BlueprintNames
    {
        public static string Sanitize(string name)
        {
            var result = new StringBuilder(48);
            foreach (char raw in name ?? "")
            {
                char c = raw >= 'A' && raw <= 'Z' ? (char)(raw + ('a' - 'A')) : raw;
                if (!(c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '-' || c == '_')) c = '_';
                if (c == '_' && (result.Length == 0 || result[result.Length - 1] == '_')) continue;
                if (result.Length == 48) break;
                result.Append(c);
            }
            string id = result.ToString().Trim('_', '-');
            return id.Length == 0 ? "blueprint" : id;
        }
        public static string Unique(string name, Func<string, bool> exists)
        {
            if (exists == null) throw new ArgumentNullException(nameof(exists));
            string id = Sanitize(name); if (!exists(id)) return id;
            for (int index = 2; ; index = checked(index + 1))
            {
                string suffix = "_" + index.ToString(CultureInfo.InvariantCulture);
                string candidate = id.Substring(0, Math.Min(id.Length, 48 - suffix.Length)) + suffix;
                if (!exists(candidate)) return candidate;
            }
        }
        public static bool IsId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 48) return false;
            foreach (char c in id) if (!(c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '-' || c == '_')) return false;
            return true;
        }
    }
}
