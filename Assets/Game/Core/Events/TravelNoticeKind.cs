namespace Game.Core.Events
{
    /// <summary>SPEC-32 §4.1 managed travel notice kind; append new values only.</summary>
    public enum TravelNoticeKind : byte { JumpRefused = 0, JumpAborted = 1, DestinationReached = 2, RouteBlocked = 3, RouteRestored = 4 }
}
