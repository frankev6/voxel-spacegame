using Unity.Entities;

namespace Game.Simulation.Commands
{
    /// <summary>Co-op spike (ADR-0116, SPEC-90 §4): on the host, a ship root flown by the guest in <see cref="Slot"/>. Never carries PlayerShip,
    /// so single-player queries keep their meaning.</summary>
    public struct RemotePilot : IComponentData { public byte Slot; }

    /// <summary>The guest's newest sampled command, same fields as <see cref="PlayerCommand"/>. Only writer: Game.Net's receive path.</summary>
    public struct RemotePilotInput : IComponentData
    {
        public PlayerCommand Command;
        public uint Sequence;          // guest frame sequence of the newest command; 0 = none yet
    }

    /// <summary>Edge counters already turned into ShipCommandEdges for this pilot. Only writer: RemotePilotApplySystem.</summary>
    public struct RemotePilotEdgeConsumed : IComponentData
    {
        public PlayerEdgeCounters Value;
        public byte Initialized;
    }
}
