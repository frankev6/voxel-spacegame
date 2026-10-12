namespace Game.UI.Services
{
    /// <summary>Implements SPEC-60 §6. Contract shell; implementation follows in the owning UI task.</summary>
    public sealed class UINavigator
    {
        public Game.UI.Views.UIScreen Top { get => throw new System.NotImplementedException(); }                                // null when empty
        public int Count { get => throw new System.NotImplementedException(); }
        public bool WorldPointerBlocked { get => throw new System.NotImplementedException(); }
        public int LastStackChangeFrame { get => throw new System.NotImplementedException(); }
        public Game.Core.InputContext CurrentContext { get => throw new System.NotImplementedException(); }
        public bool IsPausing { get => throw new System.NotImplementedException(); }                                            // holds its token
        public event System.Action StackChanged { add => throw new System.NotImplementedException(); remove => throw new System.NotImplementedException(); }
        public void Register(Game.UI.Views.UIScreen screen) => throw new System.NotImplementedException();                      // throws on duplicate type or key
        public T Get<T>() where T : Game.UI.Views.UIScreen => throw new System.NotImplementedException();                       // throws when not registered
        public bool IsOpen<T>() where T : Game.UI.Views.UIScreen => throw new System.NotImplementedException();
        public void Push(Game.UI.Views.UIScreen screen, object args = null) => throw new System.NotImplementedException();
        public T Push<T>(object args = null) where T : Game.UI.Views.UIScreen => throw new System.NotImplementedException();
        public bool Pop(Game.UI.Views.UIScreen screen) => throw new System.NotImplementedException();
        public void Toggle<T>() where T : Game.UI.Views.UIScreen => throw new System.NotImplementedException();
        public void Back() => throw new System.NotImplementedException();
        public void SetRoot(Game.UI.Views.UIScreen root, object args = null) => throw new System.NotImplementedException();
        public void CloseAllAboveRoot() => throw new System.NotImplementedException();                                          // pops every non-root screen
    }
}
