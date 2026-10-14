#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Game.Core.Events;
using Game.Core;

namespace Game.App.DevRuntime
{
    /// <summary>Managed event counts. Later payload owners add their typed subscriptions (SPEC-70 Q13).</summary>
    public sealed class DevEventCounter : IDisposable
    {
        readonly Dictionary<string, long> counts = new Dictionary<string, long>(StringComparer.Ordinal);
        readonly Dictionary<ContentHash, long> defeats = new Dictionary<ContentHash, long>();
        bool disposed;
        internal GameSaved LastGameSaved { get; private set; }
        internal GameLoaded LastGameLoaded { get; private set; }
        internal StarSystemEntered LastStarSystemEntered { get; private set; }
        public DevEventCounter()
        {
            foreach (string name in new[] { "ShipDestroyed", "StarSystemEntered", "BodyInteracted", "BodyDestroyed", "LaneJumped", "ItemAcquired", "FactChanged", "SessionReset", "GameSaved", "GameLoaded", "EncounterDefeated" }) counts.Add(name, 0);
            EventBus<EncounterDefeated>.Subscribe(OnEncounterDefeated);
            EventBus<SessionReset>.Subscribe(OnSessionReset);
            EventBus<GameSaved>.Subscribe(OnGameSaved);
            EventBus<GameLoaded>.Subscribe(OnGameLoaded);
            EventBus<StarSystemEntered>.Subscribe(OnStarSystemEntered);
        }
        internal Dictionary<string, long> Snapshot() => new Dictionary<string, long>(counts, StringComparer.Ordinal);
        internal Dictionary<ContentHash, long> DefeatSnapshot() => new Dictionary<ContentHash, long>(defeats);
        internal long DefeatCount(ContentHash encounter) => defeats.TryGetValue(encounter, out long count) ? count : 0;
        void OnEncounterDefeated(EncounterDefeated value)
        { counts[nameof(EncounterDefeated)]++; defeats[value.Encounter] = DefeatCount(value.Encounter) + 1; }
        public long Count(string name) => counts.TryGetValue(name, out long count) ? count : throw new ArgumentException("unknown event '" + name + "'");
        void OnSessionReset(SessionReset e) { counts[nameof(SessionReset)]++; }
        void OnGameSaved(GameSaved e) { LastGameSaved = e; counts[nameof(GameSaved)]++; }
        void OnGameLoaded(GameLoaded e) { LastGameLoaded = e; counts[nameof(GameLoaded)]++; }
        void OnStarSystemEntered(StarSystemEntered e) { LastStarSystemEntered = e; counts[nameof(StarSystemEntered)]++; }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            EventBus<EncounterDefeated>.Unsubscribe(OnEncounterDefeated);
            EventBus<SessionReset>.Unsubscribe(OnSessionReset);
            EventBus<GameSaved>.Unsubscribe(OnGameSaved);
            EventBus<GameLoaded>.Unsubscribe(OnGameLoaded);
            EventBus<StarSystemEntered>.Unsubscribe(OnStarSystemEntered);
        }
    }
}
#endif
