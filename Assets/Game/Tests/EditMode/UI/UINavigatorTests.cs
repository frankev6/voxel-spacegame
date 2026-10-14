using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Game.Core;
using Game.UI.Data;
using Game.UI.Services;
using Game.UI.Views;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Game.Tests.EditMode.UI
{
    /// <summary>SPEC-60 UI-004: detached views, real navigation and explicit pause/context ownership.</summary>
    public sealed class UINavigatorTests
    {
        sealed class FakePause : IPauseService
        {
            readonly HashSet<uint> active = new HashSet<uint>();
            uint next;
            public int Acquired;
            public PauseToken Acquire(string reason) { Acquired++; active.Add(++next); return new PauseToken(this, next); }
            public void Release(PauseToken token) => active.Remove(token.Id);
            public bool IsPaused => active.Count > 0;
            public int ActiveTokenCount => active.Count;
            public void GetActiveReasons(List<string> results) { results.Clear(); if (IsPaused) results.Add("UINavigator"); }
        }
        sealed class Target : IInputContextTarget { public InputContext Value; public void SetContext(InputContext context) => Value = context; }
        sealed class Clock : IFrameClock { public int FrameCount => 42; public float UnscaledTime => 1; }
        sealed class A { } sealed class B { } sealed class C { } sealed class D { }
        sealed class View<T> : UIScreen
        {
            public bool Pauses = true, RootFlag, HideHud;
            public InputContext Input = InputContext.Menu;
            public UILayer UiLayer = UILayer.Screens;
            public Action PushAction, PopAction;
            public Func<bool> BackAction;
            public int Pushes, Covers, Reveals;
            public override string Key => typeof(T).Name.ToLowerInvariant();
            public override UILayer Layer => UiLayer;
            public override bool IsRoot => RootFlag;
            public override bool PausesSimulation => Pauses;
            public override bool HidesHud => HideHud;
            public override InputContext Context => Input;
            protected override void OnBuild() { }
            public override void OnPush(object args) { Pushes++; PushAction?.Invoke(); }
            public override void OnPop() => PopAction?.Invoke();
            public override bool OnBack() => BackAction?.Invoke() ?? false;
            public override void OnCovered() => Covers++;
            public override void OnRevealed() => Reveals++;
            public void ClickClose() => Close();
        }
        FakePause pause; Target target; VisualElement hud; UINavigator nav;
        [SetUp] public void SetUp()
        { pause = new FakePause(); target = new Target(); hud = new VisualElement(); nav = new UINavigator(pause, target, new Clock(), hud); }
        [TearDown] public void TearDown() => nav.DisposeOwnedScreens();
        View<T> Register<T>(bool pausing = true, bool root = false, InputContext input = InputContext.Menu, UILayer layer = UILayer.Screens)
        {
            var view = new View<T> { Pauses = pausing, RootFlag = root, Input = input, UiLayer = layer };
            nav.Register(view); return view;
        }
        [Test] public void Push_PausingScreen_AcquiresOneToken()
        { nav.Push(Register<A>()); Assert.AreEqual(1, pause.ActiveTokenCount); Assert.IsTrue(nav.IsPausing); }
        [Test] public void Pop_LastPausingScreen_ReleasesToken()
        { var a = Register<A>(); nav.Push(a); Assert.IsTrue(nav.Pop(a)); Assert.IsFalse(pause.IsPaused); Assert.IsFalse(nav.IsPausing); }
        [Test] public void Pop_ViaCloseOnCoveredScreen_PopsItAndAbove()
        {
            var a = Register<A>(); var b = Register<B>(); var c = Register<C>();
            nav.Push(a); nav.Push(b); nav.Push(c); a.ClickClose();
            Assert.AreEqual(0, nav.Count); Assert.IsFalse(a.IsOpen || b.IsOpen || c.IsOpen); Assert.IsFalse(pause.IsPaused);
        }
        [Test] public void Back_ThreeScreens_PopsOnlyTop()
        {
            var a = Register<A>(); var b = Register<B>(); var c = Register<C>();
            nav.Push(a); nav.Push(b); nav.Push(c);
            c.BackAction = () => { nav.Pop(c); Assert.IsTrue(c.IsOpen, "Back mutations are deferred"); return true; };
            nav.Back(); Assert.AreEqual(2, nav.Count); Assert.AreSame(b, nav.Top); Assert.IsFalse(c.IsOpen);
        }
        [Test] public void Push_ScreenBuiltEarlier_DrawsAboveScreensBelowIt()
        {
            // QA-001: layer children draw in sibling order; a confirm built before the pause menu rendered beneath it.
            var pauseMenu = Register<A>(layer: UILayer.Modals); var confirm = Register<B>(layer: UILayer.Modals);
            nav.Push(confirm); nav.Pop(confirm); nav.Push(pauseMenu);
            var layer = new VisualElement(); layer.Add(confirm.Root); layer.Add(pauseMenu.Root);
            nav.Push(confirm);
            Assert.Greater(layer.IndexOf(confirm.Root), layer.IndexOf(pauseMenu.Root), "the most recently pushed screen draws last");
        }
        [Test] public void Back_OnRoot_NeverPopsRoot()
        { var a = Register<A>(false, true, InputContext.Flight, UILayer.Hud); nav.SetRoot(a); nav.Back(); Assert.AreSame(a, nav.Top); Assert.IsTrue(a.IsOpen); }
        [Test] public void NestedPausingScreens_PopInner_StaysPaused()
        {
            var a = Register<A>(); var b = Register<B>(); nav.Push(a); nav.Push(b); nav.Pop(b);
            Assert.IsTrue(pause.IsPaused); Assert.AreEqual(1, pause.ActiveTokenCount); Assert.AreEqual(1, pause.Acquired);
        }
        [Test] public void Push_OnPushThrows_NoTokenAndNotInStack()
        {
            var a = Register<A>(); a.PushAction = () => throw new InvalidOperationException("push failure");
            LogAssert.Expect(LogType.Error, new Regex("\\[UI\\] InvalidOperationException: push failure"));
            nav.Push(a); Assert.AreEqual(0, nav.Count); Assert.IsFalse(a.IsOpen || pause.IsPaused);
            Assert.IsTrue(a.Root.ClassListContains(UIStyleClasses.IsHidden)); Assert.AreEqual(InputContext.None, target.Value);
        }
        [Test] public void SetRoot_ReplacesWholeStack_OnPopTopDown()
        {
            var order = new List<string>(); var a = Register<A>(false, true, InputContext.Flight, UILayer.Hud);
            var b = Register<B>(); var c = Register<C>(); var d = Register<D>(false, true, InputContext.Editor, UILayer.Hud);
            a.PopAction = () => order.Add("a"); b.PopAction = () => order.Add("b"); c.PopAction = () => order.Add("c");
            nav.SetRoot(a); nav.Push(b); nav.Push(c); nav.SetRoot(d);
            CollectionAssert.AreEqual(new[] { "c", "b", "a" }, order); Assert.AreSame(d, nav.Top); Assert.AreEqual(1, nav.Count);
            Assert.IsFalse(pause.IsPaused); Assert.AreEqual(InputContext.Editor, target.Value);
        }
        [Test] public void Push_AlreadyOpenBelowTop_PopsAbove()
        {
            var a = Register<A>(); var b = Register<B>(); nav.Push(a); nav.Push(b); nav.Push(a);
            Assert.AreEqual(1, nav.Count); Assert.AreSame(a, nav.Top); Assert.IsFalse(b.IsOpen); Assert.AreEqual(2, a.Pushes);
        }
        [Test] public void Pop_CalledInsideOnPop_IsDeferred()
        {
            var a = Register<A>(); var b = Register<B>(); var order = new List<string>();
            a.PopAction = () => order.Add("a");
            b.PopAction = () => { order.Add("b"); Assert.IsTrue(nav.Pop(a)); Assert.IsTrue(a.IsOpen); };
            nav.Push(a); nav.Push(b); nav.Pop(b);
            CollectionAssert.AreEqual(new[] { "b", "a" }, order); Assert.AreEqual(0, nav.Count); Assert.IsFalse(pause.IsPaused);
        }
        [Test] public void Push_RootScreen_Throws()
        { var a = Register<A>(false, true); var error = Assert.Throws<InvalidOperationException>(() => nav.Push(a)); StringAssert.Contains("use SetRoot", error.Message); }
        [Test] public void Sync_HidesHudOnlyWhileHidesHudScreenInStack()
        {
            var a = Register<A>(false, true, InputContext.Flight, UILayer.Hud); var b = Register<B>(); var c = Register<C>();
            b.HideHud = c.HideHud = true; nav.SetRoot(a); nav.Push(b); nav.Push(c); nav.Pop(c);
            Assert.IsTrue(hud.ClassListContains(UIStyleClasses.IsHidden)); nav.Pop(b); Assert.IsFalse(hud.ClassListContains(UIStyleClasses.IsHidden));
        }
        [Test] public void Sync_CoversLowerScreensLayerEntries()
        {
            var a = Register<A>(); var b = Register<B>(); var c = Register<C>(true, false, InputContext.Menu, UILayer.Modals);
            nav.Push(a); nav.Push(b); nav.Push(c);
            Assert.IsTrue(a.Root.ClassListContains(UIStyleClasses.IsCovered)); Assert.AreEqual(1, a.Covers);
            Assert.IsFalse(b.Root.ClassListContains(UIStyleClasses.IsCovered)); Assert.IsFalse(c.Root.ClassListContains(UIStyleClasses.IsCovered));
            nav.Pop(b); Assert.AreEqual(1, a.Reveals); Assert.IsFalse(a.Root.ClassListContains(UIStyleClasses.IsCovered));
        }
        [Test] public void Sync_ZeroAllocation()
        {
            nav.Push(Register<A>()); nav.Push(Register<B>());
            for (int i = 0; i < 100; i++) nav.Sync();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) nav.Sync();
            Assert.AreEqual(0, GC.GetAllocatedBytesForCurrentThread() - before);
        }
        [Test] public void Context_FollowsTopScreen_NoneWhenEmpty()
        {
            var a = Register<A>(false, false, InputContext.Flight); var b = Register<B>(true, false, InputContext.Menu);
            nav.Push(a); Assert.AreEqual(InputContext.Flight, target.Value); nav.Push(b); Assert.AreEqual(InputContext.Menu, nav.CurrentContext);
            nav.Pop(b); Assert.AreEqual(InputContext.Flight, target.Value); nav.Pop(a); Assert.AreEqual(InputContext.None, target.Value);
            Assert.AreEqual(42, nav.LastStackChangeFrame);
        }
    }
}
