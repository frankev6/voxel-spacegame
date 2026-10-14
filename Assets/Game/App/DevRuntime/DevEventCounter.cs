#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Game.Core.Events;

namespace Game.App.DevRuntime
{
    /// <summary>Managed event counts. Later payload owners add their typed subscriptions (SPEC-70 Q13).</summary>
    public sealed class DevEventCounter : IDisposable
    {
        readonly Dictionary<string, long> counts = new Dictionary<string, long>(StringComparer.Ordinal);
        bool disposed;
        internal GameSaved LastGameSaved { get; private set; }
        internal GameLoaded LastGameLoaded { get; private set; }
        internal StarSystemEntered LastStarSystemEntered { get; private set; }
        public DevEventCounter()
        {
            foreach (string name in new[] { "ShipDestroyed", "StarSystemEntered", "BodyInteracted", "BodyDestroyed", "LaneJumped", "ItemAcquired", "FactChanged", "SessionReset", "GameSaved", "GameLoaded" }) counts.Add(name, 0);
            EventBus<SessionReset>.Subscribe(OnSessionReset);
            EventBus<GameSaved>.Subscribe(OnGameSaved);
            EventBus<GameLoaded>.Subscribe(OnGameLoaded);
            EventBus<StarSystemEntered>.Subscribe(OnStarSystemEntered);
        }
        internal Dictionary<string, long> Snapshot() => new Dictionary<string, long>(counts, StringComparer.Ordinal);
        public long Count(string name) => counts.TryGetValue(name, out long count) ? count : throw new ArgumentException("unknown event '" + name + "'");
        void OnSessionReset(SessionReset e) { counts[nameof(SessionReset)]++; }
        void OnGameSaved(GameSaved e) { LastGameSaved = e; counts[nameof(GameSaved)]++; }
        void OnGameLoaded(GameLoaded e) { LastGameLoaded = e; counts[nameof(GameLoaded)]++; }
        void OnStarSystemEntered(StarSystemEntered e) { LastStarSystemEntered = e; counts[nameof(StarSystemEntered)]++; }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            EventBus<SessionReset>.Unsubscribe(OnSessionReset);
            EventBus<GameSaved>.Unsubscribe(OnGameSaved);
            EventBus<GameLoaded>.Unsubscribe(OnGameLoaded);
            EventBus<StarSystemEntered>.Unsubscribe(OnStarSystemEntered);
        }
    }
}
#endif
