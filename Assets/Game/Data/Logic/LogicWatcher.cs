namespace Game.Data.Logic
{
    /// <summary>SPEC-40 §6 canonical API shell, brought forward for SPEC-32 §4.7; NAR-020 owns its behavior.</summary>
    public sealed class LogicWatcher : System.IDisposable
    {
        public LogicWatcher(ICondition condition, ILogicContext context, System.Action<bool> onChanged)
            => throw new System.NotImplementedException("NAR-020");
        public bool Value => throw new System.NotImplementedException("NAR-020");
        public void Refresh() => throw new System.NotImplementedException("NAR-020");
        public void Dispose() => throw new System.NotImplementedException("NAR-020");
    }
}
