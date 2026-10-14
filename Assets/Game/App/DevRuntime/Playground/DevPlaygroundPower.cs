#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Globalization;
using Game.App.EditorHost;
using Game.App.Flow;
using Game.App.Session;
using Game.Core;
using Game.Core.Flow;
using Game.Data.Combat;
using Game.Data.Definitions;
using Game.Input;
using Game.Persistence.Data;
using Game.Persistence.Services;
using Game.ShipEditor.Data;
using Game.ShipEditor.Services;
using Game.Simulation.Abilities;
using Game.Simulation.Power;
using Game.UI.Data;
using Game.UI.Services;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UIElements;

namespace Game.App.DevRuntime.Playground
{
    /// <summary>DEV-021: prepares a real fleet loadout before spawn and exercises its normal ability input.</summary>
    internal sealed class DevPlaygroundPower : ISceneReady, IDisposable
    {
        readonly DevContext context;
        readonly UIRoot ui;
        readonly SceneFlow flow;
        VisualElement panel, hiddenMap;
        Label energy, shield, abilities, feedback;
        Button shieldButton, boostButton;
        AbilityOperation operation;
        int operationId;
        double nextTick;
        string lastEnergy, lastShield, lastAbilities;
        GameSession trialSession;
        ProfileId trialProfile;
        bool disposed;

        internal DevPlaygroundPower(DevContext context, UIRoot ui)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
            if (!context.TryGetService(out flow)) throw new InvalidOperationException("SceneFlow unavailable.");
            flow.RegisterReady(this);
        }

        // PlayerShipSpawner borrows the flagship at 200; its source must be committed beforehand.
        public int SceneReadyOrder => SceneHookOrder.PlayerShip - 1;
        public bool IsSceneReadyComplete(in SceneReadyContext entry) => true;
        public void OnSceneReady(in SceneReadyContext entry)
        {
            Close();
            trialSession = null;
            if (entry.Scene != GameScene.World || entry.Reason != SceneEntryReason.DevQuickStart
                || !IsPlayground(context, out var session) || !session.SpawnPlayerInWorld) return;
            if (!context.TryGetService<EditorHostServices>(out var editor))
                throw new InvalidOperationException("Power trial requires the registered editor/content owners.");
            var ship = session.Fleet.Flagship;
            if (ship == null || ship.StructureHandle >= 0 || ship.Structure.IsPinned)
                throw new InvalidOperationException("Power trial must prepare the unspawned flagship.");
            if (!editor.Content.Catalog.TryGetBlockIndex(new ContentId("block_shield_small"), out ushort block))
                throw new InvalidOperationException("Approved small shield block is unavailable.");
            trialSession = session;
            context.TryGetService<SaveService>(out var save); trialProfile = save.ActiveProfile;
            var source = ship.Structure;
            for (int i = 0; i < source.Data.Instances.Length; i++)
                if (source.Data.Instances[i].Block == block) return;

            using var data = new EditorContext(editor.Config, editor.Content.Catalog,
                editor.Content.BlockDefs, editor.Content.Orientations, 16);
            using var edit = new EditSession(new ShipEditorEntryArgs {
                Source = source, PersistentId = ship.PersistentId,
                Options = new EditSessionOptions { Creative = true, TargetKind = EditTargetKind.FleetShip, Title = "Playground shield loadout" },
                CommitTarget = new FleetCommitTarget(session.Fleet, ship.PersistentId)
            }, data);
            using var batch = new EditBatch();
            var report = new ApplyReport();
            if (!source.TryGetCellBounds(out var min, out var max)) throw new InvalidOperationException("Empty playground flagship.");
            // Search attached upper faces, centre first. EditSession validates footprint, clearance,
            // attachment and connectivity; no cells, function HP or live simulation state are written here.
            int attempts = 0, width = max.x - min.x + 1, depth = max.z - min.z + 1;
            int centerX = min.x + width / 2, centerZ = min.z + depth / 2;
            bool placed = false;
            for (int y = max.y; y >= min.y && !placed; y--)
                for (int z = 0; z < depth && !placed; z++)
                    for (int x = 0; x < width && !placed; x++)
                    {
                        int px = centerX + CenterOffset(x), pz = centerZ + CenterOffset(z);
                        if (px < min.x || px > max.x || pz < min.z || pz > max.z) continue;
                        var support = new int3(px, y, pz);
                        if (source.GetCell(support).IsAir || !source.GetCell(support + new int3(0, 1, 0)).IsAir) continue;
                        if (++attempts > 128) throw new InvalidOperationException("No valid shield mount within the bounded loadout search.");
                        batch.Clear();
                        batch.Ops.Add(new DiffOp { Kind = DiffOpKind.Add, Record = new BlockRecord {
                            Block = block, Origin = support + new int3(0, 1, 0), Orient = 0, Color = 0, DamageOffset = -1
                        } });
                        placed = edit.TryApply(batch, AtomicityMode.AllOrNothing, 1, ToolId.Place, "Fit approved shield emitter", report);
                    }
            if (!placed) throw new InvalidOperationException("The actual flagship has no valid shield mount.");
            var committed = edit.Commit();
            if (committed.Status != CommitStatus.Committed) throw new InvalidOperationException("Power loadout commit: " + committed.Message);
        }
        static int CenterOffset(int index) => (index & 1) == 0 ? index / 2 : -(index + 1) / 2;
        static bool IsPlayground(DevContext ctx, out GameSession session)
        {
            if (!ctx.TryGetService(out session) || !session.IsSandbox || !ctx.TryGetService<SaveService>(out var save)) return false;
            foreach (var profile in save.ListProfiles())
                if (profile.Id.Equals(save.ActiveProfile)) return profile.DisplayName == "dev-playground";
            return false;
        }
        bool IsLiveTrial(DevContext ctx, out GameSession session)
            => ctx.TryGetService(out session) && ReferenceEquals(session, trialSession) && session.IsSandbox
                && ctx.TryGetService<SaveService>(out var save) && save.ActiveProfile.Equals(trialProfile);

