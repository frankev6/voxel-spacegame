namespace Game.Editor.Audio
{
    /// <summary>SPEC-64 §4.4. Only owner-confirmed clips may ship in release builds.</summary>
    public enum ProvenanceStatus { Unverified = 0, Confirmed = 1, Blocked = 2 }
}
