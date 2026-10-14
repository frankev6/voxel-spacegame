#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Game.App.DevRuntime.Commands;
using Game.App.Flow;
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
            new Pad("flight", "01  FLIGHT / THRUSTERS", -160, 0, "W/S forward/reverse · A/D turn · Shift+A/D strafe · Q/E strafe\nCtrl boost · X brake · Mouse wheel camera zoom. Fly between the beacons to judge drift and braking."),
            new Pad("range", "02  DESTRUCTION / VFX", 0, 0, "Space fires. Aim at the narrow pod bridges to detach pieces; destroy the core for a drifting hulk.\nAlso tests tracers, impacts, sparks, explosions, smoke, debris, sound and collider updates. Reset restores all targets."),
            new Pad("combat", "03  OPT-IN ENEMY", 560, 0, "F starts one hostile fighter facing you. Real AI, weapons, damage, power and player-death flow.\nOne enemy per reset. It can pursue you: finish the trial or use playground reset before another trial."),
            new Pad("cargo", "04  CARGO / CREDITS", -160, -180, "F grants fuel and 1000 credits through the real inventory/wallet owners, then lists cargo. Repeat to exercise stacking/capacity.\nFull inventory/merchant screen integration is still pending."),
            new Pad("facts", "05  NARRATIVE FACTS", 0, -180, "F lists the real narrative fact store. Console: facts get=<name> or facts set=<name>=<value>, using a name from the list.\nThe complete dialogue/quest gameplay loop is still pending; this pad does not pretend to play it."),
            new Pad("grid", "06  GRID INSPECTION", 160, -180, "F inspects your real grid: cells, bricks, mesh/collider and physics state.\nUse this after combat to compare damage with what is rendered. F1: grid nearest=true inspects the closest other grid."),
            new Pad("campaign", "07  CAMPAIGN / TRAVEL / SAVES", -160, -360, "F leaves the empty sandbox for a fresh seeded campaign in the playground profile.\nUse the normal galaxy/travel and save/load commands there. F1: playground reset returns here. Campaign writes stay isolated."),
            new Pad("features", "08  FEATURE DIRECTORY", 0, -360, "Available here: flight, camera, thrusters, weapons, AI, voxel damage, breakup/hulks, VFX/audio, inventory/wallet, facts and grid inspection.\nCampaign pad: generated galaxy, streaming and travel/save services. Ship editor host, merchant/docking and dialogue/quest encounters still need integration; their entry pads will be enabled when playable.")
        };
        InputService input;
        UIRoot ui;
        VisualElement panel, markers, map;
        Label heading, detail, outcome, playerDot;
        GameObject visuals;
        Material material;
        double3 origin;
        bool active, entering, enemyRequested;
        int nearest = -1, pending;
        string report = "";
        internal DevPlayground(DevRuntimeHost host) { this.host = host; }
        internal void Bind(InputService service, UIRoot root)
        {
            input = service; ui = root;
            input.Controls.Flight.Interact.performed += OnInteract;
        }
        internal DevCommandResult Enter()
        {
            var ctx = host.Context;
            if (entering || !ctx.TryGetService<IGameFlow>(out var flow) || flow.IsTransitioning
                || !ctx.TryGetService<SaveService>(out var save) || save.IsBusy
                || !ctx.TryGetService<SceneFlow>(out var scenes)) return DevCommandResult.Fail("flow/save busy or unavailable");
            if (flow.CurrentScene != GameScene.World && flow.CurrentScene != GameScene.MainMenu)
                return DevCommandResult.Fail("Leave the current editor/dialog flow for World or MainMenu first");
            entering = true;
            ClearPresentation(); active = false;
            var operation = new EntryOperation(this, Begin(flow, save, scenes));
            return DevCommandResult.Pending(host.AddOperation(operation), "resetting isolated playground");
        }
        async Awaitable<FlowResult> Begin(IGameFlow flow, SaveService save, SceneFlow scenes)
        {
            try
            {
                bool ownProfile = false;
                foreach (var profile in save.ListProfiles())
                    if (profile.Id.Equals(save.ActiveProfile) && profile.DisplayName == "dev-playground") ownProfile = true;
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
        void Start()
        {
            var ctx = host.Context;
            var player = ctx.PlayerShipRoot;
            if (!DevEcs.TryGetUniversePosition(ctx, player, out origin)) throw new InvalidOperationException("player position unavailable");
            active = true; enemyRequested = false; pending = 0; nearest = -1;
            report = "Quiet sandbox ready. No hostile ships spawn until you activate COMBAT.";
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
        internal DevCommandResult Status() => DevCommandResult.Ok((active ? "Playground active. " : "Playground inactive. F1: playground to enter. ") + Directory() + "\n" + report);
        string Directory()
        {
            var text = new System.Text.StringBuilder("F1: playground reset | playground goto=<id>\n");
            foreach (var pad in pads) text.Append(pad.Id).Append(" — ").Append(pad.Title).Append('\n');
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
                case "range":
                    result = host.Commands.Execute("range", host.Context);
                    if (result.Kind != DevResultKind.Failed) result = DevCommandResult.Ok("Station ready ahead. Space to fire. Aim at a bridge to break a pod off. F1: playground reset restores it.");
                    break;
                case "combat":
                    if (enemyRequested) result = DevCommandResult.Ok("Enemy trial already started. playground reset restores the quiet sandbox.");
                    else
                    {
                        result = host.Commands.Execute("spawn blueprint=ship_fighter dx=0 dz=90 face=player team=2 ai=true", host.Context);
                        if (result.Kind != DevResultKind.Failed) enemyRequested = true;
                    }
                    break;
                case "cargo":
                    result = host.Commands.Execute("give item=resource_fuel count=5", host.Context);
                    if (result.Kind != DevResultKind.Failed)
                    {
                        result = host.Commands.Execute("give credits=1000", host.Context);
                        if (result.Kind != DevResultKind.Failed) result = host.Commands.Execute("inv", host.Context);
                    }
                    break;
                case "facts": result = host.Commands.Execute("facts list=\"\"", host.Context); break;
                case "grid": result = host.Commands.Execute("grid player=true", host.Context); break;
                case "campaign":
                    result = host.Commands.Execute("newgame seed=42 profile=dev-playground", host.Context);
                    if (result.Kind == DevResultKind.Pending) { active = false; ClearPresentation(); }
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
            if (!active) return;
            var ctx = host.Context;
            if (!ctx.TryGetService<GameSession>(out var session) || !session.IsSandbox || session.IsTransitioning)
            { active = false; ClearPresentation(); return; }
            int next = Nearest();
            if (next != nearest)
            {
                nearest = next;
                if (heading != null) heading.text = next < 0 ? "DEV PLAYGROUND  /  FLY TO A BEACON" : pads[next].Title + "  /  " + input.GetBindingDisplay("Flight/Interact") + " ACTIVATE";
                if (detail != null) detail.text = next < 0 ? "Flight/range/enemy: north row · Cargo/facts/grid: middle row · Campaign/directory: south row\nF1: playground goto=range (or combat, cargo, facts, grid, campaign, features, flight)" : pads[next].Detail;
            }
            if (pending != 0 && host.TryGetOperation(pending, out var operation) && operation.Status != DevOperationStatus.Running)
            { pending = 0; SetReport(operation.Status + ": " + operation.Message); }
            if (playerDot != null && DevEcs.TryGetUniversePosition(ctx, ctx.PlayerShipRoot, out var current))
            {
                var offset = current - origin;
                playerDot.style.left = (float)math.clamp((offset.x + 210) * .28, 0, 225);
                playerDot.style.top = (float)math.clamp((60 - offset.z) * .30, 0, 140);
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
        void SetReport(string value)
        {
            report = value ?? "";
            // Keep verbose diagnostics available in command output; the flight HUD remains bounded.
            if (outcome != null) outcome.text = report.Length > 650 ? report.Substring(0, 650) + "… (F1: playground status)" : report;
        }
        void BuildPresentation()
        {
            if (ui == null) return;
            panel = new VisualElement { name = "dev-playground", pickingMode = PickingMode.Ignore };
            panel.AddToClassList("panel"); panel.style.position = Position.Absolute; panel.style.left = 24; panel.style.top = 80;
            panel.style.width = 540; panel.style.paddingLeft = 16; panel.style.paddingRight = 16;
            panel.style.paddingTop = 12; panel.style.paddingBottom = 12;
            heading = AddLabel(panel, "DEV PLAYGROUND", "panel__title");
            detail = AddLabel(panel, "", "text-muted");
            detail.style.whiteSpace = WhiteSpace.Normal;
            outcome = AddLabel(panel, report, "text-mono"); outcome.style.whiteSpace = WhiteSpace.Normal;
            AddLabel(panel, "F1  playground reset  — restores the entire playground", "text-muted");
            ui.GetLayer(UILayer.Hud).Add(panel);
            map = new VisualElement { name = "dev-playground-map", pickingMode = PickingMode.Ignore };
            map.AddToClassList("panel"); map.style.position = Position.Absolute; map.style.right = 24; map.style.bottom = 32;
            map.style.width = 280; map.style.paddingLeft = 16; map.style.paddingTop = 12; map.style.paddingBottom = 12;
            AddLabel(map, "BEACONS / NORTH UP", "panel__title");
            var chart = new VisualElement { pickingMode = PickingMode.Ignore }; chart.style.height = 165; map.Add(chart);
            for (int i = 0; i < pads.Length; i++)
            {
                var dot = AddLabel(chart, (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), "text-muted");
                dot.style.position = Position.Absolute;
                dot.style.left = (pads[i].Offset.x + 210) * .28f; dot.style.top = (60 - pads[i].Offset.z) * .30f;
            }
            playerDot = AddLabel(chart, "+", "panel__title"); playerDot.style.position = Position.Absolute;
            AddLabel(map, "1 Flight    2 Range    3 Enemy\n4 Cargo     5 Facts    6 Grid\n7 Campaign  8 Directory\n+ Your ship", "text-mono");
            ui.GetLayer(UILayer.Hud).Add(map);
            markers = new VisualElement { name = "dev-playground-beacons", pickingMode = PickingMode.Ignore };
            markers.style.position = Position.Absolute; markers.style.left = 0; markers.style.top = 0;
            ui.GetLayer(UILayer.World).Add(markers);
            visuals = new GameObject("Development playground beacons"); visuals.transform.SetParent(host.transform, false);
            var shader = Shader.Find("Hidden/Internal-Colored");
            if (shader != null) material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            foreach (var pad in pads)
            {
                pad.Label = AddLabel(markers, "+ " + pad.Title, "text-mono");
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
            panel?.RemoveFromHierarchy(); markers?.RemoveFromHierarchy(); map?.RemoveFromHierarchy();
            panel = markers = map = null; heading = detail = outcome = playerDot = null;
            if (visuals != null) UnityEngine.Object.Destroy(visuals);
            if (material != null) UnityEngine.Object.Destroy(material);
            visuals = null; material = null;
            foreach (var pad in pads) { pad.Ring = null; pad.Label = null; }
        }
        public void Dispose()
        {
            if (input != null) input.Controls.Flight.Interact.performed -= OnInteract;
            active = false; ClearPresentation();
        }
    }
}
#endif
