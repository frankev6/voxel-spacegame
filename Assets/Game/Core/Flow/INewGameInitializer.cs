namespace Game.Core.Flow
{
    /// <summary>Seeds a model's initial state for a new game (SPEC-01 §5.6). Implements SPEC-02 §4.8.</summary>
    public interface INewGameInitializer { int NewGameOrder { get; } void InitializeNewGame(in NewGameArgs args); }
}
