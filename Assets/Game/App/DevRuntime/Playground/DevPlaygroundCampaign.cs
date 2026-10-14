#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Game.App.DevRuntime.Commands;
using Game.App.Presenters;
using Game.App.Session;
using Game.Core;
using Game.Core.Events;
using Game.Core.Flow;
using Game.Input;
using Game.Persistence.Data;
using Game.Persistence.Services;
using Game.UI.Data;
using Game.UI.Services;
using Game.UI.Views;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.App.DevRuntime.Playground
{
    /// <summary>DEV-021: explicit portal from the isolated trial to the real opening campaign.</summary>
    internal sealed class DevPlaygroundCampaign : IDisposable
    {
        const string ProfileName = "dev-playground";
        int operationId;
        Guide guide;
        bool disposed;

        internal DevCommandResult Activate(DevContext ctx, UIRoot ui)
        {
            if (disposed || ctx == null || ui == null) return DevCommandResult.Fail("Campaign portal is unavailable.");
            if (operationId != 0 && ctx.Host.TryGetOperation(operationId, out var pending) && pending.Status == DevOperationStatus.Running)
                return DevCommandResult.Fail("The campaign portal is already opening.");
            if (!ctx.TryGetService<GameSession>(out var session) || !session.IsSandbox || !session.HasActiveSession
                || !ctx.TryGetService<IGameFlow>(out var flow) || flow.CurrentScene != GameScene.World
                || !ctx.TryGetService<SaveService>(out var save) || !IsIsolatedProfile(save, save.ActiveProfile)
                || !ctx.TryGetService<InputService>(out var input) || input.CurrentContext != InputContext.Flight
                || !ctx.TryGetService<UiComposition>(out var screens))
                return DevCommandResult.Fail("Enter the isolated playground and activate the campaign beacon in Flight context.");
            if (flow.IsTransitioning || save.IsBusy || ui.Navigator.Top is not FlightHudView || ui.Confirm.IsOpen || ui.Prompt.IsOpen)
                return DevCommandResult.Fail("Finish the current screen, save or transition before opening the campaign.");

            CloseGuide();
            var operation = new EntryOperation(this, ui, screens, session, flow, save, input);
            operationId = ctx.Host.AddOperation(operation);
            return DevCommandResult.Pending(operationId, "Opening the real campaign in dev-playground. Play the opening dialogue; playground reset returns to the quiet sandbox.");
        }

        static bool IsIsolatedProfile(SaveService save, ProfileId expected)
        {
            if (!save.ActiveProfile.Equals(expected)) return false;
            foreach (var profile in save.ListProfiles())
                if (profile.Id.Equals(expected)) return string.Equals(profile.DisplayName, ProfileName, StringComparison.Ordinal);
            return false;
        }

        sealed class EntryOperation : IDevOperation
        {
            readonly DevPlaygroundCampaign owner;
            readonly UIRoot ui;
            readonly UiComposition screens;
            readonly GameSession session;
            readonly IGameFlow flow;
            readonly SaveService save;
            readonly InputService input;
            readonly ProfileId profile;
            readonly SessionDevOperation entry;
            readonly ulong seed;
            public DevOperationStatus Status { get; private set; }
            public string Message { get; private set; } = "Opening the isolated campaign";
            public double TimeoutSeconds => 60;

            internal EntryOperation(DevPlaygroundCampaign owner, UIRoot ui, UiComposition screens,
                GameSession session, IGameFlow flow, SaveService save, InputService input)
            {
                this.owner = owner; this.ui = ui; this.screens = screens; this.session = session;
                this.flow = flow; this.save = save; this.input = input;
                profile = save.ActiveProfile; seed = session.CampaignSeed;
                // The accepted readiness adapter explicitly accepts the opening Dialog context.
                // Requiring Flight here would make entry wait for the human to finish the intro.
                entry = new SessionDevOperation(Open(flow, save, profile, seed), true, true, TimeoutSeconds);
            }

            static async Awaitable<FlowResult> Open(IGameFlow flow, SaveService save, ProfileId profile, ulong seed)
            {
                // Activation already proved this is the dev sandbox. Its disposable trials are discarded;
                // normal campaign entry/exit saving stays with IGameFlow and the existing playground owner.
                var result = await flow.QuitToMainMenuAsync(saveFirst: false);
                if (result != FlowResult.Ok) return result;
                // Recheck after the await: never select another profile or start a game in a changed profile.
                if (!IsIsolatedProfile(save, profile) || flow.HasActiveSession || flow.CurrentScene != GameScene.MainMenu)
                    return FlowResult.NotAllowed;
                if (save.IsBusy) return FlowResult.Busy;
                return await flow.NewGameAsync(new NewGameArgs("Playground Commander", seed, false, ContentId.None));
            }

            public void Tick(DevContext ctx)
            {
                if (Status != DevOperationStatus.Running) return;
                if (owner.disposed) { Status = DevOperationStatus.Failed; Message = "Campaign portal was disposed."; return; }
                entry.Tick(ctx); Message = entry.Message;
                if (entry.Status == DevOperationStatus.Running) return;
                if (entry.Status != DevOperationStatus.Succeeded)
                { Status = DevOperationStatus.Failed; Message = "Campaign entry failed: " + entry.Message; return; }
                if (!IsIsolatedProfile(save, profile) || !session.HasActiveSession || session.IsSandbox || session.IsCreativeSession
                    || session.CampaignSeed != seed || flow.CurrentScene != GameScene.World || flow.IsTransitioning)
                { Status = DevOperationStatus.Failed; Message = "Campaign entry did not retain the isolated profile/session."; return; }
                owner.guide = new Guide(owner, ui, screens, session, flow, save, input, profile);
                Status = DevOperationStatus.Succeeded;
                Message = "Real opening campaign ready in dev-playground: choose a ship and finish the opening encounter, interact with ARK for station dialogue/hangar, then follow Ship Log objectives to unlock gate travel. Normal Save/Load stays in this profile. Console: playground reset returns to the sandbox and discards this practice run.";
            }
        }

        sealed class Guide : IDisposable
        {
            readonly DevPlaygroundCampaign owner;
            readonly UIRoot ui;
            readonly GameSession session;
            readonly IGameFlow flow;
            readonly SaveService save;
            readonly InputService input;
            readonly ProfileId profile;
            readonly VisualElement panel;
            readonly Label help;
            readonly IVisualElementScheduledItem refresh;
            bool disposed;

            internal Guide(DevPlaygroundCampaign owner, UIRoot ui, UiComposition screens, GameSession session,
                IGameFlow flow, SaveService save, InputService input, ProfileId profile)
            {
                this.owner = owner; this.ui = ui; this.session = session;
                this.flow = flow; this.save = save; this.input = input; this.profile = profile;
                panel = new VisualElement { name = "dev-campaign-guide" }; panel.AddToClassList("panel");
                panel.style.position = Position.Absolute; panel.style.left = 24; panel.style.top = 80;
                panel.style.width = 440; panel.style.maxWidth = Length.Percent(45); panel.style.maxHeight = Length.Percent(75);
                panel.style.paddingLeft = 16; panel.style.paddingRight = 16; panel.style.paddingTop = 12; panel.style.paddingBottom = 12;
                var title = new Label("PRACTICE CAMPAIGN / dev-playground"); title.AddToClassList("panel__title"); panel.Add(title);
                var body = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
                body.style.flexShrink = 1; panel.Add(body);
                help = new Label { name = "dev-campaign-help", pickingMode = PickingMode.Ignore };
                help.AddToClassList("text-muted"); help.style.whiteSpace = WhiteSpace.Normal; body.Add(help);
                var actions = new VisualElement(); actions.style.flexDirection = FlexDirection.Row; actions.style.flexWrap = Wrap.Wrap; body.Add(actions);
                AddAction(actions, "dev-campaign-log", "Ship log", () => ui.Navigator.Push<ShipLogView>());
                AddAction(actions, "dev-campaign-map", "Galaxy map", () => ui.Navigator.Push<GalaxyMapView>());
                AddAction(actions, "dev-campaign-system-map", "System map", () => ui.Navigator.Push<StarSystemMapView>());
                AddAction(actions, "dev-campaign-fleet", "Fleet", screens.ShowFleet);
                var close = new Button(owner.CloseGuide) { name = "dev-campaign-guide-close", text = "Hide guide" };
                close.AddToClassList("btn"); close.AddToClassList("btn--ghost"); body.Add(close);
                ui.GetLayer(UILayer.Hud).Add(panel);
                panel.RegisterCallback<DetachFromPanelEvent>(Detached);
                ui.Navigator.StackChanged += Refresh; input.BindingsChanged += RefreshText;
                EventBus<SessionReset>.Subscribe(Reset);
                // Root switching happens before a transition finishes; a small UI-only scheduler restores
                // the guide on editor return without requiring a second production tick or shared-file hook.
                refresh = panel.schedule.Execute(Refresh).Every(150);
                RefreshText(); Refresh();
            }

            bool CanOpen => !disposed && session.HasActiveSession && !session.IsSandbox && !flow.IsTransitioning
                && flow.CurrentScene == GameScene.World && save.ActiveProfile.Equals(profile)
                && ui.Navigator.Top is FlightHudView && input.CurrentContext == InputContext.Flight
                && !ui.Confirm.IsOpen && !ui.Prompt.IsOpen;

            void AddAction(VisualElement parent, string name, string text, Action action)
            {
                var button = new Button(() => { if (CanOpen) action(); }) { name = name, text = text };
                button.AddToClassList("btn"); button.AddToClassList("btn--secondary"); parent.Add(button);
            }
            void RefreshText()
            {
                if (disposed) return;
                string interact = input.GetBindingDisplay("Flight/Interact"), fire = input.GetBindingDisplay("Flight/FirePrimary");
                if (string.IsNullOrEmpty(interact)) interact = "the Interact binding (configure it in Settings)";
                if (string.IsNullOrEmpty(fire)) fire = "the Fire Primary binding (configure it in Settings)";
                help.text = "1. Play the opening dialogue and choose a ship. Fire with " + fire + " to defeat the opening probes.\n"
                    + "2. Approach ARK and press " + interact + ". Station dialogue offers trade and Visit Hangar Bay; docked Fleet → Edit uses the real ship editor.\n"
                    + "3. Follow Ship Log objectives to unlock travel. Select a destination in Galaxy map, then approach an open lane gate and press " + interact + ".\n"
                    + "Esc opens Pause for Save/Load and Main Menu. Normal saves use dev-playground.\n"
                    + "Console: playground reset returns to the quiet sandbox and discards this practice run. The guide returns after dialogue/screens close.";
            }
            void Refresh()
            {
                if (disposed) return;
                if (!session.HasActiveSession || session.IsSandbox || !save.ActiveProfile.Equals(profile)) { owner.CloseGuide(); return; }
                panel.style.display = CanOpen ? DisplayStyle.Flex : DisplayStyle.None;
            }
            void Reset(SessionReset value) => Refresh();
            void Detached(DetachFromPanelEvent value) { if (ReferenceEquals(value.target, panel)) Dispose(); }
            public void Dispose()
            {
                if (disposed) return;
                disposed = true; refresh?.Pause();
                ui.Navigator.StackChanged -= Refresh; input.BindingsChanged -= RefreshText;
                EventBus<SessionReset>.Unsubscribe(Reset); panel.UnregisterCallback<DetachFromPanelEvent>(Detached);
                panel.RemoveFromHierarchy();
            }
        }

        void CloseGuide() { guide?.Dispose(); guide = null; }
        public void Dispose() { if (disposed) return; disposed = true; CloseGuide(); }
    }
}
#endif
