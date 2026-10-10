namespace Game.Core
{
    /// <summary>Logging facade (conventions §6, SPEC-02 §4.4, §5.4). The only file allowed to call UnityEngine.Debug.Log*.</summary>
    public static class GameLog
    {
        static readonly System.Collections.Generic.Dictionary<string, LogLevel> levels = new(System.StringComparer.Ordinal);
        static readonly System.Collections.Generic.Dictionary<string, int> onceCounts = new(System.StringComparer.Ordinal);
        static LogLevel defaultLevel;
        static bool defaultLevelSet;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            levels.Clear();
            onceCounts.Clear();
            defaultLevelSet = false;
        }

        public static LogLevel DefaultMinimumLevel   // Info in Editor/development builds, Warning in release
        {
            get
            {
                if (!defaultLevelSet)
                {
                    defaultLevel = UnityEngine.Application.isEditor || UnityEngine.Debug.isDebugBuild ? LogLevel.Info : LogLevel.Warning;
                    defaultLevelSet = true;
                }
                return defaultLevel;
            }
            set { defaultLevel = value; defaultLevelSet = true; }
        }

        public static void SetMinimumLevel(string category, LogLevel level) { levels[category ?? string.Empty] = level; }
        public static LogLevel GetMinimumLevel(string category) =>
            levels.TryGetValue(category ?? string.Empty, out var level) ? level : DefaultMinimumLevel;

        public static void Info(string category, string message)
        {
            if (Enabled(category, LogLevel.Info)) UnityEngine.Debug.Log(Format(category, message));
        }

        public static void Warn(string category, string message)
        {
            if (Enabled(category, LogLevel.Warning)) UnityEngine.Debug.LogWarning(Format(category, message));
        }

        public static void Error(string category, string message)
        {
            if (Enabled(category, LogLevel.Error)) UnityEngine.Debug.LogError(Format(category, message));
        }

        public static void Exception(string category, System.Exception exception, string context)
        {
            UnityEngine.Debug.LogError(Format(category, context + ": " + exception.GetType().Name + ": " + exception.Message));
            UnityEngine.Debug.LogException(exception);
        }

        public static bool WarnOnce(string key, string category, string message)   // true if this call logged
        {
            if (CountOnce(key) != 1) return false;
            Warn(category, message);
            return true;
        }

        public static bool ErrorOnce(string key, string category, string message)
        {
            if (CountOnce(key) != 1) return false;
            Error(category, message);
            return true;
        }

        public static int GetOnceCount(string key) =>   // calls with this key since the last reset
            onceCounts.TryGetValue(key ?? "<null>", out var count) ? count : 0;

        public static void ResetOnceKeys() { onceCounts.Clear(); }

        static bool Enabled(string category, LogLevel level) => level >= GetMinimumLevel(category);
        static string Format(string category, string message) => "[" + category + "] " + message;

        static int CountOnce(string key)
        {
            key ??= "<null>";
            onceCounts.TryGetValue(key, out var count);
            onceCounts[key] = ++count;
            return count;
        }

        /// <summary>Burst-callable logging. Errors and one-off warnings only; never per frame.</summary>
        public static class Burst
        {
            public static void Warn(in Unity.Collections.FixedString128Bytes message) { UnityEngine.Debug.LogWarning(message); }
            public static void Error(in Unity.Collections.FixedString128Bytes message) { UnityEngine.Debug.LogError(message); }
        }
    }
}
