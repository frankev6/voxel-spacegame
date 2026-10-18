using Unity.Entities;

namespace Game.Simulation.Mechanisms
{
    /// <summary>On a Gate zone that is one end of a lane-gate portal (ADR-0119 decision 8): the other end's zone PersistentId.</summary>
    public struct PortalLink : IComponentData
    {
        public ulong Other;
        public float JamSeconds;        // a PortalJam action closes the portal for this long
        public uint JammedUntilTick;
    }

    /// <summary>Authority record of one body passing through a portal; applied on every machine.</summary>
    public struct PortalTransit
    {
        public Entity Body;
        public ulong From, To;
        public uint Tick;
    }
}
