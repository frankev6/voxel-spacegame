#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.App.DevRuntime
{
    /// <summary>Thread-safe lifetime log counts and a bounded 200-message history (SPEC-70 DEV-003).</summary>
    public sealed class DevLogCapture : MonoBehaviour
    {
        readonly object sync = new object();
        readonly Queue<(long sequence, string text)> messages = new Queue<(long, string)>(200);
        long errors, exceptions, warnings, sequence;
        public long Errors { get { lock (sync) return errors; } }
        public long Exceptions { get { lock (sync) return exceptions; } }
        public long Warnings { get { lock (sync) return warnings; } }
        public long MessageCount { get { lock (sync) return sequence; } }
        public string[] LastMessages
        {
            get { lock (sync) { var result = new string[messages.Count]; int i = 0; foreach (var message in messages) result[i++] = message.text; return result; } }
        }
        public bool ContainsSince(long after, string text)
        {
            lock (sync) foreach (var message in messages)
                if (message.sequence > after && message.text.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }
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
                messages.Enqueue((++sequence, type + ": " + message));
            }
        }
    }
}
#endif
