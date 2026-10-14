using System.Collections.Generic;
using Game.Core;
using Game.Core.Events;

namespace Game.Galaxy.Encounters
{
    /// <summary>Director-owned managed state shared with its ENCT participant; no ECS entity is serialized.</summary>
    internal sealed class EncounterSaveState
    {
        internal uint NextInstanceSerial = 1;
        internal readonly List<EncounterInstance> Active = new(16);
        internal readonly Dictionary<ulong, uint> Members = new(40);
        internal readonly List<PendingEncounter> Pending = new();
        internal readonly HashSet<ContentId> GloballyUsed = new();
        internal readonly HashSet<ContentId> ResolvedGloballyUsed = new();
        internal void Reset(SessionResetReason reason)
        {
            Active.Clear(); Members.Clear();
            // SaveService has already restored ENCT before publishing Loaded.
            if (reason == SessionResetReason.Loaded) return;
            NextInstanceSerial = 1; Pending.Clear(); GloballyUsed.Clear(); ResolvedGloballyUsed.Clear();
        }
    }
}
