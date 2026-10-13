namespace Game.Core.Events
{
    /// <summary>SPEC-31 §4.1 entry reason, brought forward for VFX lifetime (ADR-0060).</summary>
    public enum StarSystemEntryReason : byte { NewGame = 0, Load = 1, Jump = 2, ReturnFromEditor = 3, DevSandbox = 4 }
}
