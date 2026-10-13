using System;
using System.Collections.Generic;
using Game.Core;
using Game.UI.Data;
using Game.UI.Views;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Services
{
    /// <summary>Implements SPEC-60 §5.1/5.10: owns one document, its layers, screen assets and root callbacks.</summary>
    public sealed class UIRoot : IDisposable, IWorldPointerGate
    {
        static readonly ProfilerMarker TickMarker = new ProfilerMarker(ProfilerCategory.Scripts, "UIRoot.Tick");
        static readonly ProfilerMarker BuildMarker = new ProfilerMarker(ProfilerCategory.Scripts, "UINavigator.Build");
        readonly UIDocument document;
        readonly IUISoundSink sound;
        readonly IFrameClock clock;
        readonly Dictionary<string, ScreenAsset> assets = new Dictionary<string, ScreenAsset>(StringComparer.Ordinal);
        readonly VisualElement[] layers = new VisualElement[6];
        Button lastHover;
        bool ownsDocument, disposed;
        public UIRoot(UIDocument document, UIConfig config, IPauseService pause, IInputContextTarget contextTarget,
            IUISoundSink sound, IFrameClock clock)
        {
            this.clock = clock; this.document = document; this.sound = sound ?? new NullUISoundSink();
            try
            {
                if (document == null || config == null || config.PanelSettings == null || config.RootUxml == null)
                    throw new InvalidOperationException("UIRoot requires a document, config, panel settings and root UXML");
                if (pause == null || contextTarget == null || clock == null)
                    throw new InvalidOperationException("UIRoot requires pause, input context and frame clock services");
                if (config.MaxVisibleToasts < 1 || config.Screens == null)
                    throw new InvalidOperationException("UIConfig requires MaxVisibleToasts >= 1 and a Screens array");
                for (int i = 0; i < config.Screens.Length; i++)
                {
                    var asset = config.Screens[i];
                    if (string.IsNullOrWhiteSpace(asset.Key) || asset.Uxml == null)
                        throw new InvalidOperationException("UIConfig screen '" + asset.Key + "' requires a nonempty key and UXML");
                    if (!assets.TryAdd(asset.Key, asset)) throw new InvalidOperationException("UIConfig duplicate screen key '" + asset.Key + "'");
                }
                ownsDocument = true;
                document.panelSettings = config.PanelSettings;
                document.visualTreeAsset = config.RootUxml;
                document.rootVisualElement.pickingMode = PickingMode.Ignore;
                RootElement = document.rootVisualElement.Q<VisualElement>("ui-root");
                if (RootElement == null || RootElement.childCount != layers.Length)
                    throw new InvalidOperationException("UIRoot requires #ui-root with exactly six layers");
                for (int i = 0; i < layers.Length; i++)
                {
                    string name = UINames.Layer((UILayer)i);
                    layers[i] = RootElement[i];
                    if (layers[i].name != name) throw new InvalidOperationException("UIRoot missing or out-of-order layer '" + name + "'");
                }
                Navigator = new UINavigator(this, pause, contextTarget, clock);
                Tooltips = new TooltipService(this, config); Notifications = new NotificationService(this, config, clock);
                Confirm = new ConfirmDialog(); Prompt = new TextPrompt();
                RootElement.RegisterCallback<ClickEvent>(OnClick);
                RootElement.RegisterCallback<PointerOverEvent>(OnPointerOver);
                RootElement.RegisterCallback<PointerOutEvent>(OnPointerOut);
                RootElement.RegisterCallback<FocusInEvent>(OnFocusIn, TrickleDown.TrickleDown);
                RootElement.RegisterCallback<FocusOutEvent>(OnFocusOut, TrickleDown.TrickleDown);
                UIConverters.RegisterAll();
                // TODO(UI-011): dialog registration.
                // TODO(UI-013): ApplyAccessibility; until then use the specified construction defaults.
                config.PanelSettings.scale = 1;
            }
            catch (Exception e)
            {
                Dispose();
                GameLog.Error("UI", e.Message);
                throw;
            }
        }
        public IPanel Panel => RootElement?.panel;
        public VisualElement RootElement { get; }
        public VisualElement GetLayer(UILayer layer)
        {
            if (disposed) throw new ObjectDisposedException(nameof(UIRoot));
            if ((uint)layer >= layers.Length) throw new ArgumentOutOfRangeException(nameof(layer));
            return layers[(int)layer];
        }
        public UINavigator Navigator { get; }
        public TooltipService Tooltips { get; }
        public NotificationService Notifications { get; }
        public ConfirmDialog Confirm { get; }
        public TextPrompt Prompt { get; }
        public bool ReduceMotion { get; private set; }
        public void Register(UIScreen screen)
        {
            if (disposed) throw new ObjectDisposedException(nameof(UIRoot));
            Navigator.Register(screen);
        }
        internal bool ShouldPrewarm(string key) => assets.TryGetValue(key, out var asset) && asset.Prewarm;
        internal void BuildScreen(UIScreen screen)
        {
            if (screen.IsBuilt) return;
            using (BuildMarker.Auto())
            {
                VisualElement tree = assets.TryGetValue(screen.Key, out var asset) ? asset.Uxml.Instantiate() : screen.CreateView();
                if (tree == null) throw new InvalidOperationException("UIConfig missing screen '" + screen.Key + "'");
                tree.name = UINames.ScreenRoot(screen.Key); tree.pickingMode = PickingMode.Ignore;
                tree.AddToClassList(UIStyleClasses.Screen); tree.AddToClassList(UIStyleClasses.IsHidden);
                tree.AddToClassList(screen.Layer == UILayer.Hud ? UIStyleClasses.ScreenHud
                    : screen.Layer == UILayer.Screens ? UIStyleClasses.ScreenScreens : UIStyleClasses.ScreenModals);
                GetLayer(screen.Layer).Add(tree);
                try { screen.Build(Navigator, tree); }
                catch { tree.RemoveFromHierarchy(); throw; }
            }
        }
        public void Tick(float unscaledDeltaTime)
        {
            if (disposed) return;
            using (TickMarker.Auto())
            {
                Tooltips.Tick();
                Notifications.Tick(unscaledDeltaTime, ReduceMotion);
            }
        }
        public void HandleBackPressed() => Navigator.Back();
        public bool IsWorldPointerAllowed(Vector2 screenPosBottomLeft)
        {
            if (disposed || Panel == null || Navigator.WorldPointerBlocked
                || clock.FrameCount - Navigator.LastStackChangeFrame <= 1) return false;
            // ECS player-loop callbacks can see Editor-window Screen.height. Flip in the actual
            // panel's coordinates so world input and UI agree even in an unfocused Editor.
            var position = RuntimePanelUtils.ScreenToPanel(Panel, screenPosBottomLeft);
            position.y = Panel.visualTree.layout.height - position.y;
            return Panel.Pick(position) == null;
        }
        static bool IsTextEntry(Focusable target)
        {
            var element = target as VisualElement;
            return element is TextField || element?.GetFirstAncestorOfType<TextField>() != null;
        }
        void OnFocusIn(FocusInEvent evt) => Navigator.SetTextEntry(IsTextEntry(evt.target as Focusable));
        void OnFocusOut(FocusOutEvent evt)
        { if (!IsTextEntry(evt.relatedTarget)) Navigator.SetTextEntry(false); }
        public void ApplyAccessibility(in AccessibilityOptions options) => throw new NotImplementedException("UI-013");
        internal void PlaySound(UISound value) => sound.Play(value);
        void OnClick(ClickEvent evt)
        { if (evt.target is Button button && button.enabledInHierarchy) sound.Play(UISound.Click); }
        void OnPointerOver(PointerOverEvent evt)
        {
            var target = evt.target as VisualElement;
            var button = target as Button ?? target?.GetFirstAncestorOfType<Button>();
            if (button == null || button == lastHover || !button.enabledInHierarchy) return;
            lastHover = button; sound.Play(UISound.Hover);
        }
        void OnPointerOut(PointerOutEvent evt)
        { if (ReferenceEquals(evt.target, lastHover)) lastHover = null; }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            RootElement?.UnregisterCallback<FocusInEvent>(OnFocusIn, TrickleDown.TrickleDown);
            RootElement?.UnregisterCallback<FocusOutEvent>(OnFocusOut, TrickleDown.TrickleDown);
            RootElement?.UnregisterCallback<ClickEvent>(OnClick);
            RootElement?.UnregisterCallback<PointerOverEvent>(OnPointerOver);
            RootElement?.UnregisterCallback<PointerOutEvent>(OnPointerOut);
            Tooltips?.Dispose();
            Notifications?.Dispose();
            Navigator?.DisposeOwnedScreens();
            lastHover = null;
            Game.Core.Services.Unregister(this);
            Game.Core.Services.Unregister<IWorldPointerGate>(this);
            Game.Core.Services.Unregister(Navigator);
            if (ownsDocument && document != null)
            {
                Game.Core.Services.Unregister(document);
                document.visualTreeAsset = null; document.rootVisualElement?.Clear();
            }
            assets.Clear();
        }
    }
}
