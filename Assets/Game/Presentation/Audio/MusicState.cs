namespace Game.Presentation.Audio
{
    /// <summary>Implements SPEC-64 §4.4.</summary>
    public enum MusicState : byte { Silent = 0, Menu = 1, Exploration = 2, Combat = 3, Editor = 4, Override = 5 }
}
