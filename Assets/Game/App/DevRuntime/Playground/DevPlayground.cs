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
    /// <summary>DEV-021: development-only presentation and command composition. Simulation remains with its owners.</summary>
    internal sealed class DevPlayground : IDisposable
    {
        // DEV-022: the F pads are the hub's stations (PlaygroundHub). Screens that are only menus (cargo, settings, fleet, story log,
        // survey, map, power instruments) have no place in the world; they stay console shortcuts: playground run=<id>.
        [Game.Core.StaticStateOk("constant id list")] static readonly string[] ConsoleOnly = { "cargo", "settings", "fleet", "story", "survey", "map", "power" };
        readonly DevRuntimeHost host;
        readonly PlaygroundHub.Station[] pads = PlaygroundHub.Stations;
        DevPlaygroundPower power;
        DevPlaygroundShip ship;
        DevPlaygroundSalvage salvage;
        internal DevPlaygroundRace Race { get; private set; }
        readonly DevPlaygroundCampaign campaign = new();
        InputService input;
        UIRoot ui;
        VisualElement panel, launcher;
        Label heading, detail, outcome;
        Button enterButton;
        // QA-001: districts appear on the game's own HUD radar as point-of-interest blips, not on a second map.
        double3[] beaconPositions;
        PlaygroundHubDressing dressing;
        PlaygroundHubWorld world;
        // DEV-023 option E: the Hollow, 9 km east of the ARK Proving Grounds, built the first time a pilot goes there
        PlaygroundHollow hollow;
        int hollowNearest = -2;
        bool inHollow;
        PlaygroundHub.District here;
        string resumeAt;                    // a station to return to after a reset (the shipyard pad after a ship swap)
        int courseGate = -1;
        // the brake box: 0 idle, 1 armed (waiting for a fast entry), 2 timing a stop
        int brakeState; double brakeStart; float brakeSpeed;
        double3 origin;
        bool active, entering, enemyRequested, tradeGranted, xenoRequested;
        bool autoEnter;
        // Co-op: a guest shows the host's beacons (rings, labels, radar markers and zoom) without running pads; the host sends
        // its origin to every guest once it is in the world.
        bool guestView;
        readonly List<Game.Core.Net.CoopPeer> peers = new();
        readonly HashSet<byte> shownTo = new();
        int nearest = -1, pending;
        string report = "";
        internal DevPlayground(DevRuntimeHost host)
        { this.host = host; autoEnter = Application.isEditor && Game.App.Testing.GameRootTestHooks.SaveRoot == null && SceneManager.GetActiveScene().name == "World"; }
        internal void Bind(InputService service, UIRoot root)
        {
            input = service; ui = root;
            power = new DevPlaygroundPower(host.Context, ui);
            ship = new DevPlaygroundShip(host.Context);
            salvage = new DevPlaygroundSalvage(host, SetReport);
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
            public string Message => Status == DevOperationStatus.Succeeded ? "Playground ready. F activates nearby pads; F1: playground reset. Use playground status for the directory." : session.Message;
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
            active = true; enemyRequested = false; tradeGranted = false; xenoRequested = false; pending = 0; nearest = -1; here = null;
            courseGate = -1; brakeState = 0;
            report = "Welcome to the ARK Proving Grounds. Fly out through a portal to visit a district.";
            salvage?.Reset(); Race?.End();
            DevPlaygroundStory.RefreshResetMarkers(ctx);
            world = new PlaygroundHubWorld(ctx, origin);
            world.Build();
            hollow?.Dispose(); hollow = new PlaygroundHollow(ctx, origin); hollowNearest = -2; inHollow = false;
            if (world.Missing != null) { report = world.Missing; GameLog.Warn(LogCategories.Dev, world.Missing); }
            BuildPresentation();
            string resume = resumeAt; resumeAt = null;
            if (resume == null || GoTo(resume).Kind == DevResultKind.Failed) Place(PlaygroundHub.Spawn, PlaygroundHub.SpawnYaw);
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
        /// <summary>Moves the player to a station (in front of it, facing it), a district's entrance, or the plaza ("hub").</summary>
        internal DevCommandResult GoTo(string id)
        {
            if (!active || entering) return DevCommandResult.Fail("enter with playground first");
            if (id == "hub" || id == "plaza") { var moved = Place(PlaygroundHub.Spawn, PlaygroundHub.SpawnYaw); return moved.Kind == DevResultKind.Failed ? moved : DevCommandResult.Ok("At the plaza."); }
            if (id == "hollow" && hollow != null)
            {
                hollow.Build();
                var moved = Place(HollowLayout.Offset + HollowLayout.Spawn, HollowLayout.SpawnYaw);
                if (moved.Kind == DevResultKind.Failed) return moved;
                return hollow.Missing != null ? DevCommandResult.Fail(hollow.Missing) : DevCommandResult.Ok("Off the Hollow: the Maw is ahead. The Clan keeps the peace here.");
            }
            // hollow@x,z[,yaw°]: anywhere in the Hollow (its own metres), for playtests and shots
            if (id.StartsWith("hollow@", StringComparison.Ordinal) && hollow != null)
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
            if (hollow != null && hollow.Built)
                foreach (var st in HollowLayout.Stations)
                    if (st.Id == id)
                    {
                        var moved = Place(HollowLayout.Offset + st.Position, st.Yaw);
                        return moved.Kind == DevResultKind.Failed ? moved : DevCommandResult.Ok("At " + st.Title + ". Press F.");
                    }
            // a station first (several share their district's id); district:<id> is the district's entrance
            foreach (var pad in pads)
                if (pad.Id == id)
                {
                    var moved = Place(pad.Position, pad.Yaw);
                    return moved.Kind == DevResultKind.Failed ? moved : DevCommandResult.Ok("At " + pad.Title + ". Press F to activate.");
                }
            foreach (var d in PlaygroundHub.Districts)
                if (d.Id == id || "district:" + d.Id == id)
                {
                    var moved = Place(d.Arrival, d.Yaw);
                    return moved.Kind == DevResultKind.Failed ? moved : DevCommandResult.Ok("At " + d.Title + ".");
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
            if (Array.IndexOf(ConsoleOnly, id) >= 0) return Report(Execute(id, null));
            var moved = GoTo(id);
            if (moved.Kind == DevResultKind.Failed) return moved;
            var activation = Activate();
            if (id == "salvage" && activation.Kind == DevResultKind.Ok && salvage.Target != Entity.Null)
                return host.Commands.Execute("interact", host.Context);
            return activation;
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
            ClearPresentation(); origin = hostOrigin; guestView = true; nearest = -1;
            report = "The host's playground. Stations run on the host; salvage works from here.";
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
            var text = new System.Text.StringBuilder("Console: playground reset | playground run=<station> | playground goto=<station> or goto=district:<id> | playground goto=hollow (option E)\nDistricts: hub");
            foreach (var d in PlaygroundHub.Districts) text.Append(", ").Append(d.Id);
            text.Append("\nStations:");
            foreach (var pad in pads) if (pad.Kind != PlaygroundHub.StationKind.FlyShip) text.Append(' ').Append(pad.Id);
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
            if (hollow != null && hollow.Built && DevEcs.TryGetUniversePosition(host.Context, host.Context.PlayerShipRoot, out var shipAt))
            {
                int there = hollow.Nearest(hollow.Relative(shipAt));
                if (there >= 0) return Report(hollow.Activate(there));
            }
            int index = Nearest();
            if (index < 0) return DevCommandResult.Fail("Fly onto a station's F pad first");
            if (pending != 0 && host.TryGetOperation(pending, out var operation) && operation.Status == DevOperationStatus.Running)
                return DevCommandResult.Fail("Previous station operation is still running");
            var pad = pads[index];
            switch (pad.Kind)
            {
                case PlaygroundHub.StationKind.ResetAll: return Enter();
                case PlaygroundHub.StationKind.ResetDistrict:
                    if (pad.District == PlaygroundHub.Salvage) salvage?.Reset();
                    if (pad.District == PlaygroundHub.Combat) { enemyRequested = false; xenoRequested = false; }
                    if (pad.District == PlaygroundHub.Flight) { courseGate = -1; brakeState = 0; dressing?.SetCourseGate(-1); }
                    world.Reset(pad.District);
                    return Report(DevCommandResult.Ok(pad.District.Title + " is back as it started."));
                case PlaygroundHub.StationKind.FlyShip:
                    resumeAt = pad.Id;
                    var swapped = SwitchShip(pad.Ship);
                    if (swapped.Kind == DevResultKind.Failed) resumeAt = null;
                    return Report(swapped);
            }
            return Report(Execute(pad.Id, pad));
        }
        /// <summary>What a station (or a console-only screen) does.</summary>
        DevCommandResult Execute(string id, PlaygroundHub.Station pad)
        {
            var ctx = host.Context;
            switch (id)
            {
                case "flight":
                    courseGate = 0; dressing?.SetCourseGate(0);
                    return DevCommandResult.Ok("Slalom: gate 1 / " + PlaygroundHub.Course.Length + ". Fly through the lit ring; reverse, strafe and brake through the turns.");
                case "boost_strip": return DevCommandResult.Ok("Fly up the green strip ahead on the right for a speed kick. Ctrl fires your boost ability.");
                case "brake":
                    brakeState = 1;
                    return DevCommandResult.Ok("Brake test armed: fly out, come back into the box faster than 40 m/s and stop inside it (X brakes).");
                case "range":
                    return ctx.TryGetService<FiringRangeService>(out var range) && ctx.EntityManager.Exists(range.Target)
                        ? DevCommandResult.Ok("Target station ahead. Space to fire; aim at a bridge to cut a pod off. RESET WEAPONS RANGE restores it.")
                        : DevCommandResult.Fail("The target station is gone: RESET WEAPONS RANGE brings a new one.");
                case "combat":
                    if (enemyRequested) return DevCommandResult.Ok("The raider is already out. RESET COMBAT ARENA clears it.");
                    var duel = Spawn(pad, "spawn blueprint=ship_nocturne_wraith", 0, 330, "faction_raiders", "");
                    if (duel.Kind != DevResultKind.Failed) enemyRequested = true;
                    return duel;
                case "xeno":
                    if (xenoRequested) return DevCommandResult.Ok("The xeno wave is already awake. RESET COMBAT ARENA clears it.");
                    var wave = Spawn(pad, "spawn blueprint=ship_xeno_husk", -80, 330, "faction_xeno", " count=2 spacing=70");
                    if (wave.Kind != DevResultKind.Failed) wave = Spawn(pad, "spawn blueprint=ship_xeno_broodmother", 0, 460, "faction_xeno", "");
                    if (wave.Kind != DevResultKind.Failed) xenoRequested = true;
                    return wave;
                case "hazards": return DevCommandResult.Ok(pad.Detail);
                case "movers": return DevCommandResult.Ok(pad.Detail);
                case "cargo":
                    var result = host.Commands.Execute("give item=resource_fuel count=5", ctx);
                    if (result.Kind != DevResultKind.Failed)
                    {
                        result = host.Commands.Execute("give credits=1000", ctx);
                        if (result.Kind != DevResultKind.Failed && ctx.TryGetService<UiComposition>(out var cargoUi)) cargoUi.ShowInventory();
                    }
                    return result;
                case "merchant":
                    if (ctx.TryGetService<EconomyService>(out var economy) && ctx.TryGetService<ContentService>(out var content)
                        && ctx.TryGetService<MerchantPresenter>(out var merchant)
                        && content.Catalog.TryGet<MerchantDefinition>(new ContentId("merchant_station_market"), out var market))
                    {
                        if (!tradeGranted) { economy.Wallet.Add(2000); tradeGranted = true; }
                        merchant.Show(economy.Merchants.OpenSession(market, 0, false, null), null);
                        return DevCommandResult.Ok("The merchant answers: ARK Station Market. Buy and sell against real stock and cargo.");
                    }
                    return DevCommandResult.Fail("Merchant services unavailable");
                case "story":
                case "survey":
                case "map":
                    return DevPlaygroundStory.Activate(ctx, ui, id);
                case "power": return power.Activate(ctx, ui, "power");
                case "salvage": return salvage.Activate(ctx, ui, origin + new double3(pad.Position.x, 0, pad.Position.y));
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
            return DevCommandResult.Ok(pad?.Detail ?? "");
        }
        /// <summary>A hostile ship from the spawn command at a point in the station's district (x right, z forward of its entrance pad).</summary>
        DevCommandResult Spawn(PlaygroundHub.Station pad, string command, float x, float z, string faction, string extra)
        {
            if (!host.Context.TryGetFrame(out var frame)) return DevCommandResult.Fail("frame unavailable");
            var at = pad.Position + pad.District.Right * x + pad.District.Forward * z;
            var local = (float3)(origin + new double3(at.x, 0, at.y) - frame.OriginInStarSystem);
            return host.Commands.Execute(FormattableString.Invariant($"{command} x={local.x:0.##} z={local.z:0.##} face=player faction={faction} team=2 ai=true{extra}"), host.Context);
        }
        int Nearest()
        {
            if (!DevEcs.TryGetUniversePosition(host.Context, host.Context.PlayerShipRoot, out var position)) return -1;
            int found = -1; double best = PlaygroundHub.StationRadius * PlaygroundHub.StationRadius;
            for (int i = 0; i < pads.Length; i++)
            {
                double distance = math.lengthsq(position.xz - (origin.xz + (double2)pads[i].Position));
                if (distance <= best) { best = distance; found = i; }
            }
            return found;
        }
        internal void Tick()
        {
            if (launcher != null) launcher.style.display = host.Console.Screen.IsOpen ? DisplayStyle.None : DisplayStyle.Flex;
            bool coop = host.Context.TryGetService<Game.Core.Net.ICoopSession>(out var coopSession) && coopSession.Role != Game.Core.Net.SimRoleKind.Solo;
            if (enterButton != null) enterButton.style.display = active || coop ? DisplayStyle.None : DisplayStyle.Flex;
            var ctx = host.Context;
            power?.Tick(ctx); salvage?.Tick(); Race?.Tick();
            if (autoEnter && !entering && input != null && PlayerScenarioProbes.IsReady(ctx)
                && ctx.TryGetService<IGameFlow>(out var startup) && !startup.IsTransitioning)
            { autoEnter = false; Enter(); }
            if (guestView)
            {
                if (!coop) { guestView = false; ClearPresentation(); return; }
            }
            else
            {
                if (!active) return;
                if (!ctx.TryGetService<GameSession>(out var session) || !session.IsSandbox || session.IsTransitioning)
                { active = false; ClearPresentation(); return; }
                if (coopSession is Game.App.Session.CoopSession hosting && hosting.Role == Game.Core.Net.SimRoleKind.Host) ShowToGuests(hosting);
            }
            if (!ctx.TryGetFrame(out var frame)) return;
            if (dressing != null) dressing.Root.transform.position = (float3)(origin - frame.OriginInStarSystem);
            bool located = DevEcs.TryGetUniversePosition(ctx, ctx.PlayerShipRoot, out var ship3);
            float2 at = located ? (float2)(ship3.xz - origin.xz) : new float2(float.MaxValue, float.MaxValue);
            // a district fills with its exhibits the first time a pilot comes near it
            var district = located ? PlaygroundHub.DistrictAt(at, 160) : null;
            if (district != null && !guestView && world != null && !world.IsPopulated(district)) world.Populate(district);
            if (located)
            {
                var inside = PlaygroundHub.DistrictAt(at);
                if (inside != here)
                {
                    here = inside; nearest = -2;
                    SetReport(inside == null ? "The plaza: fly out through a portal to visit a district." : world?.Intro(inside) ?? inside.Tagline);
                }
            }
            if (courseGate >= 0 && located && math.distancesq(at, PlaygroundHub.Course[courseGate]) < 18 * 18)
            {
                courseGate++;
                if (courseGate == PlaygroundHub.Course.Length) { courseGate = -1; SetReport("Slalom complete: all six gates. Back on the SLALOM pad, F runs it again."); }
                else SetReport("Slalom: gate " + (courseGate + 1) + " / " + PlaygroundHub.Course.Length + ".");
                dressing?.SetCourseGate(courseGate);
            }
            if (brakeState > 0 && located) BrakeTest(ctx, at);
            if (hollow != null && !guestView)
            {
                if (located && !hollow.Built && HollowLayout.Near(at - HollowLayout.Offset, 1500)) hollow.Build();
                hollow.Tick(located, ship3);
                // the report greets the Hollow, not the Proving Grounds the pilot left behind
                bool atHollow = located && HollowLayout.Near(at - HollowLayout.Offset, 600);
                if (atHollow != inHollow)
                {
                    inHollow = atHollow; nearest = -2;   // and the heading names it
                    SetReport(atHollow ? "The Hollow: the Khuchad Clan's haven. Each district has its colours; its emblem turns over its station, and F in a station's circle trades."
                        : here == null ? "The plaza: fly out through a portal to visit a district." : world?.Intro(here) ?? here.Tagline);
                }
                int st = located && hollow.Built ? hollow.Nearest(at - HollowLayout.Offset) : -1;
                if (st != hollowNearest)
                {
                    hollowNearest = st; nearest = -2;
                    if (st >= 0 && heading != null)
                    {
                        heading.text = HollowLayout.Stations[st].Title + "  ·  " + input.GetBindingDisplay("Flight/Interact") + " ACTIVATE";
                        if (detail != null) detail.style.display = DisplayStyle.None;
                    }
                }
            }
            int next = Nearest();
            if (next != nearest && hollowNearest < 0)
            {
                nearest = next;
                bool atHollow = located && HollowLayout.Near(at - HollowLayout.Offset, 600);
                string where = atHollow ? "THE HOLLOW" : here == null ? "ARK PROVING GROUNDS" : here.Title;
                if (heading != null) heading.text = next < 0 ? where + (atHollow ? "  ·  KHUCHAD CLAN HAVEN" : here == null ? "  ·  FLY THROUGH A PORTAL" : "  ·  " + here.Tagline)
                    : guestView ? pads[next].Title + "  ·  THE HOST ACTIVATES STATIONS" : pads[next].Title + "  ·  " + input.GetBindingDisplay("Flight/Interact") + " ACTIVATE";
                // Details only on a pad; the floor signs do the wayfinding (QA-001 clutter).
                if (detail != null) { detail.text = next < 0 ? "" : pads[next].Detail; detail.style.display = next < 0 ? DisplayStyle.None : DisplayStyle.Flex; }
            }
            if (pending != 0 && host.TryGetOperation(pending, out var operation) && operation.Status != DevOperationStatus.Running)
            { pending = 0; SetReport(operation.Status + ": " + operation.Message); }
            if (beaconPositions != null && ctx.World != null && ctx.World.IsCreated)
            {
                beaconPositions[0] = origin + new double3(PlaygroundHub.Plaza.x, 0, PlaygroundHub.Plaza.y);
                for (int i = 0; i < PlaygroundHub.Districts.Length; i++)
                    beaconPositions[i + 1] = origin + new double3(PlaygroundHub.Districts[i].Centre.x, 0, PlaygroundHub.Districts[i].Centre.y);
                var hud = ctx.World.GetExistingSystemManaged<Game.Presentation.Systems.HudSyncSystem>();
                if (hud != null) { hud.SetMarkers(beaconPositions); hud.DisplayRangeM = 2200; }
            }
        }
        /// The brake box: armed by its pad; a pilot entering faster than 40 m/s starts the clock, which stops when the ship is
        /// still (under 1 m/s) inside the box, or fails when it leaves.
        void BrakeTest(DevContext ctx, float2 at)
        {
            var em = ctx.EntityManager;
            if (!em.HasComponent<Unity.Physics.PhysicsVelocity>(ctx.PlayerShipRoot)) return;
            float speed = math.length(em.GetComponentData<Unity.Physics.PhysicsVelocity>(ctx.PlayerShipRoot).Linear.xz);
            var d = PlaygroundHub.Flight;
            float2 rel = at - d.At(-175, 120);
            bool inside = math.abs(math.dot(rel, d.Right)) < 22 && math.abs(math.dot(rel, d.Forward)) < 22;
            if (brakeState == 1 && inside && speed > 40) { brakeState = 2; brakeStart = UnityEngine.Time.timeAsDouble; brakeSpeed = speed; SetReport(FormattableString.Invariant($"Into the box at {speed:0} m/s: stop!")); }
            else if (brakeState == 2 && !inside) { brakeState = 1; SetReport("Overshot the box. Come round and try again."); }
            else if (brakeState == 2 && speed < 1)
            {
                brakeState = 1;
                SetReport(FormattableString.Invariant($"Stopped from {brakeSpeed:0} m/s in {UnityEngine.Time.timeAsDouble - brakeStart:0.00} s inside the box. Again?"));
            }
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
            heading = AddLabel(panel, "ARK PROVING GROUNDS", "text-s");
            detail = AddLabel(panel, "", "text-xs"); detail.AddToClassList("text-muted");
            detail.style.whiteSpace = WhiteSpace.Normal; detail.style.display = DisplayStyle.None;
            outcome = AddLabel(panel, report, "text-xs"); outcome.style.whiteSpace = WhiteSpace.Normal;
            ui.GetLayer(UILayer.Hud).Add(panel);
            beaconPositions = new double3[PlaygroundHub.Districts.Length + 1];
            if (host.Context.TryGetService<ContentService>(out var content) && content.Catalog.TryGet<TrackDefinition>(new ContentId("track_01"), out var track))
                dressing = new PlaygroundHubDressing(track.Look, host.transform);
            nearest = -2;
        }
        static Label AddLabel(VisualElement parent, string text, string style)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore }; label.AddToClassList(style); parent.Add(label); return label;
        }
        void ClearPresentation()
        {
            panel?.RemoveFromHierarchy();
            if (beaconPositions != null && host.Context.World != null && host.Context.World.IsCreated)
            {
                var hud = host.Context.World.GetExistingSystemManaged<Game.Presentation.Systems.HudSyncSystem>();
                if (hud != null) { hud.SetMarkers(default); hud.DisplayRangeM = 2000; }
            }
            beaconPositions = null; shownTo.Clear();
            panel = null; heading = detail = outcome = null;
            dressing?.Dispose(); dressing = null; courseGate = -1;
            hollow?.Dispose();
        }
        public void Dispose()
        {
            if (input != null) input.Controls.Flight.Interact.performed -= OnInteract;
            power?.Dispose(); ship?.Dispose(); campaign.Dispose(); salvage?.Dispose(); Race?.Dispose();
            active = false; ClearPresentation();
            launcher?.RemoveFromHierarchy(); launcher = null;
        }
    }
}
#endif
