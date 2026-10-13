namespace Game.Galaxy.Travel
{
    /// <summary>Implements SPEC-32 §4.4; append new values only.</summary>
    public enum RouteStatus : byte { None = 0, Ok = 1, Blocked = 2, Unreachable = 3 }
}
