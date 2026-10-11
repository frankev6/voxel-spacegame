#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Game.App.DevRuntime
{
    /// <summary>A multi-frame command. Advanced once per frame by DevRuntimeHost.Update until not Running. Implements SPEC-70 §4.</summary>
    public interface IDevOperation
    {
        DevOperationStatus Status { get; }
        string Message { get; }               // progress or final result
        double TimeoutSeconds { get; }        // realtime; host fails the op when exceeded
        void Tick(DevContext ctx);
    }
}
#endif
