#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Game.App.DevRuntime.Commands;
using Game.App.Flow;
using Game.App.EditorHost;
using Game.Economy;
using Game.Data.Services;
using Game.Data.Definitions;
using Game.Presentation.Presenters;
using Game.App.Presenters;
using Game.App.DevRuntime.Scenarios;
using UnityEngine.SceneManagement;
using Game.App.Session;
using Game.Core;
using Game.Core.Flow;
using Game.Input;
using Game.Persistence.Services;
using Game.UI.Data;
using Game.UI.Services;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Game.App.DevRuntime.Playground
{
    /// <summary>DEV-021: development-only presentation and command composition. Simulation remains with its owners. The playground is the Hollow (DEV-023 option E).</summary>
    internal sealed class DevPlayground : IDisposable
    {
        // The playground is the Hollow (owner, 2026-10-18: "remove the current playground it was superseeded by the asteroid, i want
        // to load into the entrance of the asteroid"): the ARK Proving Grounds and their exhibit districts are gone. Screens and trials
        // that are only menus stay console shortcuts: playground run=<id>. The race stays: a portal inside the Hollow, or race open.
        [Game.Core.StaticStateOk("constant id list")] static readonly string[] ConsoleOnly = { "cargo", "settings", "fleet", "story", "survey", "map", "power", "campaign", "editor", "race" };
        readonly DevRuntimeHost host;
        DevPlaygroundPower power;
        DevPlaygroundShip ship;
        internal DevPlaygroundRace Race { get; private set; }
        readonly DevPlaygroundCampaign campaign = new();
        InputService input;
        UIRoot ui;
        VisualElement panel, launcher;
        Label heading, detail, outcome;
        Button enterButton;
        PlaygroundHollow hollow;
        int hollowNearest = -2;
        double3 origin;
        bool active, entering;
        bool autoEnter;
        bool toRace;   // the player flew through the race portal: open the race when they arrive at the track
        bool wasRacing;
        // Co-op: a guest shows the host's playground without running stations; the host sends its origin to every guest once it is in
        // the world.
        bool guestView;
        readonly List<Game.Core.Net.CoopPeer> peers = new();
        readonly HashSet<byte> shownTo = new();
        int pending;
        string report = "";
        internal DevPlayground(DevRuntimeHost host)
        { this.host = host; autoEnter = Application.isEditor && Game.App.Testing.GameRootTestHooks.SaveRoot == null && SceneManager.GetActiveScene().name == "World"; }
        internal void Bind(InputService service, UIRoot root)
        {
            input = service; ui = root;
            power = new DevPlaygroundPower(host.Context, ui);
            ship = new DevPlaygroundShip(host.Context);
            Race = new DevPlaygroundRace(host); Race.Bind(root);
            input.Controls.Flight.Interact.performed += OnInteract;
            launcher = new VisualElement { name = "dev-launcher", pickingMode = PickingMode.Ignore };
            // Bottom-right corner: the HUD radar owns the top-right (QA-001: these buttons covered it).
            launcher.style.position = Position.Absolute; launcher.style.right = 24; launcher.style.bottom = 12;
            launcher.style.flexDirection = FlexDirection.Row;
            var console = new Button(() => host.Console.Toggle()) { name = "btn-open-console", text = "Console" };
            console.AddToClassList("btn"); console.AddToClassList("btn--secondary"); launcher.Add(console);
            var enter = new Button(() => { var result = Enter(); if (result.Kind == DevResultKind.Failed) Game.Core.GameLog.Warn(Game.Core.LogCategories.Dev, result.Text); })
                { name = "btn-enter-playground", text = "Enter playground" };
            enter.AddToClassList("btn"); enter.AddToClassList("btn--secondary"); launcher.Add(enter); enterButton = enter;
            console.AddToClassList("btn--small"); enter.AddToClassList("btn--small");
            ui.GetLayer(UILayer.Toasts).Add(launcher);
        }
        internal DevCommandResult Enter()
        {
            autoEnter = false;
            var ctx = host.Context;
            // Co-op spike: a reset would tear down the world other players share; a guest never runs the playground.
            if (ctx.TryGetService<Game.Core.Net.ICoopSession>(out var coop) && coop.Role != Game.Core.Net.SimRoleKind.Solo)
                return DevCommandResult.Fail("net leave first: the playground cannot reset during a co-op session");
            if (entering || !ctx.TryGetService<IGameFlow>(out var flow) || flow.IsTransitioning
                || !ctx.TryGetService<SaveService>(out var save) || save.IsBusy
                || !ctx.TryGetService<SceneFlow>(out var scenes)) return DevCommandResult.Fail("flow/save busy or unavailable");
            if (flow.CurrentScene != GameScene.World && flow.CurrentScene != GameScene.MainMenu
                && !(flow.CurrentScene == GameScene.BlueprintLab && IsPlaygroundProfile(save)))
                return DevCommandResult.Fail("Leave the current editor for World or MainMenu first; direct Lab reset is limited to the playground profile");
            entering = true;
            ClearPresentation(); active = false;
            var operation = new EntryOperation(this, Begin(flow, save, scenes));
            return DevCommandResult.Pending(host.AddOperation(operation), "resetting isolated playground");
        }
        static bool IsPlaygroundProfile(SaveService save)
        {
            foreach (var profile in save.ListProfiles())
                if (profile.Id.Equals(save.ActiveProfile) && profile.DisplayName == "dev-playground") return true;
            return false;
        }
        async Awaitable<FlowResult> Begin(IGameFlow flow, SaveService save, SceneFlow scenes)
        {
            try
            {
                bool ownProfile = IsPlaygroundProfile(save);
                // Save a real campaign BEFORE switching profiles. Playground reset deliberately discards its own trials.
                if (flow.HasActiveSession)
                {
                    var exited = await flow.QuitToMainMenuAsync(!ownProfile);
                    if (exited != FlowResult.Ok) return exited;
                }
                NewGameDevCommand.SelectProfile(save, "dev-playground");
                return await scenes.EnterSandboxAsync(new SandboxArgs(true, 42));
            }
            finally { entering = false; }
        }
        sealed class EntryOperation : IDevOperation
        {
            readonly DevPlayground owner;
            readonly SessionDevOperation session;
            public EntryOperation(DevPlayground owner, Awaitable<FlowResult> flow)
            { this.owner = owner; session = new SessionDevOperation(flow, true, false, 45); }
            public DevOperationStatus Status => session.Status;
            public string Message => Status == DevOperationStatus.Succeeded ? "Playground ready at the Hollow's entrance. F in a station's circle; F1: playground reset. playground status lists the stations." : session.Message;
            public double TimeoutSeconds => 45;
            public void Tick(DevContext ctx)
            {
                session.Tick(ctx);
                if (session.Status == DevOperationStatus.Succeeded && !owner.active) owner.Start();
            }
        }
        sealed class LabEntryOperation : IDevOperation
        {
            readonly Awaitable<FlowResult> flow;
            public DevOperationStatus Status { get; private set; }
            public string Message { get; private set; } = "Opening Blueprint Lab";
            public double TimeoutSeconds => 20;
            internal LabEntryOperation(IGameFlow game) { flow = Open(game); }
            static async Awaitable<FlowResult> Open(IGameFlow game)
            {
                // Activation is sandbox-only: discard the trial, preserve stored blueprints.
                var result = await game.QuitToMainMenuAsync(false);
                if (result != FlowResult.Ok) return result;
                return await game.EnterBlueprintLabAsync(new BlueprintLabArgs(BlueprintLabMode.Creative, 0, ContentId.None));
            }
            public void Tick(DevContext ctx)
            {
                if (Status != DevOperationStatus.Running) return;
                var awaiter = flow.GetAwaiter();
                if (!awaiter.IsCompleted) return;
                var result = awaiter.GetResult();
                bool ready = result == FlowResult.Ok && ctx.TryGetService<IGameFlow>(out var game)
                    && game.CurrentScene == GameScene.BlueprintLab && !game.IsTransitioning
                    && ctx.TryGetService<Game.UI.Services.UIRoot>(out var ui) && ui.Navigator.IsOpen<Game.UI.Views.ShipEditorView>();
                Status = ready ? DevOperationStatus.Succeeded : DevOperationStatus.Failed;
                Message = ready ? "Blueprint Lab ready. Save a blueprint, then Exit and run playground reset to return."
                    : "Blueprint Lab unavailable: " + result;
            }
        }
        void Start()
        {
            var ctx = host.Context;
            var player = ctx.PlayerShipRoot;
            if (!DevEcs.TryGetUniversePosition(ctx, player, out origin)) throw new InvalidOperationException("player position unavailable");
            active = true; pending = 0; hollowNearest = -2; toRace = false; wasRacing = false;
            report = "The Hollow: the Khuchad Clan's haven. Fly in through the Maw; the wall lights take the colour of the next station that way, and F in a station's circle trades.";
            Race?.End();
            DevPlaygroundStory.RefreshResetMarkers(ctx);
            hollow?.Dispose(); hollow = new PlaygroundHollow(ctx, origin);
            hollow.Build();
            if (hollow.Missing != null) { report = hollow.Missing; GameLog.Warn(LogCategories.Dev, hollow.Missing); }
            BuildPresentation();
            Place(HollowLayout.Offset + HollowLayout.Entrance, HollowLayout.EntranceYaw);
        }
        internal bool Active => active;
        internal PlaygroundHollow Hollow => hollow;
        internal double3 Origin => origin;
        DevCommandResult Place(float2 at, float yaw)
        {
            if (!host.Context.TryGetFrame(out var frame) || host.Context.PlayerShipRoot == Entity.Null)
                return DevCommandResult.Fail("player unavailable; playground reset restores it");
            DevEcs.TeleportRoot(host.Context.EntityManager, host.Context.PlayerShipRoot,
                (float3)(origin + new double3(at.x, 0, at.y) - frame.OriginInStarSystem), quaternion.RotateY(yaw));
            return DevCommandResult.Ok("");
        }
        /// <summary>Moves the player to the Hollow's entrance ("hollow"), anywhere in it (hollow@x,z[,yaw°]) or a station (in front of
        /// it, facing it).</summary>
        internal DevCommandResult GoTo(string id)
        {
            if (!active || entering || hollow == null) return DevCommandResult.Fail("enter with playground first");
            if (id == "hollow" || id == "entrance" || id == "hub")
            {
                hollow.Build();
                var moved = Place(HollowLayout.Offset + HollowLayout.Entrance, HollowLayout.EntranceYaw);
                if (moved.Kind == DevResultKind.Failed) return moved;
                return hollow.Missing != null ? DevCommandResult.Fail(hollow.Missing) : DevCommandResult.Ok("At the Hollow's entrance: the Maw is ahead. The Clan keeps the peace here.");
            }
            // hollow@x,z[,yaw°]: anywhere in the Hollow (its own metres), for playtests and shots
            if (id.StartsWith("hollow@", StringComparison.Ordinal))
            {
                var parts = id.Substring(7).Split(',');
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                if (parts.Length < 2 || !float.TryParse(parts[0], System.Globalization.NumberStyles.Float, ci, out float x)
                    || !float.TryParse(parts[1], System.Globalization.NumberStyles.Float, ci, out float z)) return DevCommandResult.Fail("goto=hollow@x,z[,yaw degrees]");
                float yaw = parts.Length > 2 && float.TryParse(parts[2], System.Globalization.NumberStyles.Float, ci, out float deg) ? math.radians(deg) : 0;
                hollow.Build();
                var moved = Place(HollowLayout.Offset + new float2(x, z), yaw);
                return moved.Kind == DevResultKind.Failed ? moved : DevCommandResult.Ok($"In the Hollow at {x:0}, {z:0}.");
            }
            hollow.Build();
            foreach (var st in HollowLayout.Stations)
                if (st.Id == id)
                {
                    var moved = Place(HollowLayout.Offset + st.Position, st.Yaw);
                    return moved.Kind == DevResultKind.Failed ? moved : DevCommandResult.Ok("At " + st.Title + ". Press F.");
                }
            if (Array.IndexOf(ConsoleOnly, id) >= 0) return DevCommandResult.Fail(id + " is a screen, not a place: playground run=" + id + " opens it.");
            return DevCommandResult.Fail("Unknown place. " + Directory());
        }
        internal DevCommandResult Run(string id)
        {
            // Console text entry must be removed before normal Flight actions can run.
            if (host.Console.Screen.IsOpen) host.Console.Toggle();
            if (id == "station")
            {
                if (!host.Context.TryGetService<SaveService>(out var save) || !IsPlaygroundProfile(save)
                    || !host.Context.TryGetService<GameSession>(out var session) || session.IsSandbox)
                    return DevCommandResult.Fail("Start playground run=campaign first, then finish the opening dialogue.");
                return host.Commands.Execute("interact body=ark_station", host.Context);
            }
            if (!active || entering) return DevCommandResult.Fail("enter with playground first");
            if (id == "shield" || id == "boost")
            {
                var result = power.Activate(host.Context, ui, id);
                pending = result.Kind == DevResultKind.Pending ? result.OperationId : 0;
                SetReport(result.Text); return result;
            }
            if (Array.IndexOf(ConsoleOnly, id) >= 0) return Report(Execute(id));
            // a Hollow station: fly there and press F
            var moved = GoTo(id);
            return moved.Kind == DevResultKind.Failed ? moved : Activate();
        }
        DevCommandResult Report(DevCommandResult result)
        {
            pending = result.Kind == DevResultKind.Pending ? result.OperationId : 0;
            SetReport(result.Text);
            return result;
        }
        /// <summary>Co-op guest: show the host's playground around <paramref name="hostOrigin"/>. Stations still run on the host.</summary>
        internal DevCommandResult View(double3 hostOrigin)
        {
            if (!host.Context.TryGetService<Game.Core.Net.ICoopSession>(out var coop) || coop.Role != Game.Core.Net.SimRoleKind.Guest)
                return DevCommandResult.Fail("playground view is sent to co-op guests by the host's playground");
            ClearPresentation(); origin = hostOrigin; guestView = true;
            report = "The host's playground: the Hollow. Stations run on the host.";
            BuildPresentation();
            return DevCommandResult.Ok("showing the host's playground");
        }
        void ShowToGuests(Game.App.Session.CoopSession coop)
        {
            coop.GetPeers(peers);
            shownTo.RemoveWhere(slot => !peers.Exists(peer => peer.Slot == slot && peer.InWorld));
            foreach (var peer in peers)
                if (peer.InWorld && shownTo.Add(peer.Slot))
                    coop.SendGuestCommand(peer.Slot, FormattableString.Invariant($"playground view={origin.x:R},{origin.y:R},{origin.z:R}"), out _);
        }
        internal DevCommandResult Status() => DevCommandResult.Ok((active ? "Playground active. " : guestView ? "Showing the host's playground. " : "Playground inactive. F1: playground to enter. ")
            + (ship == null || ship.Override.IsNone ? "" : "Flying " + ship.Override + " (playground ship=starter restores the start ship). ") + Directory() + "\n" + report);
        /// <summary>QA-001: choose the playground flagship, then reset so it spawns.</summary>
        internal DevCommandResult SwitchShip(string id)
        {
            if (ship == null) return DevCommandResult.Fail("playground is not bound yet");
            string error = ship.Select(id);
            return error != null ? DevCommandResult.Fail(error) : Enter();
        }
        string Directory()
        {
            var text = new System.Text.StringBuilder("Console: playground reset | playground goto=hollow (the entrance), goto=<station> or goto=hollow@x,z[,yaw] | playground run=<station or screen>\nStations:");
            foreach (var st in HollowLayout.Stations) text.Append(' ').Append(st.Id);
            text.Append("\nScreens (run only): ").Append(string.Join(", ", ConsoleOnly)).Append("; shield / boost trigger equipped abilities; station: ARK in the practice campaign\n");
            return text.ToString();
        }
        void OnInteract(InputAction.CallbackContext _) { Activate(); }
        internal DevCommandResult Activate()
        {
            if (!active || entering || input == null || input.CurrentContext != InputContext.Flight)
                return DevCommandResult.Fail("Activate a playground station in Flight context");
            // On the race grid F starts the countdown (or the next race after the results).
            if (Race != null && Race.IsOpen && (Race.InLobby || Race.IsFinished)) { var started = Race.Start(); SetReport(started.Text); return started; }
            if (pending != 0 && host.TryGetOperation(pending, out var operation) && operation.Status == DevOperationStatus.Running)
                return DevCommandResult.Fail("Previous station operation is still running");
            if (hollow != null && hollow.Built && DevEcs.TryGetUniversePosition(host.Context, host.Context.PlayerShipRoot, out var shipAt))
            {
                int there = hollow.Nearest(hollow.Relative(shipAt));
                if (there >= 0) return Report(hollow.Activate(there));
            }
            return DevCommandResult.Fail("Fly into a station's circle first");
        }
        /// <summary>What a console-only screen or trial does.</summary>
        DevCommandResult Execute(string id)
        {
            var ctx = host.Context;
            switch (id)
            {
                case "cargo":
                    var result = host.Commands.Execute("give item=resource_fuel count=5", ctx);
                    if (result.Kind != DevResultKind.Failed)
                    {
                        result = host.Commands.Execute("give credits=1000", ctx);
                        if (result.Kind != DevResultKind.Failed && ctx.TryGetService<UiComposition>(out var cargoUi)) cargoUi.ShowInventory();
                    }
                    return result;
                case "story":
                case "survey":
                case "map":
                    return DevPlaygroundStory.Activate(ctx, ui, id);
                case "power": return power.Activate(ctx, ui, "power");
                case "race": return Race.Open(origin);
                case "campaign": return campaign.Activate(ctx, ui);
                case "editor":
                    return ctx.TryGetService<GameSession>(out var editorSession) && editorSession.IsSandbox
                        ? DevCommandResult.Pending(host.AddOperation(new LabEntryOperation(editorSession)), "Opening creative Blueprint Lab")
                        : DevCommandResult.Fail("Editor trial requires the isolated playground");
                case "fleet":
                    if (!ctx.TryGetService<UiComposition>(out var fleetUi)) return DevCommandResult.Fail("UI unavailable");
                    fleetUi.ShowFleet(); return DevCommandResult.Ok("Fleet opened. Select a ship to inspect its committed design and condition.");
                case "settings":
                    if (!ctx.TryGetService<UiComposition>(out var screens)) return DevCommandResult.Fail("UI unavailable");
                    screens.ShowSettings(); return DevCommandResult.Ok("Settings opened.");
            }
            return DevCommandResult.Fail("Unknown screen " + id);
        }
        internal void Tick()
        {
            if (launcher != null) launcher.style.display = host.Console.Screen.IsOpen ? DisplayStyle.None : DisplayStyle.Flex;
            bool coop = host.Context.TryGetService<Game.Core.Net.ICoopSession>(out var coopSession) && coopSession.Role != Game.Core.Net.SimRoleKind.Solo;
            if (enterButton != null) enterButton.style.display = active || coop ? DisplayStyle.None : DisplayStyle.Flex;
            var ctx = host.Context;
            power?.Tick(ctx); Race?.Tick();
            if (autoEnter && !entering && input != null && PlayerScenarioProbes.IsReady(ctx)
                && ctx.TryGetService<IGameFlow>(out var startup) && !startup.IsTransitioning)
            { autoEnter = false; Enter(); }
            if (guestView)
            {
                if (!coop) { guestView = false; ClearPresentation(); }
                return;
            }
            if (!active) return;
            if (!ctx.TryGetService<GameSession>(out var session) || !session.IsSandbox || session.IsTransitioning)
            { active = false; ClearPresentation(); return; }
            if (coopSession is Game.App.Session.CoopSession hosting && hosting.Role == Game.Core.Net.SimRoleKind.Host) ShowToGuests(hosting);
            if (!ctx.TryGetFrame(out _) || hollow == null) return;
            bool located = DevEcs.TryGetUniversePosition(ctx, ctx.PlayerShipRoot, out var ship3);
            float2 at = located ? (float2)(ship3.xz - origin.xz) - HollowLayout.Offset : new float2(float.MaxValue, float.MaxValue);
            if (located && !hollow.Built && HollowLayout.Near(at, 1500)) hollow.Build();
            hollow.Tick(located, ship3);
            // the race portal (owner: "inside the hollow there should be a portal to the race") lands the pilot off the track; the race
            // opens and puts them on its grid
            bool racing = Race != null && Race.IsOpen;
            double toTrack = located ? math.distance(ship3.xz, (origin + DevPlaygroundRace.TrackOffset).xz) : double.MaxValue;
            if (located && hollow.Built && !racing)
            {
                if (toRace && toTrack < PlaygroundHollow.RaceArrivalRadius) { toRace = false; Report(Race.Open(origin)); }
                else if (!toRace && math.distancesq(at, HollowLayout.RacePortal) < 80 * 80) toRace = true;
                // the race ended (race end) with the pilot at the track: back in front of the portal, facing away from it, out to the Ring
                else if (wasRacing && toTrack < PlaygroundHollow.RaceArrivalRadius)
                {
                    var away = -new float2(math.sin(HollowLayout.RacePortalYaw), math.cos(HollowLayout.RacePortalYaw));
                    Place(HollowLayout.Offset + HollowLayout.RacePortal + away * 45, HollowLayout.RacePortalYaw + math.PI);
                    SetReport("Back in the Hollow, by the race portal.");
                }
            }
            wasRacing = racing;
            int st = located && hollow.Built ? hollow.Nearest(at) : -1;
            if (st != hollowNearest && heading != null)
            {
                hollowNearest = st;
                bool atHollow = located && HollowLayout.Near(at, 600);
                heading.text = st >= 0 ? HollowLayout.Stations[st].Title + "  ·  " + input.GetBindingDisplay("Flight/Interact") + " ACTIVATE"
                    : atHollow ? "THE HOLLOW  ·  KHUCHAD CLAN HAVEN" : Race != null && Race.IsOpen ? "TRACK 01  ·  BATTLE GRAVEYARD" : "THE HOLLOW";
            }
            if (pending != 0 && host.TryGetOperation(pending, out var operation) && operation.Status != DevOperationStatus.Running)
            { pending = 0; SetReport(operation.Status + ": " + operation.Message); }
        }
        void SetReport(string value)
        {
            report = value ?? "";
            // Keep verbose diagnostics available in command output; the flight HUD remains bounded.
            if (outcome == null) return;
            outcome.text = report.Length > 240 ? report.Substring(0, 240) + "… (F1: playground status)" : report;
            outcome.style.display = report.Length == 0 ? DisplayStyle.None : DisplayStyle.Flex;
        }
        void BuildPresentation()
        {
            if (ui == null) return;
            panel = new VisualElement { name = "dev-playground", pickingMode = PickingMode.Ignore };
            panel.AddToClassList("panel"); panel.style.position = Position.Absolute; panel.style.left = 24; panel.style.top = 96;
            panel.style.maxWidth = 420; panel.style.paddingLeft = 12; panel.style.paddingRight = 12;
            panel.style.paddingTop = 8; panel.style.paddingBottom = 8;
            heading = AddLabel(panel, "THE HOLLOW", "text-s");
            detail = AddLabel(panel, "", "text-xs"); detail.AddToClassList("text-muted");
            detail.style.whiteSpace = WhiteSpace.Normal; detail.style.display = DisplayStyle.None;
            outcome = AddLabel(panel, report, "text-xs"); outcome.style.whiteSpace = WhiteSpace.Normal;
            ui.GetLayer(UILayer.Hud).Add(panel);
            hollowNearest = -2;
        }
        static Label AddLabel(VisualElement parent, string text, string style)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore }; label.AddToClassList(style); parent.Add(label); return label;
        }
        void ClearPresentation()
        {
            panel?.RemoveFromHierarchy();
            shownTo.Clear();
            panel = null; heading = detail = outcome = null;
            hollow?.Dispose();
        }
        public void Dispose()
        {
            if (input != null) input.Controls.Flight.Interact.performed -= OnInteract;
            power?.Dispose(); ship?.Dispose(); campaign.Dispose(); Race?.Dispose();
            active = false; ClearPresentation();
            launcher?.RemoveFromHierarchy(); launcher = null;
        }
    }
}
#endif
