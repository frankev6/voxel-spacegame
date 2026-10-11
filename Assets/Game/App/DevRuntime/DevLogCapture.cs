#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;

namespace Game.App.DevRuntime
{
    /// <summary>Thread-safe lifetime log counts and a bounded 200-message history (SPEC-70 DEV-003).</summary>
    public sealed class DevLogCapture : MonoBehaviour
    {
        readonly object sync = new object();
        readonly Queue<string> messages = new Queue<string>(200);
        long errors, exceptions, warnings;
        public long Errors { get { lock (sync) return errors; } }
        public long Exceptions { get { lock (sync) return exceptions; } }
        public long Warnings { get { lock (sync) return warnings; } }
        public string[] LastMessages { get { lock (sync) return messages.ToArray(); } }
        void OnEnable() { Application.logMessageReceivedThreaded += Capture; }
        void OnDisable() { Application.logMessageReceivedThreaded -= Capture; }
        void Capture(string message, string stackTrace, LogType type)
        {
            lock (sync)
            {
                if (type == LogType.Error || type == LogType.Assert) errors++;
                else if (type == LogType.Exception) exceptions++;
                else if (type == LogType.Warning) warnings++;
                if (messages.Count == 200) messages.Dequeue();
                messages.Enqueue(type + ": " + message);
            }
        }
    }
}
#endif
