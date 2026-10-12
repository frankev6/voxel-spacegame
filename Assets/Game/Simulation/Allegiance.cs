using Unity.Entities;

namespace Game.Simulation
{
    /// <summary>Implements SPEC-21 weapon contract: Allegiance.</summary>
    public struct Allegiance : IComponentData, System.IEquatable<Allegiance>
    {
        public byte Faction;    // index into FactionRelations; 255 = none (asteroids, derelicts)
        public byte Team;       // encounter/fleet group; same Faction + same Team = wingmates
        public static Allegiance Player => new Allegiance { Faction = 0, Team = 0 };
        public static Allegiance None => new Allegiance { Faction = 255, Team = 0 };
        public bool Equals(Allegiance o) => Faction == o.Faction && Team == o.Team;
    }
}
