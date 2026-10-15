using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Events;

namespace Game.App.Services
{
    /// <summary>Ref-counted pause (SPEC-01 §5.6, decision 10): the only writer of Time.timeScale. Main thread.
    /// PauseStateChanged is published once per change of IsPaused (0 → 1 and 1 → 0 tokens).</summary>
    public sealed class PauseService : IPauseService, IDisposable
    {
        readonly List<(uint id, string reason)> tokens = new List<(uint, string)>();
        uint nextId = 1;

        bool keepRunning;
        /// <summary>Co-op spike (SPEC-90 §3.2): while a session is live, tokens are still counted (menus, input contexts) but the world
        /// keeps running, because other players share it. Only Game.App's CoopSession sets it.</summary>
        public bool KeepRunning
        {
            get => keepRunning;
            set { keepRunning = value; UnityEngine.Time.timeScale = tokens.Count > 0 && !value ? 0f : 1f; }
        }

        public PauseToken Acquire(string reason)
        {
            if (string.IsNullOrEmpty(reason)) throw new ArgumentException("A pause reason is required.", nameof(reason));
            uint id = nextId++;
            tokens.Add((id, reason));
            if (tokens.Count == 1)
            {
                if (!keepRunning) UnityEngine.Time.timeScale = 0f;
                EventBus<PauseStateChanged>.Publish(new PauseStateChanged(true, tokens.Count));
            }
            return new PauseToken(this, id);
        }

        public bool IsPaused => tokens.Count > 0;
        public int ActiveTokenCount => tokens.Count;

        /// <summary>Idempotent per token; a token of another service or an unknown token is a no-op.</summary>
        public void Release(PauseToken token)
        {
            if (!token.IsValid || !token.Equals(new PauseToken(this, token.Id))) return;
            int i = tokens.FindIndex(t => t.id == token.Id);
            if (i < 0) return;
            tokens.RemoveAt(i);
            if (tokens.Count == 0)
            {
                UnityEngine.Time.timeScale = 1f;
                EventBus<PauseStateChanged>.Publish(new PauseStateChanged(false, 0));
            }
        }

        public void GetActiveReasons(List<string> results)
        {
            results.Clear();
            foreach (var t in tokens) results.Add(t.reason);
        }

        /// <summary>Releases everything and restores timeScale 1 (publishes the unpause if it was paused).</summary>
        public void Dispose()
        {
            bool wasPaused = tokens.Count > 0;
            tokens.Clear();
            UnityEngine.Time.timeScale = 1f;
            if (wasPaused) EventBus<PauseStateChanged>.Publish(new PauseStateChanged(false, 0));
        }
    }
}