        internal DevCommandResult Activate(DevContext ctx, UIRoot root, string action)
        {
            if (disposed || ctx != context || root != ui || !IsLiveTrial(ctx, out var session) || session.IsTransitioning
                || !ctx.TryGetService<InputService>(out var input) || input.CurrentContext != InputContext.Flight)
                return DevCommandResult.Fail("Power trials require the isolated playground in Flight context.");
            if (action != "power" && action != "shield" && action != "boost") return DevCommandResult.Fail("Unknown power trial action.");
            var player = ctx.PlayerShipRoot;
            if (player == Entity.Null || !ctx.EntityManager.HasBuffer<AbilitySlot>(player))
                return DevCommandResult.Fail("Player abilities are not ready; playground reset restores the loadout.");
            Show();
            if (action == "power")
            {
                var result = DevCommandResult.Ok("Power trial open. Toggle the real shield or activate Speed Boost; hold W to feel boosted thrust and Space to compare weapon demand. COMBAT supplies real incoming damage.");
                feedback.text = result.Text; Tick(ctx, true); return result;
            }
            if (operation != null && operation.Status == DevOperationStatus.Running)
                return DevCommandResult.Fail("An ability request is still pending.");
            ctx.EntityManager.CompleteAllTrackedJobs();
            var id = new ContentId(action == "shield" ? "ability_shield_toggle" : "ability_speed_boost").Hash;
            if (!FindSlot(ctx.EntityManager, player, id, out int index, out var slot))
                return Refuse("The live ship has no " + action + " ability. A destroyed granting block removes it; playground reset restores it.");
            if (slot.Blocked != 0 || slot.State != AbilitySlotState.Ready || slot.Charges == 0)
                return Refuse(action + " unavailable: " + slot.State + ", " + slot.Charges + " charges, " + Seconds(slot.CooldownRemainingTicks) + " s cooldown. Shield-on also requires at least 30% HP.");
            var binding = input.Controls.asset.FindAction("Flight/Ability" + (index + 1), false);
            if (binding == null || !binding.enabled || binding.controls.Count == 0 || !(binding.controls[0] is KeyControl key))
                return Refuse("This trial requires a resolved keyboard binding for ability slot " + (index + 1) + ". Use the normal ability control or rebind it in Settings.");
            operation = new AbilityOperation(this, ctx, player, id, action == "shield", slot, key.keyCode);
            ctx.Input.PressAction(binding, 0);
            operationId = ctx.Host.AddOperation(operation);
            feedback.text = "Requested " + action + " through Flight/Ability" + (index + 1) + "; waiting for the real fixed-step result.";
            return DevCommandResult.Pending(operationId, feedback.text);
        }
        DevCommandResult Refuse(string text) { feedback.text = text; return DevCommandResult.Fail(text); }
        static bool FindSlot(EntityManager em, Entity player, ContentHash id, out int index, out AbilitySlot slot)
        {
            var slots = em.GetBuffer<AbilitySlot>(player, true);
            for (int i = 0; i < math.min(4, slots.Length); i++)
                if (slots[i].Ability == id) { index = i; slot = slots[i]; return true; }
            index = -1; slot = default; return false;
        }

