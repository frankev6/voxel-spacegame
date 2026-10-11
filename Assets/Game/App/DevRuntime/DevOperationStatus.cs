#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Game.App.DevRuntime
{
    /// <summary>Implements SPEC-70 §4.</summary>
    public enum DevOperationStatus : byte { Running = 0, Succeeded = 1, Failed = 2 }
}
#endif
