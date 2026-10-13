using UnityEngine;

namespace Game.Data.Definitions
{
    /// <summary>Role of a body inside a star system. Implements SPEC-30 §4.2. Add values at the end only.</summary>
    public enum BodyRole : byte { None = 0, Star = 1, Planet = 2, Moon = 3, Station = 4, Belt = 5, Asteroid = 6, LaneGate = 7, PointOfInterest = 8, StorySpawn = 9 }
}
