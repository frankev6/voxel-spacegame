using Unity.Entities;

namespace Game.Simulation.Flight
{
    /// <summary>ADR-0123: the switch for experimental 3D flight, on player ships (FlightSetup). Only writer: the dev command
    /// `flight3d` (SPEC-70 §5.4). Flight3DModeSystem turns it into <see cref="Game.Data.Components.Flight3D"/>: on at once, off once
    /// the ship has levelled.</summary>
    public struct Flight3DRequest : IComponentData
    {
        public byte On;
    }
}
