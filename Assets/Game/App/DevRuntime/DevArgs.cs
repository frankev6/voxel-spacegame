#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Game.App.DevRuntime
{
    /// <summary>Invariant, quoted command arguments (SPEC-70 §4). Invalid typed values throw a command-readable FormatException.</summary>
    public sealed class DevArgs
    {
        readonly Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        readonly List<string> keys = new List<string>();
        readonly List<string> positional = new List<string>();
        public IReadOnlyList<string> Keys => keys;
        public IReadOnlyList<string> Positional => positional;
        public static bool TryParse(string line, out string command, out DevArgs args, out string error)
        {
            command = null; args = null; error = null;
            var parsed = new DevArgs(); int i = 0;
            line ??= string.Empty;
            while (i < line.Length)
            {
                while (i < line.Length && char.IsWhiteSpace(line[i])) i++;
                if (i == line.Length) break;
                var token = new StringBuilder(); int equals = -1, quoteColumn = 0; bool quoted = false;
                while (i < line.Length && (quoted || !char.IsWhiteSpace(line[i])))
                {
                    char c = line[i++];
                    if (c == '"') { quoted = !quoted; if (quoted) quoteColumn = i; continue; }
                    if (c == '\\' && i < line.Length && (line[i] == '"' || line[i] == '\\')) { token.Append(line[i++]); continue; }
                    if (c == '=' && !quoted && equals < 0) equals = token.Length;
                    token.Append(c);
                }
                if (quoted) { error = "unterminated quote at column " + quoteColumn; return false; }
                string word = token.ToString();
                if (command == null)
                {
                    if (word.Length == 0 || equals >= 0) { error = "expected command"; return false; }
                    command = word.ToLowerInvariant();
                }
                else if (equals >= 0)
                {
                    string key = word.Substring(0, equals).ToLowerInvariant();
                    if (key.Length == 0) { error = "empty argument key"; return false; }
                    if (parsed.values.ContainsKey(key)) { error = "duplicate argument '" + key + "'"; return false; }
                    parsed.values.Add(key, word.Substring(equals + 1)); parsed.keys.Add(key);
                }
                else parsed.positional.Add(word);
            }
            if (command == null) { error = "expected command"; return false; }
            args = parsed; return true;
        }
        public bool Has(string key) => values.ContainsKey(key);
        public string GetString(string key, string fallback) => values.TryGetValue(key, out var s) ? s : fallback;
        public bool TryGetString(string key, out string value) => values.TryGetValue(key, out value);
        public double GetDouble(string key, double fallback)
        {
            if (!values.TryGetValue(key, out var s)) return fallback;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && !double.IsNaN(v) && !double.IsInfinity(v)) return v;
            throw Bad(key, "finite number");
        }
        public long GetLong(string key, long fallback)
        {
            if (!values.TryGetValue(key, out var s)) return fallback;
            if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long v)) return v;
            throw Bad(key, "integer");
        }
        public ulong GetULong(string key, ulong fallback)
        {
            if (!values.TryGetValue(key, out var s)) return fallback;
            if (ulong.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong v)) return v;
            throw Bad(key, "unsigned integer");
        }
        public bool GetBool(string key, bool fallback)
        {
            if (!values.TryGetValue(key, out var s)) return fallback;
            if (s == "1") return true;
            if (s == "0") return false;
            if (bool.TryParse(s, out bool v)) return v;
            throw Bad(key, "true/false/1/0");
        }
        static FormatException Bad(string key, string type) => new FormatException("argument '" + key + "' expects " + type);
    }
}
#endif
