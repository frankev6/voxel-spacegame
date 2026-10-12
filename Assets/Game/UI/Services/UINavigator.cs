using System;
using System.Collections.Generic;
using Game.Core;
using Game.UI.Data;
using Game.UI.Views;

namespace Game.UI.Services
{
    /// <summary>Implements SPEC-60 §5.1/6 registration and prewarm. UI-004 owns stack operations.</summary>
    public sealed class UINavigator
    {
        readonly UIRoot root;
        readonly Dictionary<Type, UIScreen> registry = new Dictionary<Type, UIScreen>();
        readonly HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
        bool disposed;
        internal UINavigator(UIRoot root, IPauseService pause, IInputContextTarget contextTarget, IFrameClock clock)
        { this.root = root; } // UI-004 retains pause/context/clock when stack ownership starts.
        public Game.UI.Views.UIScreen Top { get => throw new System.NotImplementedException(); }                                // null when empty
        public int Count { get => throw new System.NotImplementedException(); }
        public bool WorldPointerBlocked { get => throw new System.NotImplementedException(); }
        public int LastStackChangeFrame { get => throw new System.NotImplementedException(); }
        public Game.Core.InputContext CurrentContext { get => throw new System.NotImplementedException(); }
        public bool IsPausing { get => throw new System.NotImplementedException(); }                                            // holds its token
        public event System.Action StackChanged { add => throw new System.NotImplementedException(); remove => throw new System.NotImplementedException(); }
        public void Register(UIScreen screen)
        {
            if (disposed) throw new ObjectDisposedException(nameof(UINavigator));
            if (screen == null) throw new ArgumentNullException(nameof(screen));
            if (string.IsNullOrWhiteSpace(screen.Key) || screen.Layer < UILayer.Hud || screen.Layer > UILayer.Modals)
                throw new InvalidOperationException("Invalid screen key or layer: " + screen.Key);
            if (registry.ContainsKey(screen.GetType()) || keys.Contains(screen.Key))
                throw new InvalidOperationException("Duplicate screen type or key: " + screen.Key);
            registry.Add(screen.GetType(), screen); keys.Add(screen.Key);
            if (root.ShouldPrewarm(screen.Key)) root.BuildScreen(screen);
        }                      // throws on duplicate type or key
        public T Get<T>() where T : UIScreen
        {
            if (disposed) throw new ObjectDisposedException(nameof(UINavigator));
            return registry.TryGetValue(typeof(T), out var screen) ? (T)screen
                : throw new InvalidOperationException("Screen not registered: " + typeof(T).Name);
        }                       // throws when not registered
        public bool IsOpen<T>() where T : Game.UI.Views.UIScreen => throw new System.NotImplementedException();
        public void Push(Game.UI.Views.UIScreen screen, object args = null) => throw new System.NotImplementedException();
        public T Push<T>(object args = null) where T : Game.UI.Views.UIScreen => throw new System.NotImplementedException();
        public bool Pop(Game.UI.Views.UIScreen screen) => throw new System.NotImplementedException();
        public void Toggle<T>() where T : Game.UI.Views.UIScreen => throw new System.NotImplementedException();
        public void Back() => throw new System.NotImplementedException();
        public void SetRoot(Game.UI.Views.UIScreen root, object args = null) => throw new System.NotImplementedException();
        public void CloseAllAboveRoot() => throw new System.NotImplementedException();                                          // pops every non-root screen
        internal void DisposeOwnedScreens()
        {
            if (disposed) return;
            disposed = true;
            // TODO(UI-004): pop the active stack and release the navigator pause token before clearing registrations.
            foreach (var screen in registry.Values) screen.Root?.RemoveFromHierarchy();
            registry.Clear(); keys.Clear();
        }
    }
}
