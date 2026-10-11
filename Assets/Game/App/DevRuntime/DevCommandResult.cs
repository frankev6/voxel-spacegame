#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Game.App.DevRuntime
{
    /// <summary>Result of IDevCommand.Execute. Implements SPEC-70 §4.</summary>
    public readonly struct DevCommandResult
    {
        public readonly DevResultKind Kind;
        public readonly string Text;          // human/agent readable, never null
        public readonly int OperationId;      // > 0 when Kind == Pending

        DevCommandResult(DevResultKind kind, string text, int operationId) { Kind = kind; Text = text ?? ""; OperationId = operationId; }

        public static DevCommandResult Ok(string text) => new DevCommandResult(DevResultKind.Ok, text, 0);
        public static DevCommandResult Fail(string text) => new DevCommandResult(DevResultKind.Failed, text, 0);
        /// <summary>A started multi-frame operation; <paramref name="operationId"/> must be greater than 0.</summary>
        public static DevCommandResult Pending(int operationId, string text)
        {
            if (operationId <= 0) throw new System.ArgumentOutOfRangeException(nameof(operationId), operationId, "operation ids start at 1");
            return new DevCommandResult(DevResultKind.Pending, text, operationId);
        }
    }
}
#endif
