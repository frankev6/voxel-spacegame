using System;
using System.Collections.Generic;
using Game.Core;
using Game.UI.Data;
using Game.UI.Views;
using Unity.Profiling;
using UnityEngine.UIElements;

namespace Game.UI.Services
{
    /// <summary>Implements SPEC-60 §5.2/6: single owner of screen order, derived pause/context and deferred navigation.</summary>
    public sealed class UINavigator
    {
        enum Kind : byte { Push, Pop, Back, SetRoot, CloseAboveRoot }
        readonly struct PendingOp
        {
            internal readonly Kind Kind;
            internal readonly UIScreen Screen;
            internal readonly object Args;
            internal PendingOp(Kind kind, UIScreen screen, object args) { Kind = kind; Screen = screen; Args = args; }
        }
        static readonly ProfilerMarker SyncMarker = new ProfilerMarker(ProfilerCategory.Scripts, "UINavigator.Sync");
        readonly UIRoot root;
        internal UIRoot RootOwner => root;
        readonly IPauseService pause;
        readonly IInputContextTarget contextTarget;
        readonly IFrameClock clock;
        readonly VisualElement hud;
        readonly Dictionary<Type, UIScreen> registry = new Dictionary<Type, UIScreen>();
        readonly HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
        readonly List<UIScreen> registered = new List<UIScreen>();
        readonly List<UIScreen> stack = new List<UIScreen>(16);
        readonly Queue<PendingOp> pending = new Queue<PendingOp>(16);
        PauseToken token;
        bool holdsToken, hudHidden, mutating, draining, disposed, textEntry;
        InputContext lastContext;
        internal UINavigator(UIRoot root, IPauseService pause, IInputContextTarget contextTarget, IFrameClock clock)
            : this(pause, contextTarget, clock, root.GetLayer(UILayer.Hud)) { this.root = root; }
        // Detached views use the same stack logic in tests, without a second runtime navigation implementation.
        internal UINavigator(IPauseService pause, IInputContextTarget contextTarget, IFrameClock clock, VisualElement hud)
        {
            this.pause = pause ?? throw new ArgumentNullException(nameof(pause));
            this.contextTarget = contextTarget ?? throw new ArgumentNullException(nameof(contextTarget));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock)); this.hud = hud;
        }
        public UIScreen Top => stack.Count == 0 ? null : stack[stack.Count - 1];
        public int Count => stack.Count;
        /// <summary>ADR-0078: query a layer without exposing or allocating a copy of the navigation stack.</summary>
        public bool HasOpenLayer(UILayer layer)
        {
            for (int i = 0; i < stack.Count; i++) if (stack[i].Layer == layer) return true;
            return false;
        }
        public bool WorldPointerBlocked { get; private set; }
        public int LastStackChangeFrame { get; private set; }
        public InputContext CurrentContext => lastContext;
        public bool IsPausing => holdsToken;
        public event Action StackChanged;
        public void Register(UIScreen screen)
        {
            CheckAlive();
            if (screen == null) throw new ArgumentNullException(nameof(screen));
            if (string.IsNullOrWhiteSpace(screen.Key) || screen.Layer < UILayer.Hud || screen.Layer > UILayer.Modals)
                throw new InvalidOperationException("Invalid screen key or layer: " + screen.Key);
            if (registry.ContainsKey(screen.GetType()) || keys.Contains(screen.Key))
                throw new InvalidOperationException("Duplicate screen type or key: " + screen.Key);
            registry.Add(screen.GetType(), screen); keys.Add(screen.Key); registered.Add(screen);
            try { if (root != null && root.ShouldPrewarm(screen.Key)) Build(screen); }
            catch { registry.Remove(screen.GetType()); keys.Remove(screen.Key); registered.Remove(screen); throw; }
        }
        public T Get<T>() where T : UIScreen
        {
            CheckAlive(); return registry.TryGetValue(typeof(T), out var screen) ? (T)screen
                : throw new InvalidOperationException("Screen not registered: " + typeof(T).Name);
        }
        public bool IsOpen<T>() where T : UIScreen => registry.TryGetValue(typeof(T), out var screen) && screen.IsOpen;
        public void Push(UIScreen screen, object args = null) => Execute(new PendingOp(Kind.Push, screen, args));
        public T Push<T>(object args = null) where T : UIScreen { var screen = Get<T>(); Push(screen, args); return screen; }
        public bool Pop(UIScreen screen) => Execute(new PendingOp(Kind.Pop, screen, null));
        public void Toggle<T>() where T : UIScreen { var screen = Get<T>(); if (screen.IsOpen) Pop(screen); else Push(screen); }
        public void Back() => Execute(new PendingOp(Kind.Back, null, null));
        public void SetRoot(UIScreen screen, object args = null) => Execute(new PendingOp(Kind.SetRoot, screen, args));
        public void CloseAllAboveRoot() => Execute(new PendingOp(Kind.CloseAboveRoot, null, null));
        void CheckAlive() { if (disposed) throw new ObjectDisposedException(nameof(UINavigator)); }
        void RequireRegistered(UIScreen screen)
        {
            if (screen == null || !registry.TryGetValue(screen.GetType(), out var found) || !ReferenceEquals(screen, found))
                throw new InvalidOperationException("Screen not registered: " + (screen?.GetType().Name ?? "null"));
        }
        void Build(UIScreen screen)
        {
            if (screen.IsBuilt)
            {
                if (!ReferenceEquals(screen.Navigator, this)) throw new InvalidOperationException("Screen belongs to another navigator");
                return;
            }
            if (root != null) root.BuildScreen(screen);
            else
            {
                var element = new VisualElement { name = UINames.ScreenRoot(screen.Key), pickingMode = PickingMode.Ignore };
                element.AddToClassList(UIStyleClasses.IsHidden); screen.Build(this, element);
            }
        }
        bool Execute(in PendingOp op)
        {
            CheckAlive();
            if (mutating)
            {
                if (pending.Count >= 16) { NavigationLoop(); return false; }
                pending.Enqueue(op); return true;
            }
            bool changed = false, result = true, success = false;
            UIScreen focus = null; UISound? sound = null;
            mutating = true;
            try
            {
                switch (op.Kind)
                {
                    case Kind.Push:
                        RequireRegistered(op.Screen);
                        if (op.Screen.IsRoot) throw new InvalidOperationException("use SetRoot");
                        Activate(op.Screen, op.Args, ref changed, out focus);
                        if (focus != null && focus.Layer != UILayer.Hud) sound = UISound.Open;
                        break;
                    case Kind.SetRoot:
                        RequireRegistered(op.Screen);
                        if (!op.Screen.IsRoot) throw new InvalidOperationException("SetRoot requires a root screen");
                        RemoveFrom(0, ref changed);
                        Activate(op.Screen, op.Args, ref changed, out focus);
                        if (focus != null && focus.Layer != UILayer.Hud) sound = UISound.Open;
                        break;
                    case Kind.Pop:
                        result = Remove(op.Screen, ref changed);
                        if (result) { focus = Top; sound = UISound.Close; }
                        break;
                    case Kind.Back:
                        if (textEntry) { root?.Panel?.focusController?.focusedElement?.Blur(); break; }
                        if (Top == null) break;
                        bool consumed;
                        try { consumed = Top.OnBack(); }
                        catch (Exception e) { Log(e); consumed = true; }
                        if (!consumed && !Top.IsRoot && Remove(Top, ref changed)) { focus = Top; sound = UISound.Close; }
                        break;
                    case Kind.CloseAboveRoot:
                        int first = stack.Count > 0 && stack[0].IsRoot ? 1 : 0;
                        RemoveFrom(first, ref changed);
                        if (changed) { focus = Top; sound = UISound.Close; }
                        break;
                }
                success = true; return result;
            }
            finally
            {
                try
                {
                    if (changed) SyncCore();
                    if (success)
                    {
                        try { focus?.DefaultFocus?.Focus(); } catch (Exception e) { Log(e); }
                        if (sound.HasValue) root?.PlaySound(sound.Value);
                    }
                }
                finally { mutating = false; Drain(); }
            }
        }
        void Activate(UIScreen screen, object args, ref bool changed, out UIScreen focus)
        {
            focus = null; Build(screen);
            int index = stack.IndexOf(screen);
            if (index >= 0) RemoveFrom(index + 1, ref changed);
            else { stack.Add(screen); screen.IsOpen = true; screen.Root.RemoveFromClassList(UIStyleClasses.IsHidden); }
            changed = true;
            try { screen.OnPush(args); focus = screen; }
            catch (Exception e)
            {
                Log(e); stack.Remove(screen); screen.IsOpen = false;
                screen.Root.AddToClassList(UIStyleClasses.IsHidden); screen.Root.RemoveFromClassList(UIStyleClasses.IsCovered);
            }
        }
        bool Remove(UIScreen screen, ref bool changed)
        {
            int index = stack.IndexOf(screen); if (index < 0) return false;
            if (screen.IsRoot) { GameLog.Error("UI", "cannot pop root"); return false; }
            RemoveFrom(index, ref changed); return true;
        }
        void RemoveFrom(int first, ref bool changed)
        {
            for (int i = stack.Count - 1; i >= first; i--)
            {
                var screen = stack[i]; stack.RemoveAt(i); screen.IsOpen = false; changed = true;
                try { screen.OnPop(); } catch (Exception e) { Log(e); }
                screen.Root.AddToClassList(UIStyleClasses.IsHidden); screen.Root.RemoveFromClassList(UIStyleClasses.IsCovered);
            }
        }
        void Drain()
        {
            if (draining || disposed) return;
            draining = true;
            try
            {
                int count = 0;
                while (pending.Count > 0)
                {
                    if (++count > 16) { NavigationLoop(); break; }
                    var next = pending.Dequeue();
                    try { Execute(next); } catch (Exception e) { Log(e); }
                }
            }
            finally { draining = false; }
        }
        void NavigationLoop() { pending.Clear(); GameLog.Error("UI", "navigation loop"); }
        static void Log(Exception e) => GameLog.Error("UI", e.GetType().Name + ": " + e.Message);
        internal void SetTextEntry(bool value)
        {
            if (disposed || textEntry == value) return;
            textEntry = value; Sync();
        }
        internal void Sync()
        {
            CheckAlive(); bool ownsMutation = !mutating;
            if (ownsMutation) mutating = true;
            try { SyncCore(); }
            finally { if (ownsMutation) { mutating = false; Drain(); } }
        }
        void SyncCore()
        {
            using var marker = SyncMarker.Auto();
            bool wantPause = false, hide = false, blocked = false; int topScreens = -1;
            for (int i = 0; i < stack.Count; i++)
            {
                var screen = stack[i]; wantPause |= screen.PausesSimulation; hide |= screen.HidesHud;
                blocked |= screen.BlocksWorldPointer; if (screen.Layer == UILayer.Screens) topScreens = i;
            }
            if (wantPause && !holdsToken) { token = pause.Acquire("UINavigator"); holdsToken = true; }
            else if (!wantPause && holdsToken) { token.Dispose(); token = default; holdsToken = false; }
            var context = textEntry ? InputContext.TextEntry : Top?.Context ?? InputContext.None;
            if (context != lastContext) { contextTarget.SetContext(context); lastContext = context; }
            if (hide != hudHidden) { hud?.EnableInClassList(UIStyleClasses.IsHidden, hide); hudHidden = hide; }
            for (int i = 0; i < stack.Count; i++)
            {
                var screen = stack[i]; if (screen.Layer != UILayer.Screens) continue;
                bool covered = i != topScreens, wasCovered = screen.Root.ClassListContains(UIStyleClasses.IsCovered);
                if (covered == wasCovered) continue;
                screen.Root.EnableInClassList(UIStyleClasses.IsCovered, covered);
                try { if (covered) screen.OnCovered(); else screen.OnRevealed(); } catch (Exception e) { Log(e); }
            }
            WorldPointerBlocked = blocked; LastStackChangeFrame = clock.FrameCount;
            try { StackChanged?.Invoke(); } catch (Exception e) { Log(e); }
        }
        internal void DisposeOwnedScreens()
        {
            if (disposed) return;
            mutating = true;
            try
            {
                bool changed = false; RemoveFrom(0, ref changed); pending.Clear(); textEntry = false; SyncCore();
                for (int i = 0; i < registered.Count; i++) registered[i].Root?.RemoveFromHierarchy();
                registry.Clear(); registered.Clear(); keys.Clear(); StackChanged = null;
            }
            finally
            {
                token.Dispose(); token = default; holdsToken = false;
                pending.Clear(); disposed = true; mutating = false;
            }
        }
    }
}
