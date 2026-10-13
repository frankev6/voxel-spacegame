namespace Game.Galaxy.Travel
{
    /// <summary>Implements SPEC-32 §4.4; append new values only.</summary>
    public enum TravelPhase : byte { Idle = 0, Spooling = 1, Transit = 2, Arriving = 3 }
}
