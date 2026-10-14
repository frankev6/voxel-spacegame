#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
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
        sealed class Pad
        {
            internal readonly string Id, Title, Detail;
            internal readonly float3 Offset;
            internal Label Label;
            internal LineRenderer Ring;
            internal Pad(string id, string title, float x, float z, string detail)
            { Id = id; Title = title; Offset = new float3(x, 0, z); Detail = detail; }
        }
        readonly DevRuntimeHost host;
        readonly Pad[] pads = {
            new Pad("flight", "01  FLIGHT / THRUSTERS", -160, 0, "W/S forward/reverse · A/D turn · Shift+A/D strafe · Q/E strafe\nCtrl boost · X brake · Mouse wheel camera zoom. F starts a four-gate course. Fly through the highlighted rings to complete it."),
            new Pad("range", "02  DESTRUCTION / VFX", 0, 0, "Space fires. Aim at the narrow pod bridges to detach pieces; destroy the core for a drifting hulk.\nAlso tests tracers, impacts, sparks, explosions, smoke, debris, sound and collider updates. Reset restores all targets."),
            new Pad("combat", "03  OPT-IN ENEMY", 560, 0, "F starts one hostile fighter facing you. Real AI, weapons, damage, power and player-death flow.\nOne enemy per reset. It can pursue you: finish the trial or use playground reset before another trial."),
            new Pad("cargo", "04  CARGO / CREDITS", -160, -180, "F grants fuel and 1000 credits through the real inventory/wallet owners. Repeat to exercise stacking/capacity.\nOpens your inventory: drag items, inspect, sort and stack cargo."),
            new Pad("settings", "05  SETTINGS", 0, -180, "F opens real settings. Change audio, display, accessibility and control settings; close to return to flight."),
            new Pad("merchant", "06  TRADE", 160, -180, "F opens ARK Station Market with real stock, prices, Buy and Sell. First visit grants 2000 test credits; trades change your actual cargo and wallet."),
            new Pad("fleet", "07  FLEET", -320, -180, "F opens your actual fleet. Select a ship to inspect hull condition, blocks, thrust, power, shields and cargo capacity. Docked fleet editing is separate from the creative Lab beacon."),
            new Pad("editor", "08  BLUEPRINT LAB", -320, 0, "F opens the real creative Blueprint Lab. Place/remove blocks, rotate, mirror, undo/redo, inspect overlays and save a blueprint.\nThis leaves the sandbox. Use playground reset directly to return. Saved blueprints survive reset."),
            new Pad("story", "09  STORY / SHIP LOG", -160, -360, "F accepts the authored Survey Local Materials quest and reveals Fragmented Warning plus a targeted research-station rumor.\nInspect the real log: track the quest, read the clue, or select Rumors → Show on map. No campaign intro starts."),
            new Pad("survey", "10  SURVEY SUPPLIES", 0, -360, "F collects one actual palladium (up to three) and opens the survey log. Repeat to see real objective progress and completion of the collection step.\nThe ARK delivery step stays unfinished here; use the campaign for its station dialogue."),
            new Pad("map", "11  GALAXY / RUMOR", 160, -360, "F opens the real galaxy map at the research-station rumor. Pan, zoom, inspect systems and the quest/rumor markers.\nThis previews the generated galaxy without loading its bodies or starting travel; your sandbox ship stays here."),
            new Pad("power", "12  POWER / ABILITIES", -320, -360, "F opens live power and shield instruments. Toggle shield or trigger Speed Boost using the buttons or equipped ability keys. Console: playground run=shield or run=boost."),
            new Pad("campaign", "13  PRACTICE CAMPAIGN", -160, -540, "F starts the real opening campaign in the isolated playground profile: dialogue, combat, station docking, trade and travel. Console: playground reset returns here."),
            new Pad("salvage", "14  SALVAGE / RECOVER CARGO", 0, -540, DevPlaygroundSalvage.PadDetail),
            new Pad("xeno", "15  XENO / ALIENS", 160, -540, "F wakes two HUSK skimmers and a BROODMOTHER, grown ships of smooth obsidian tissue (ADR-0115).\nThey hunt you with acid. Shoot them to see wounds open into glowing flesh; kill the heart for a hulk. One wave per reset.")
        };
        DevPlaygroundPower power;
        DevPlaygroundSalvage salvage;
        readonly DevPlaygroundCampaign campaign = new();
        InputService input;
        UIRoot ui;
        VisualElement panel, markers, launcher;
        Label heading, detail, outcome;
        Button enterButton;
        // QA-001: beacons appear on the game's own HUD radar as point-of-interest blips, not on a second map.
        double3[] beaconPositions;
        GameObject visuals;
        Material material;
        LineRenderer courseRing;
        readonly float3[] courseOffsets = { new float3(-160, 0, 90), new float3(-240, 0, 150), new float3(-320, 0, 70), new float3(-160, 0, 0) };
        int courseGate = -1;
        double3 origin;
        bool active, entering, enemyRequested, tradeGranted, xenoRequested;
        bool autoEnter;
        int nearest = -1, pending;
        string report = "";
        internal DevPlayground(DevRuntimeHost host)
        { this.host = host; autoEnter = Application.isEditor && Game.App.Testing.GameRootTestHooks.SaveRoot == null && SceneManager.GetActiveScene().name == "World"; }
        internal void Bind(InputService service, UIRoot root)
        {
            input = service; ui = root;
            power = new DevPlaygroundPower(host.Context, ui);
            salvage = new DevPlaygroundSalvage(host, SetReport);
            input.Controls.Flight.Interact.performed += OnInteract;
            launcher = new VisualElement { name = "dev-launcher", pickingMode = PickingMode.Ignore };
            // Bottom-right corner: the HUD radar owns the top-right (QA-001: these buttons covered it).
            launcher.style.position = Position.Absolute; launcher.style.right = 24; launcher.style.bottom = 12;
            launcher.style.flexDirection = FlexDirection.Row;
            var console = new Button(() => host.Console.Toggle()) { name = "btn-open-console", text = "Console" };
            console.AddToClassList("btn"); console.AddToClassList("btn--secondary"); launcher.Add(console);
            var enter = new Button(() => { var result = Enter(); if (result.Kind == DevResultKind.Failed) Debug.LogWarning(result.Text); })
                { name = "btn-enter-playground", text = "Enter playground" };
            enter.AddToClassList("btn"); enter.AddToClassList("btn--secondary"); launcher.Add(enter); enterButton = enter;
            console.AddToClassList("btn--small"); enter.AddToClassList("btn--small");
            ui.GetLayer(UILayer.Toasts).Add(launcher);
        }
        internal DevCommandResult Enter()
        {
            autoEnter = false;
            var ctx = host.Context;
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
                    && UnityEngine.Object.FindAnyObjectByType<ShipEditorHost>() != null;
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
            active = true; enemyRequested = false; tradeGranted = false; xenoRequested = false; pending = 0; nearest = -1;
            courseGate = -1;
            report = "Quiet sandbox ready. No hostile ships spawn until you activate COMBAT.";
            salvage?.Reset();
            DevPlaygroundStory.RefreshResetMarkers(ctx);
            BuildPresentation();
            GoTo("flight");
        }
        internal DevCommandResult GoTo(string id)
        {
            if (!active || entering) return DevCommandResult.Fail("enter with playground first");
            foreach (var pad in pads)
                if (pad.Id == id)
                {
                    if (!host.Context.TryGetFrame(out var frame) || host.Context.PlayerShipRoot == Entity.Null)
                        return DevCommandResult.Fail("player unavailable; playground reset restores it");
                    DevEcs.TeleportRoot(host.Context.EntityManager, host.Context.PlayerShipRoot,
                        (float3)(origin + pad.Offset - frame.OriginInStarSystem), quaternion.identity);
                    return DevCommandResult.Ok("At " + pad.Title + ". Press F to activate.");
                }
            return DevCommandResult.Fail("Unknown pad. " + Directory());
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
            var moved = GoTo(id == "shield" || id == "boost" ? "power" : id);
            if (moved.Kind == DevResultKind.Failed) return moved;
            if (id == "shield" || id == "boost")
            {
                var result = power.Activate(host.Context, ui, id);
                pending = result.Kind == DevResultKind.Pending ? result.OperationId : 0;
                SetReport(result.Text); return result;
            }
            var activation = Activate();
            if (id == "salvage" && activation.Kind == DevResultKind.Ok && salvage.Target != Entity.Null)
                return host.Commands.Execute("interact", host.Context);
            return activation;
        }
        internal DevCommandResult Status() => DevCommandResult.Ok((active ? "Playground active. " : "Playground inactive. F1: playground to enter. ") + Directory() + "\n" + report);
        string Directory()
        {
            var text = new System.Text.StringBuilder("Console: playground reset | playground run=<id> | playground goto=<id>\n");
            foreach (var pad in pads) text.Append(pad.Id).Append(" — ").Append(pad.Title).Append('\n');
            text.Append("shield / boost — trigger equipped abilities\nstation — ARK interaction in practice campaign\n");
            return text.ToString();
        }
        void OnInteract(InputAction.CallbackContext _) { Activate(); }
        internal DevCommandResult Activate()
        {
            if (!active || entering || input == null || input.CurrentContext != InputContext.Flight)
                return DevCommandResult.Fail("Activate a playground pad in Flight context");
            int index = Nearest();
            if (index < 0) return DevCommandResult.Fail("Fly inside a beacon ring first");
            if (pending != 0 && host.TryGetOperation(pending, out var operation) && operation.Status == DevOperationStatus.Running)
                return DevCommandResult.Fail("Previous pad operation is still running");
            var pad = pads[index];
            DevCommandResult result;
            switch (pad.Id)
            {
                case "flight":
                    courseGate = 0;
                    ShowCourseGate();
                    result = DevCommandResult.Ok("Flight course: gate 1 / 4. Fly through the highlighted ring; reverse, strafe and brake through the turns.");
                    break;
                case "range":
                    GoTo("range");
                    result = host.Commands.Execute("range", host.Context);
                    if (result.Kind != DevResultKind.Failed) result = DevCommandResult.Ok("Station ready ahead. Space to fire. Aim at a bridge to break a pod off. F1: playground reset restores it.");
                    break;
                case "combat":
                    if (enemyRequested) result = DevCommandResult.Ok("Enemy trial already started. playground reset restores the quiet sandbox.");
                    else
                    {
                        result = host.Commands.Execute("spawn blueprint=ship_fighter dx=0 dz=90 face=player faction=faction_raiders team=2 ai=true", host.Context);
                        if (result.Kind != DevResultKind.Failed) enemyRequested = true;
                    }
                    break;
                case "cargo":
                    result = host.Commands.Execute("give item=resource_fuel count=5", host.Context);
                    if (result.Kind != DevResultKind.Failed)
                    {
                        result = host.Commands.Execute("give credits=1000", host.Context);
                        if (result.Kind != DevResultKind.Failed && host.Context.TryGetService<UiComposition>(out var cargoUi)) cargoUi.ShowInventory();
                    }
                    break;
                case "merchant":
                    if (host.Context.TryGetService<EconomyService>(out var economy)
                        && host.Context.TryGetService<ContentService>(out var content)
                        && host.Context.TryGetService<MerchantPresenter>(out var merchant)
                        && content.Catalog.TryGet<MerchantDefinition>(new ContentId("merchant_station_market"), out var market))
                    {
                        if (!tradeGranted) { economy.Wallet.Add(2000); tradeGranted = true; }
                        merchant.Show(economy.Merchants.OpenSession(market, 0, false, null), null);
                        result = DevCommandResult.Ok("ARK Station Market opened. Buy and sell against real stock and cargo.");
                    }
                    else result = DevCommandResult.Fail("Merchant services unavailable");
                    break;
                case "story":
                case "survey":
                case "map":
                    result = DevPlaygroundStory.Activate(host.Context, ui, pad.Id);
                    break;
                case "power":
                    result = power.Activate(host.Context, ui, "power");
                    break;
                case "xeno":
                    if (xenoRequested) result = DevCommandResult.Ok("The xeno wave is already awake. playground reset restores the quiet sandbox.");
                    else
                    {
                        result = host.Commands.Execute("spawn blueprint=ship_xeno_husk dx=0 dz=220 face=player faction=faction_xeno team=2 count=2 spacing=70 ai=true", host.Context);
                        if (result.Kind != DevResultKind.Failed)
                            result = host.Commands.Execute("spawn blueprint=ship_xeno_broodmother dx=0 dz=420 face=player faction=faction_xeno team=2 ai=true", host.Context);
                        if (result.Kind != DevResultKind.Failed) xenoRequested = true;
                    }
                    break;
                case "salvage":
                    result = salvage.Activate(host.Context, ui, origin + pad.Offset);
                    break;
                case "campaign":
                    result = campaign.Activate(host.Context, ui);
                    break;
                case "editor":
                    if (host.Context.TryGetService<GameSession>(out var editorSession) && editorSession.IsSandbox)
                        result = DevCommandResult.Pending(host.AddOperation(new LabEntryOperation(editorSession)), "Opening creative Blueprint Lab");
                    else result = DevCommandResult.Fail("Editor trial requires the isolated playground");
                    break;
                case "fleet":
                    if (host.Context.TryGetService<UiComposition>(out var fleetUi))
                    { fleetUi.ShowFleet(); result = DevCommandResult.Ok("Fleet opened. Select a ship to inspect its committed design and condition."); }
                    else result = DevCommandResult.Fail("UI unavailable");
                    break;
                case "settings":
                    if (host.Context.TryGetService<UiComposition>(out var screens))
                    { screens.ShowSettings(); result = DevCommandResult.Ok("Settings opened."); }
                    else result = DevCommandResult.Fail("UI unavailable");
                    break;
                default: result = DevCommandResult.Ok(pad.Detail); break;
            }
            pending = result.Kind == DevResultKind.Pending ? result.OperationId : 0;
            SetReport(result.Text);
            return result;
        }
        int Nearest()
        {
            if (!DevEcs.TryGetUniversePosition(host.Context, host.Context.PlayerShipRoot, out var position)) return -1;
            int found = -1; double best = 28 * 28;
            for (int i = 0; i < pads.Length; i++)
            {
                double distance = math.lengthsq(position - (origin + pads[i].Offset));
                if (distance <= best) { best = distance; found = i; }
            }
            return found;
        }
        internal void Tick()
        {
            if (launcher != null) launcher.style.display = host.Console.Screen.IsOpen ? DisplayStyle.None : DisplayStyle.Flex;
            if (enterButton != null) enterButton.style.display = active ? DisplayStyle.None : DisplayStyle.Flex;
            var ctx = host.Context;
            power?.Tick(ctx); salvage?.Tick();
            if (autoEnter && !entering && input != null && PlayerScenarioProbes.IsReady(ctx)
                && ctx.TryGetService<IGameFlow>(out var startup) && !startup.IsTransitioning)
            { autoEnter = false; Enter(); }
            if (!active) return;
            if (!ctx.TryGetService<GameSession>(out var session) || !session.IsSandbox || session.IsTransitioning)
            { active = false; ClearPresentation(); return; }
            if (courseGate >= 0 && ctx.TryGetFrame(out var courseFrame))
            {
                if (courseRing != null) courseRing.transform.position = (float3)(origin + courseOffsets[courseGate] - courseFrame.OriginInStarSystem);
                if (DevEcs.TryGetUniversePosition(ctx, ctx.PlayerShipRoot, out var shipPosition)
                    && math.lengthsq(shipPosition - (origin + courseOffsets[courseGate])) < 18 * 18)
                {
                    courseGate++;
                    if (courseGate == courseOffsets.Length)
                    { courseGate = -1; if (courseRing != null) courseRing.gameObject.SetActive(false); SetReport("Flight course complete: all four gates reached. Return to FLIGHT and press F to repeat."); }
                    else { ShowCourseGate(); SetReport("Flight course: gate " + (courseGate + 1) + " / 4."); }
                }
            }
            int next = Nearest();
            if (next != nearest)
            {
                nearest = next;
                if (heading != null) heading.text = next < 0 ? "DEV PLAYGROUND  ·  FLY TO A BEACON" : pads[next].Title + "  ·  " + input.GetBindingDisplay("Flight/Interact") + " ACTIVATE";
                // Details only inside a ring; the beacon chart and world labels do the wayfinding (QA-001 clutter).
                if (detail != null) { detail.text = next < 0 ? "" : pads[next].Detail; detail.style.display = next < 0 ? DisplayStyle.None : DisplayStyle.Flex; }
            }
            if (pending != 0 && host.TryGetOperation(pending, out var operation) && operation.Status != DevOperationStatus.Running)
            { pending = 0; SetReport(operation.Status + ": " + operation.Message); }
            if (beaconPositions != null && ctx.World != null && ctx.World.IsCreated)
            {
                for (int i = 0; i < pads.Length; i++) beaconPositions[i] = origin + pads[i].Offset;
                var hud = ctx.World.GetExistingSystemManaged<Game.Presentation.Systems.HudSyncSystem>();
                if (hud != null) { hud.SetMarkers(beaconPositions); hud.DisplayRangeM = 800; } // beacons lie within ±600 m
            }
            var camera = ctx.GameplayCamera;
            if (markers == null || camera == null || !ctx.TryGetFrame(out var frame)) return;
            for (int i = 0; i < pads.Length; i++)
            {
                Vector3 position = (float3)(origin + pads[i].Offset - frame.OriginInStarSystem);
                if (pads[i].Ring != null) pads[i].Ring.transform.position = position;
                var label = pads[i].Label;
                var viewport = camera.WorldToViewportPoint(position);
                bool visible = viewport.z > 0 && viewport.x > 0 && viewport.x < 1 && viewport.y > 0 && viewport.y < 1;
                label.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                if (!visible || ui.Panel == null) continue;
                var point = RuntimePanelUtils.CameraTransformWorldToPanel(ui.Panel, position, camera);
                label.style.left = point.x + 28; label.style.top = point.y + 28;
                if (pads[i].Ring != null) pads[i].Ring.startColor = pads[i].Ring.endColor = label.resolvedStyle.color;
            }
        }
        void ShowCourseGate()
        {
            if (courseRing == null && material != null && visuals != null)
            {
                var go = new GameObject("Flight course gate"); go.transform.SetParent(visuals.transform, false);
                courseRing = go.AddComponent<LineRenderer>(); courseRing.sharedMaterial = material;
                courseRing.useWorldSpace = false; courseRing.loop = true; courseRing.widthMultiplier = 2;
                courseRing.positionCount = 48; courseRing.startColor = courseRing.endColor = heading.resolvedStyle.color;
                courseRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                for (int i = 0; i < 48; i++)
                { float a = i * math.PI * 2 / 48; courseRing.SetPosition(i, new Vector3(math.cos(a) * 18, -1, math.sin(a) * 18)); }
            }
            if (courseRing != null) courseRing.gameObject.SetActive(true);
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
            heading = AddLabel(panel, "DEV PLAYGROUND", "text-s");
            detail = AddLabel(panel, "", "text-xs"); detail.AddToClassList("text-muted");
            detail.style.whiteSpace = WhiteSpace.Normal; detail.style.display = DisplayStyle.None;
            outcome = AddLabel(panel, report, "text-xs"); outcome.style.whiteSpace = WhiteSpace.Normal;
            ui.GetLayer(UILayer.Hud).Add(panel);
            beaconPositions = new double3[pads.Length];
            markers = new VisualElement { name = "dev-playground-beacons", pickingMode = PickingMode.Ignore };
            markers.style.position = Position.Absolute; markers.style.left = 0; markers.style.top = 0;
            ui.GetLayer(UILayer.World).Add(markers);
            visuals = new GameObject("Development playground beacons"); visuals.transform.SetParent(host.transform, false);
            var shader = Shader.Find("Hidden/Internal-Colored");
            if (shader != null) material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            foreach (var pad in pads)
            {
                pad.Label = AddLabel(markers, pad.Title, "text-xs");
                pad.Label.style.position = Position.Absolute;
                if (material == null) continue;
                var ringObject = new GameObject(pad.Id); ringObject.transform.SetParent(visuals.transform, false);
                var ring = ringObject.AddComponent<LineRenderer>(); pad.Ring = ring;
                ring.sharedMaterial = material; ring.useWorldSpace = false; ring.loop = true;
                ring.widthMultiplier = .6f; ring.positionCount = 48;
                ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; ring.receiveShadows = false;
                for (int i = 0; i < 48; i++)
                {
                    float a = i * math.PI * 2 / 48;
                    ring.SetPosition(i, new Vector3(math.cos(a) * 28, -1, math.sin(a) * 28));
                }
            }
        }
        static Label AddLabel(VisualElement parent, string text, string style)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore }; label.AddToClassList(style); parent.Add(label); return label;
        }
        void ClearPresentation()
        {
            panel?.RemoveFromHierarchy(); markers?.RemoveFromHierarchy();
            if (beaconPositions != null && host.Context.World != null && host.Context.World.IsCreated)
            {
                var hud = host.Context.World.GetExistingSystemManaged<Game.Presentation.Systems.HudSyncSystem>();
                if (hud != null) { hud.SetMarkers(default); hud.DisplayRangeM = 2000; }
            }
            beaconPositions = null;
            panel = markers = null; heading = detail = outcome = null;
            if (visuals != null) UnityEngine.Object.Destroy(visuals);
            if (material != null) UnityEngine.Object.Destroy(material);
            visuals = null; material = null; courseRing = null; courseGate = -1;
            foreach (var pad in pads) { pad.Ring = null; pad.Label = null; }
        }
        public void Dispose()
        {
            if (input != null) input.Controls.Flight.Interact.performed -= OnInteract;
            power?.Dispose(); campaign.Dispose(); salvage?.Dispose();
            active = false; ClearPresentation();
            launcher?.RemoveFromHierarchy(); launcher = null;
        }
    }
}
#endif
