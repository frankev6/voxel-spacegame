#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Game.App.DevRuntime
{
    /// <summary>Implements SPEC-70 §4.</summary>
    public enum DevResultKind : byte { Ok = 0, Pending = 1, Failed = 2 }
}
#endif