        void Show()
        {
            if (panel != null) return;
            hiddenMap = ui.RootElement.Q<VisualElement>("dev-playground-map");
            if (hiddenMap != null) hiddenMap.style.display = DisplayStyle.None;
            panel = new VisualElement { name = "dev-power-trial" }; panel.AddToClassList("panel");
            panel.style.position = Position.Absolute; panel.style.right = 24; panel.style.top = 90;
            panel.style.width = 390; panel.style.maxWidth = Length.Percent(45); panel.style.paddingLeft = 14;
            panel.style.paddingRight = 14; panel.style.paddingTop = 12; panel.style.paddingBottom = 12;
            var title = new Label("POWER / SHIELDS / ABILITIES"); title.AddToClassList("label"); panel.Add(title);
            energy = Row("dev-power-energy"); shield = Row("dev-power-shield"); abilities = Row("dev-power-abilities");
            feedback = Row("dev-power-feedback"); feedback.style.whiteSpace = WhiteSpace.Normal;
            shieldButton = ActionButton("dev-power-toggle", "Toggle shield", "shield");
            boostButton = ActionButton("dev-power-boost", "Speed Boost", "boost");
            var hint = Row("dev-power-hint"); hint.style.whiteSpace = WhiteSpace.Normal;
            hint.text = "W: thrust · Space: weapons · 1–4: equipped abilities. Boost costs 15 E and lasts 1 s. Shield draw is 8 E/s; shield-on requires 30% HP. Visit COMBAT for damage and regeneration.";
            var close = new Button(Close) { name = "dev-power-close", text = "Close instruments" };
            close.AddToClassList("btn"); close.AddToClassList("btn--secondary"); panel.Add(close);
            ui.GetLayer(UILayer.Hud).Add(panel); nextTick = -1;
        }
        Label Row(string name)
        {
            var label = new Label { name = name }; label.AddToClassList("label"); label.AddToClassList("label--mono");
            label.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(label); return label;
        }
        Button ActionButton(string name, string title, string action)
        {
            var button = new Button(() => {
                var result = Activate(context, ui, action);
                if (feedback != null) feedback.text = result.Text;
            }) { name = name, text = title };
            button.AddToClassList("btn"); button.AddToClassList("btn--secondary"); panel.Add(button); return button;
        }
        internal void Tick(DevContext ctx) => Tick(ctx, false);
        void Tick(DevContext ctx, bool force)
        {
            if (disposed || panel == null) return;
            if (!IsLiveTrial(ctx, out var session) || session.IsTransitioning) { Close(); return; }
            bool visible = ctx.TryGetService<InputService>(out var currentInput) && currentInput.CurrentContext == InputContext.Flight;
            panel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (operationId != 0 && ctx.Host.TryGetOperation(operationId, out var observed) && observed.Status != DevOperationStatus.Running)
            { feedback.text = observed.Status + ": " + observed.Message; operation?.Cancel("Ability observation finished."); operationId = 0; }
            // No owner writes or per-frame formatting. Read the same live state as the production HUD.
            if (!force && ctx.Tick < nextTick) return;
            nextTick = ctx.Tick + 6;
            var em = ctx.EntityManager; em.CompleteAllTrackedJobs(); var player = ctx.PlayerShipRoot;
            if (player == Entity.Null || !em.HasComponent<ShipPower>(player) || !em.HasComponent<ShieldState>(player)
                || !em.HasBuffer<AbilitySlot>(player)) { Close(); return; }
            var p = em.GetComponentData<ShipPower>(player); var s = em.GetComponentData<ShieldState>(player);
            Set(energy, ref lastEnergy, "Energy " + Number(p.Stored) + " / " + Number(p.Capacity) + " E\nGeneration " + Number(p.Generation) + " · demand " + Number(p.DemandPerSecond) + " · granted " + Number(p.GrantedPerSecond) + " E/s\n" + ((p.Flags & ShipPowerFlags.Brownout) != 0 ? "BROWNOUT" : "Power supplied"));
            Set(shield, ref lastShield, "Shield " + (s.Active != 0 ? "UP" : "DOWN") + " · " + Number(s.Hp) + " / " + Number(s.MaxHp) + " HP · regen " + Number(s.RegenPerSecond) + "/s");
            string rows = "";
            var slots = em.GetBuffer<AbilitySlot>(player, true);
            for (int i = 0; i < math.min(4, slots.Length); i++)
            {
                var slot = slots[i]; string name = slot.Ability == new ContentId("ability_speed_boost").Hash ? "Speed Boost" : slot.Ability == new ContentId("ability_shield_toggle").Hash ? "Shield Toggle" : "Ability";
                rows += (i == 0 ? "" : "\n") + (i + 1) + " " + name + " · " + slot.State + " · charges " + slot.Charges + " · cooldown " + Seconds(slot.CooldownRemainingTicks) + "s";
            }
            Set(abilities, ref lastAbilities, rows);
            bool flight = ctx.TryGetService<InputService>(out var input) && input.CurrentContext == InputContext.Flight;
            bool idle = operation == null || operation.Status != DevOperationStatus.Running;
            shieldButton.SetEnabled(flight && idle); boostButton.SetEnabled(flight && idle);
        }
        static void Set(Label label, ref string previous, string text) { if (previous != text) { label.text = text; previous = text; } }
        static string Number(float value) => value.ToString("0.0", CultureInfo.InvariantCulture);
        static string Seconds(float ticks) => Number(ticks / SimTime.TicksPerSecond);
        void Close()
        {
            operation?.Cancel("Power trial closed."); operation = null; operationId = 0;
            if (hiddenMap != null) hiddenMap.style.display = DisplayStyle.Flex;
            hiddenMap = null;
            panel?.RemoveFromHierarchy(); panel = null; energy = shield = abilities = feedback = null;
            shieldButton = boostButton = null; lastEnergy = lastShield = lastAbilities = null;
        }
        public void Dispose() { if (disposed) return; disposed = true; Close(); flow.UnregisterReady(this); }

