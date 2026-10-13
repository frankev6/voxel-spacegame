namespace Game.Galaxy.Services
{
    /// <summary>Implements SPEC-31 §4.4. Append-only loader lifecycle.</summary>
    public enum LoaderState : byte { Idle = 0, Unloading = 1, Loading = 2, Ready = 3 }
}
