namespace Game.Core
{
    /// <summary>Logging facade (conventions §6, SPEC-02 §4.4, §5.4). The only file allowed to call UnityEngine.Debug.Log*.</summary>
    public static class GameLog
    {
        public static LogLevel DefaultMinimumLevel { get => throw new System.NotImplementedException("CORE-004"); set => throw new System.NotImplementedException("CORE-004"); }   // Info in Editor/development builds, Warning in release
        public static void SetMinimumLevel(string category, LogLevel level) { throw new System.NotImplementedException("CORE-004"); }
        public static LogLevel GetMinimumLevel(string category) { throw new System.NotImplementedException("CORE-004"); }
        public static void Info(string category, string message) { throw new System.NotImplementedException("CORE-004"); }
        public static void Warn(string category, string message) { throw new System.NotImplementedException("CORE-004"); }
        public static void Error(string category, string message) { throw new System.NotImplementedException("CORE-004"); }
        public static void Exception(string category, System.Exception exception, string context) { throw new System.NotImplementedException("CORE-004"); }
        public static bool WarnOnce(string key, string category, string message) { throw new System.NotImplementedException("CORE-004"); }   // true if this call logged
        public static bool ErrorOnce(string key, string category, string message) { throw new System.NotImplementedException("CORE-004"); }
        public static int GetOnceCount(string key) { throw new System.NotImplementedException("CORE-004"); }                                 // calls with this key since the last reset
        public static void ResetOnceKeys() { throw new System.NotImplementedException("CORE-004"); }

        /// <summary>Burst-callable logging. Errors and one-off warnings only; never per frame.</summary>
        public static class Burst
        {
            public static void Warn(in Unity.Collections.FixedString128Bytes message) { throw new System.NotImplementedException("CORE-004"); }
            public static void Error(in Unity.Collections.FixedString128Bytes message) { throw new System.NotImplementedException("CORE-004"); }
        }
    }
}
