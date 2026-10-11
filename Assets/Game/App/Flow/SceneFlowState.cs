namespace Game.App.Flow
{
    /// <summary>Implements SPEC-01 §4.3.</summary>
    public enum SceneFlowState : byte { Idle = 0, Exiting = 1, Loading = 2, Entering = 3, Recovering = 4 }
}
