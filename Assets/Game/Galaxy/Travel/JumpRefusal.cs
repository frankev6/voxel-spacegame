namespace Game.Galaxy.Travel
{
    /// <summary>Implements SPEC-32 §4.4; append new values only.</summary>
    public enum JumpRefusal : byte { None = 0, NoGalaxy = 1, NotInWorld = 2, Busy = 3, NoPlayerShip = 4, Docked = 5, UnknownTarget = 6, SameStarSystem = 7, NoLane = 8, LaneLocked = 9, NotEnoughFuel = 10, InCombat = 11, TooClose = 12 }
}
