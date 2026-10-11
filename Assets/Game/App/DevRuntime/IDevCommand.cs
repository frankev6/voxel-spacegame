#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Game.App.DevRuntime
{
    /// <summary>One dev command. Implementations are stateless; state lives in DevContext/DevRuntimeHost. Implements SPEC-70 §4.</summary>
    public interface IDevCommand
    {
        string Name { get; }          // lowercase, one word: "teleport"
        string Usage { get; }         // "teleport x=<m> z=<m> [yaw=<deg>] | sx=<m> sz=<m> | near=<body> [offset=<m>]"
        string Summary { get; }       // one line for help
        DevCommandResult Execute(DevArgs args, DevContext ctx);
    }
}
#endif
