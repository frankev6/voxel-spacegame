using Game.Core.Net;
using Unity.Entities;

namespace Game.Simulation.Flight
{
    /// <summary>ADR-0123 (amended 2026-11-08): the world-wide switch for experimental 3D flight. Flight3DModeSystem creates it on, the
    /// owner's default; after that its only writer is the dev command `flight3d` (SPEC-70 §5.4). Every flying ship follows it: on enables
    /// <see cref="Game.Data.Components.Flight3D"/> at once, off once each ship has levelled. Solo only: in co-op it counts as off.</summary>
    public struct Flight3DMode : IComponentData
    {
        public byte On;

        /// <summary>True when 3D flight is wanted this tick: the switch is present and on, and the session is solo.</summary>
        public static bool Wanted(bool present, in Flight3DMode mode, SimRoleKind role) => present && mode.On != 0 && role == SimRoleKind.Solo;
    }
}
