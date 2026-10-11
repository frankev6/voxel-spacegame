namespace Game.App.Boot
{
    /// <summary>Builds and disposes every service in order (SPEC-01 §5.2). Implements SPEC-01 §4.5 (shell; FND-005 adds the behaviour).</summary>
    internal sealed class GameComposition : System.IDisposable
    {
        internal readonly System.Collections.Generic.List<System.IDisposable> DisposeStack = new System.Collections.Generic.List<System.IDisposable>();
        public void Dispose() { throw new System.NotImplementedException("FND-005"); }
    }
}
