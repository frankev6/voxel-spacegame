using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Validation;
using Game.Narrative.Dialog;
using Game.Presentation.Bridges;
using Game.Tests.EditMode.Data;
using Game.Tests.EditMode.Data.Logic;
using Game.Tests.EditMode.Narrative;
using Game.UI.Data;
using Game.UI.Services;
using Game.UI.ViewModels;
using UnityEngine;
using UnityEngine.UIElements;
using NUnit.Framework;

namespace Game.Tests.EditMode.Presentation
{
    /// <summary>Tiny detached navigation with the real service, presenter and compiled-table owner.</summary>
    internal sealed class DialogPresenterFixture : IDisposable
    {
        internal sealed class Runner : IDialogRunner
        {
            internal Action<IDialogRunnerHost> StartAction;
            internal int Selected = -1, Selections;
            IDialogRunnerHost host;
            public bool IsRunning { get; private set; }
            public bool HasNode(string node) => node == "start";
            public void Start(string node, IDialogRunnerHost value)
            { host = value; IsRunning = true; host.OnNodeStarted(node); if (StartAction == null) Stop(); else StartAction(host); }
            public void Continue() => Stop();
            public void Resume() => Stop();
            public void SelectOption(int id) { Selected = id; Selections++; Stop(); }
            public void Stop() { if (!IsRunning) return; IsRunning = false; host.OnNodeCompleted("start"); host.OnDialogComplete(); }
        }
        sealed class Pause : IPauseService
        {
            uint next; int count;
            public PauseToken Acquire(string reason) { count++; return new PauseToken(this, ++next); }
            public void Release(PauseToken token) => count--;
            public bool IsPaused => count > 0;
            public int ActiveTokenCount => count;
            public void GetActiveReasons(List<string> into) { into.Clear(); }
        }
        sealed class Target : IInputContextTarget { public void SetContext(InputContext context) { } }
        sealed class Clock : IFrameClock { public int FrameCount => 1; public float UnscaledTime => 0; }
        internal sealed class Sound : IUISoundSink
        {
            internal int Errors;
            public void Play(UISound sound) { if (sound == UISound.Error) Errors++; }
        }
        internal readonly Runner Script = new();
        internal readonly Sound Sounds = new();
        internal readonly DialogViewModel Model = new();
        internal readonly DialogService Service;
        internal readonly DialogPresenter Presenter;
        internal readonly UINavigator Navigator;
        internal readonly DialogServiceTests.Context Context;
        internal readonly Sprite Portrait = TestContentFactory.Icon(), Illustration = TestContentFactory.Icon();
        internal DialogPresenterFixture()
        {
            var definitions = FactCatalogTests.BaseContent();
            definitions.Add(TestContentFactory.Create<SpeakerDefinition>("speaker_nora", so =>
            { TestContentFactory.Set(so, "displayName", "NORA"); TestContentFactory.Set(so, "portrait", Portrait); TestContentFactory.Set(so, "nameStyleClass", "speaker--nora"); }));
            definitions.Add(TestContentFactory.Create<SpeakerDefinition>("speaker_captain", so => TestContentFactory.Set(so, "displayName", "Captain")));
            definitions.Add(TestContentFactory.Create<IllustrationDefinition>("illustration_known", so => TestContentFactory.Set(so, "image", Illustration)));
            definitions.Add(TestContentFactory.Create<IllustrationDefinition>("illustration_empty"));
            var content = FactCatalogTests.Content(definitions); var report = new ValidationReport("Dialog presenter fixture");
            var facts = FactCatalog.Build(content, RefResolverRegistry.CreateDefault(content), report);
            Assert.AreEqual(0, report.ErrorCount, report.ToText());
            Context = new DialogServiceTests.Context(facts);
            Service = new DialogService(Script, DialogServiceTests.Table(), Context, new DialogMemory());
            Navigator = new UINavigator(new Pause(), new Target(), new Clock(), new VisualElement());
            Presenter = new DialogPresenter(Service, content, Model, Navigator, Sounds);
        }
        internal void Start(Action<IDialogRunnerHost> action)
        { Script.StartAction = action; Assert.IsTrue(Service.TryStart(new DialogRequest(0, "start", default))); }
        internal static DialogLine Line(string text = "line", string portrait = "", string illustration = "", string speaker = "", string name = "")
            => new DialogLine(text, text, name, portrait, speaker, illustration, "start", false);
        public void Dispose() { Presenter.Dispose(); Navigator.DisposeOwnedScreens(); }
    }
}
