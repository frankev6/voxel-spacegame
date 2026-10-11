namespace Game.Presentation.Audio
{
    /// <summary>Implements SPEC-64 §4.4 (internal). One pooled voice.</summary>
    internal struct VoiceInfo { public int Slot; public byte Priority; public float Audibility; public double StartTime; public bool Active; }
}
