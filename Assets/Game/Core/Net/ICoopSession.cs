using System.Collections.Generic;

namespace Game.Core.Net
{
    public enum CoopState : byte { Offline = 0, Hosting = 1, Connecting = 2, Joining = 3, Joined = 4, Failed = 5 }

    /// <summary>One connected player as the local machine sees it (SPEC-90 §6).</summary>
    public readonly struct CoopPeer
    {
        public readonly byte Slot; public readonly string Name; public readonly float RttMs; public readonly bool InWorld;
        public CoopPeer(byte slot, string name, float rttMs, bool inWorld) { Slot = slot; Name = name; RttMs = rttMs; InWorld = inWorld; }
    }

    /// <summary>Co-op session control (SPEC-90 §6). Implemented in Game.App; main thread only.</summary>
    public interface ICoopSession
    {
        CoopState State { get; }
        SimRoleKind Role { get; }
        string StatusText { get; }
        bool Host(ushort port);
        bool Join(string address, ushort port);
        void Leave(string reason);
        void GetPeers(List<CoopPeer> results);
    }
}
