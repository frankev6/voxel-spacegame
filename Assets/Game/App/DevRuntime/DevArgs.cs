#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;

namespace Game.App.DevRuntime
{
    /// <summary>Parsed "key=value" arguments. Keys are lowercase. Values may be quoted with "..." (\" escapes a quote). Implements SPEC-70 §4.</summary>
    public sealed class DevArgs
    {
        public static bool TryParse(string line, out string command, out DevArgs args, out string error) { throw new System.NotImplementedException("DEV-003"); }
        public bool Has(string key) { throw new System.NotImplementedException("DEV-003"); }
        public string GetString(string key, string fallback) { throw new System.NotImplementedException("DEV-003"); }
        public bool TryGetString(string key, out string value) { throw new System.NotImplementedException("DEV-003"); }
        public double GetDouble(string key, double fallback) { throw new System.NotImplementedException("DEV-003"); }   // invariant culture
        public long GetLong(string key, long fallback) { throw new System.NotImplementedException("DEV-003"); }
        public ulong GetULong(string key, ulong fallback) { throw new System.NotImplementedException("DEV-003"); }
        public bool GetBool(string key, bool fallback) { throw new System.NotImplementedException("DEV-003"); }          // true/false/1/0
        public IReadOnlyList<string> Keys => throw new System.NotImplementedException("DEV-003");
        public IReadOnlyList<string> Positional => throw new System.NotImplementedException("DEV-003");         // tokens without '=' (e.g. "record start")
    }
}
#endif