        sealed class AbilityOperation : IDevOperation
        {
            readonly DevPlaygroundPower owner;
            readonly DevContext context;
            readonly World world;
            readonly Entity player;
            readonly ContentHash ability;
            readonly bool toggle;
            readonly byte charges, requested;
            readonly uint activeUntil;
            readonly Key key;
            readonly double startedTick, startedTime;
            readonly int startedFrame;
            bool released;
            public DevOperationStatus Status { get; private set; }
            public string Message { get; private set; } = "Waiting for ability input and fixed-step owners";
            public double TimeoutSeconds => 6;
            internal AbilityOperation(DevPlaygroundPower owner, DevContext ctx, Entity player, ContentHash ability, bool toggle, AbilitySlot slot, Key key)
            {
                this.owner = owner; context = ctx; world = ctx.World; this.player = player; this.ability = ability; this.toggle = toggle; this.key = key;
                charges = slot.Charges; activeUntil = slot.ActiveUntilTick;
                requested = ctx.EntityManager.HasComponent<ShieldControl>(player) ? ctx.EntityManager.GetComponentData<ShieldControl>(player).RequestedOn : (byte)0;
                startedTick = ctx.Tick; startedFrame = Time.frameCount; startedTime = Time.realtimeSinceStartupAsDouble;
            }
            public void Tick(DevContext ctx)
            {
                if (Status != DevOperationStatus.Running) return;
                if (ctx.World != world || !world.IsCreated || !owner.IsLiveTrial(ctx, out var session) || session.IsTransitioning
                    || !ctx.TryGetService<InputService>(out var input) || input.CurrentContext != InputContext.Flight)
                { Cancel("Ability cancelled: playground or Flight context changed."); return; }
                if (Time.frameCount > startedFrame + 1) Release();
                if (Time.realtimeSinceStartupAsDouble - startedTime > 4) { Cancel("No live ability result within 4 seconds; input released."); return; }
                if (ctx.Tick <= startedTick) return;
                var em = ctx.EntityManager; em.CompleteAllTrackedJobs();
                if (!em.Exists(player) || ctx.PlayerShipRoot != player || !em.HasBuffer<AbilitySlot>(player)
                    || !FindSlot(em, player, ability, out _, out var slot)) { Cancel("The player or granting block disappeared."); return; }
                bool changed = toggle ? em.GetComponentData<ShieldControl>(player).RequestedOn != requested : slot.ActiveUntilTick > activeUntil && slot.Charges < charges;
                if (changed && released)
                {
                    if (toggle)
                    {
                        var s = em.GetComponentData<ShieldState>(player);
                        byte target = requested == 0 ? (byte)1 : (byte)0;
                        if (s.Active != target) return;
                        Message = "Shield " + (s.Active != 0 ? "UP" : "DOWN") + " · " + Number(s.Hp) + " / " + Number(s.MaxHp) + " HP. Normal shield and power owners confirmed.";
                    }
                    else
                    {
                        if (!em.HasComponent<ShipStatMultipliers>(player)) return;
                        var stats = em.GetComponentData<ShipStatMultipliers>(player);
                        if (stats.Get(StatId.Thrust) <= 1 || stats.Get(StatId.MaxSpeed) <= 1) return;
                        Message = "Speed Boost active: thrust ×" + Number(stats.Get(StatId.Thrust)) + ", speed cap ×" + Number(stats.Get(StatId.MaxSpeed)) + ". " + slot.Charges + " charges left; normal energy grant confirmed. Hold W to fly.";
                    }
                    Status = DevOperationStatus.Succeeded; return;
                }
                if (!changed && released && ctx.Tick > startedTick + 12 && slot.State != AbilitySlotState.PendingPower)
                    Cancel("Ability not activated: " + slot.State + ", blocked=" + slot.Blocked + ". Check energy, charges, cooldown and control locks.");
            }
            void Release() { if (released) return; context.Input.SetKeys(key.ToString(), 0, true); released = true; }
            internal void Cancel(string reason)
            { Release(); if (Status != DevOperationStatus.Running) return; Status = DevOperationStatus.Failed; Message = reason; }
        }
    }
}
#endif
