namespace Game.Persistence.Data
{
    /// <summary>Implements SPEC-05 §4.5.</summary>
    public enum AutosaveTrigger : byte { Timer = 0, SceneTransition = 1, StarSystemEntered = 2, Quit = 3 }
}
