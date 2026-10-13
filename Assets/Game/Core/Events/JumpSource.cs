namespace Game.Core.Events
{
    /// <summary>SPEC-32 §4.1 jump request source; append new values only.</summary>
    public enum JumpSource : byte { Gate = 0, Narrative = 1, Dev = 2 }
}
