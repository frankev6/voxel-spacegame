using System;
using Game.Core;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Galaxy.Components;
using Game.Presentation.Components;
using Game.Presentation.Presenters;
using Game.Simulation.Commands;
using Game.UI.ViewModels;
using Game.UI.Views;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;

namespace Game.Presentation.Systems
{
    /// <summary>Quantized, persistent view-model updates after the authoritative snapshot jobs finish.</summary>
    [UpdateInGroup(typeof(PresentationBridgeGroup))]
    [UpdateAfter(typeof(HudSnapshotSystem))]
    [UpdateBefore(typeof(ManagedEventDrainSystem))]
    public partial class HudSyncSystem : SystemBase, IHudRadarSource
    {
        readonly HudRadarBlipView[] radar = new HudRadarBlipView[64];
        readonly string[] weaponKeys = new string[3], abilityKeys = new string[4];
        readonly Game.Data.Definitions.AbilityDefinition[] tooltipFor = new Game.Data.Definitions.AbilityDefinition[4];
        readonly byte[] tooltipBlocked = new byte[4];
        readonly ProfilerMarker marker = new("HudSyncSystem.Apply");
        EntityQuery snapshots, players, origins, cameras;
        HudViewModel model;
        IHudNames names;
        Func<string, string> bindingDisplay;
        string interactKey;
        int radarCount, radarVersion;
        uint lastSourceRadar;
        bool radarInitialized;
        readonly System.Collections.Generic.List<double3> markers = new();
        readonly System.Collections.Generic.List<string> markerLabels = new();
        public ReadOnlySpan<HudRadarBlipView> Blips => radar.AsSpan(0, radarCount);
        /// <summary>Extra point-of-interest markers in star-system coordinates, drawn as POI blips after the
        /// simulation contacts and clamped to the rim when beyond range (QA-001: the dev playground's beacons use the
        /// real radar instead of a second map). Replaces the previous set; an empty span clears it.</summary>
        /// <summary>Displayed radar range; contacts are gathered within 2000 m and those beyond this range sit on the rim.</summary>
        public float DisplayRangeM { get => displayRange; set => displayRange = math.isfinite(value) ? math.clamp(value, 100, 2000) : 2000; }
        float displayRange = 2000;
        public void SetMarkers(ReadOnlySpan<double3> positions, ReadOnlySpan<string> labels = default)
        {
            markers.Clear(); markerLabels.Clear();
            for (int i = 0; i < positions.Length; i++) { markers.Add(positions[i]); markerLabels.Add(i < labels.Length ? labels[i] : null); }
        }
        protected override void OnCreate()
        {
            snapshots = GetEntityQuery(ComponentType.ReadOnly<HudSnapshot>(), ComponentType.ReadOnly<HudRadarBlip>());
            players = GetEntityQuery(ComponentType.ReadOnly<PlayerShip>(), ComponentType.ReadOnly<LocalTransform>());
            origins = GetEntityQuery(ComponentType.ReadOnly<FrameOrigin>());
            cameras = GetEntityQuery(ComponentType.ReadOnly<MainCameraState>());
            weaponBlocks = GetEntityQuery(ComponentType.ReadOnly<Game.Voxel.Model.FunctionalBlock>(), ComponentType.ReadOnly<Game.Simulation.Weapons.WeaponMount>());
            RequireForUpdate(snapshots); RequireForUpdate<GameplayActive>();
        }
        public void Bind(HudViewModel vm, IHudNames names, Func<string, string> bindingDisplay)
        {
            model = vm ?? throw new ArgumentNullException(nameof(vm));
            this.names = names ?? throw new ArgumentNullException(nameof(names));
            this.bindingDisplay = bindingDisplay ?? throw new ArgumentNullException(nameof(bindingDisplay));
            radarInitialized = false; radarCount = 0;
            RefreshBindings();
        }
        public void Unbind() { model = null; names = null; bindingDisplay = null; radarCount = 0; radarInitialized = false; }
        /// <summary>Called by the input owner after a rebind or device change.</summary>
        public void RefreshBindings()
        {
            if (model == null) return;
            weaponKeys[0] = bindingDisplay("Flight/FirePrimary"); weaponKeys[1] = bindingDisplay("Flight/FireSecondary"); weaponKeys[2] = bindingDisplay("Flight/FireTertiary");
            for (int i = 0; i < abilityKeys.Length; i++) abilityKeys[i] = bindingDisplay("Flight/Ability" + (i + 1));
            interactKey = bindingDisplay("Flight/Interact");
        }
        protected override void OnUpdate()
        {
            if (model == null) return;
            CompleteDependency();
            EntityManager.CompleteDependencyBeforeRO<HudSnapshot>();
            EntityManager.CompleteDependencyBeforeRO<HudRadarBlip>();
            var snapshot = snapshots.GetSingleton<HudSnapshot>();
            var contacts = snapshots.GetSingletonBuffer<HudRadarBlip>(true).AsNativeArray();
            double3 position = 0;
            bool hasPosition = players.CalculateEntityCount() == 1;
            if (hasPosition)
            {
                EntityManager.CompleteDependencyBeforeRO<LocalTransform>();
                position = players.GetSingleton<LocalTransform>().Position;
                if (!origins.IsEmptyIgnoreFilter) position += origins.GetSingleton<FrameOrigin>().OriginInStarSystem;
            }
            float yaw = model.RadarYawDeg;
            if (!cameras.IsEmptyIgnoreFilter)
            {
                var forward = math.forward(cameras.GetSingleton<MainCameraState>().Rotation);
                yaw = math.degrees(math.atan2(forward.x, forward.z));
            }
            Apply(snapshot, contacts, position, hasPosition, yaw);
            Marks(snapshot);
        }

        // HUD redesign (2026-10-21): the reticle, the lead pip and the arcs at the player's ship (HudMarks).
        float hullSeen = -1, shieldSeen = -1, hullAlpha, shieldAlpha, energyAlpha, leadSpeed;
        double hullHitAt = double.NegativeInfinity, leadSpeedAt = double.NegativeInfinity;
        EntityQuery weaponBlocks;
        /// <summary>Seconds an arc stays after the last hit; a full bar never shows (Frank, 2026-10-21).</summary>
        const float HitHoldSeconds = 3, FadeSeconds = .4f;
        void Marks(in HudSnapshot value)
        {
            double now = UnityEngine.Time.unscaledTimeAsDouble;
            float dt = math.clamp(UnityEngine.Time.unscaledDeltaTime, 0, .1f);
            var preset = Game.Presentation.Services.FlightCameraPreset.Current;
            bool ship = model.HasShip && !model.IsHulk, hasCamera = !cameras.IsEmptyIgnoreFilter;
            var camera = hasCamera ? cameras.GetSingleton<MainCameraState>() : default;
            float hull = math.saturate(value.Hull01), shield = value.ShieldMax > 0 ? math.saturate(value.ShieldHp / value.ShieldMax) : 1;
            float energy = value.EnergyMax > 0 ? math.saturate(value.Energy / value.EnergyMax) : 1;
            if (ship)
            {
                if (hullSeen >= 0 && hull < hullSeen - 1e-4f) hullHitAt = now;
                hullSeen = hull; shieldSeen = shield;
            }
            else hullSeen = shieldSeen = -1;
            // Hull: on a hit or below 30 %. Shield: below full it is either just hit, recharging or low, so it shows until full again.
            // Energy: while boosting or below 25 %.
            // a scratch (above 97 %) reads as full: the arc would only draw a full bar
            bool hullOn = ship && hull < .97f && (now - hullHitAt < HitHoldSeconds || hull < .3f);
            bool shieldOn = ship && value.ShieldMax > 0 && shield < .995f;
            bool energyOn = ship && value.EnergyMax > 0 && energy < .995f && ((value.Flags & HudFlags.Boosting) != 0 || energy < .25f);
            hullAlpha = Fade(hullAlpha, hullOn, dt); shieldAlpha = Fade(shieldAlpha, shieldOn, dt); energyAlpha = Fade(energyAlpha, energyOn, dt);

            UnityEngine.Vector2 shipPoint = model.ShipPoint; float shipRadius = model.ShipRadiusFraction; bool showShip = false;
            Entity player = players.CalculateEntityCount() == 1 ? players.GetSingletonEntity() : Entity.Null;
            if (ship && hasCamera && player != Entity.Null && Bounds(player, out float3 centre, out float radius) && Project(camera, centre, out var point, out float depth))
            {
                shipPoint = point; showShip = true;
                shipRadius = radius / (depth * math.tan(math.radians(camera.VerticalFovDeg) * .5f)) * .5f;
            }
            // The reticle is the cursor: the system cursor hides in flight. Mouse look puts it at the preset's reticle height.
            UnityEngine.Vector2 reticle = new(.5f, .5f);
            if (preset.MouseLook) reticle = new UnityEngine.Vector2(.5f, 1 - preset.ReticleHeight);
            else if (UnityEngine.InputSystem.Mouse.current != null && hasCamera && math.all(camera.ScreenSizePx > 0))
            {
                var mouse = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                reticle = new UnityEngine.Vector2(math.saturate(mouse.x / camera.ScreenSizePx.x), math.saturate(1 - mouse.y / camera.ScreenSizePx.y));
            }
            var sampler = World.GetExistingSystemManaged<Game.Presentation.Input.PlayerInputSampleSystem>();
            Entity target = sampler != null ? sampler.AssistTarget : Entity.Null;
            bool locked = ship && target != Entity.Null && EntityManager.Exists(target);
            if (!locked && ship && player != Entity.Null && EntityManager.HasComponent<Game.Simulation.Weapons.ShipTarget>(player))
            {
                var held = EntityManager.GetComponentData<Game.Simulation.Weapons.ShipTarget>(player).Value;
                if (held != Entity.Null && EntityManager.Exists(held) && value.TargetRelation == (byte)Game.Simulation.FactionStance.Hostile) target = held;
            }
            UnityEngine.Vector2 leadPoint = model.LeadPoint; bool showLead = false;
            if (ship && hasCamera && target != Entity.Null && EntityManager.Exists(target) && Lead(player, target, now, out float3 intercept) && Project(camera, intercept, out var leadScreen, out _))
            { leadPoint = leadScreen; showLead = true; }
            bool showReticle = ship;
            if (showReticle == model.ShowReticle && locked == model.Locked && showLead == model.ShowLead && showShip == model.ShowShip
                && (reticle - model.ReticlePoint).sqrMagnitude < 1e-8f && (!showLead || (leadPoint - model.LeadPoint).sqrMagnitude < 1e-8f)
                && (!showShip || (shipPoint - model.ShipPoint).sqrMagnitude < 1e-8f && math.abs(shipRadius - model.ShipRadiusFraction) < 1e-4f)
                && hullAlpha == model.HullArc && shieldAlpha == model.ShieldArc && energyAlpha == model.EnergyArc) return;
            model.ShowReticle = showReticle; model.Locked = locked; model.ReticlePoint = reticle; model.ShowLead = showLead; model.LeadPoint = leadPoint;
            model.ShowShip = showShip; model.ShipPoint = shipPoint; model.ShipRadiusFraction = shipRadius;
            model.HullArc = hullAlpha; model.ShieldArc = shieldAlpha; model.EnergyArc = energyAlpha;
            model.MarksVersion++;
        }
        static float Fade(float alpha, bool on, float dt) => math.saturate(alpha + (on ? 1 : -1) * dt / FadeSeconds);
        bool Bounds(Entity entity, out float3 centre, out float radius)
        {
            centre = default; radius = 0;
            if (!EntityManager.HasComponent<LocalToWorld>(entity) || !EntityManager.HasComponent<Game.Voxel.Model.VoxelGrid>(entity)) return false;
            var grid = EntityManager.GetComponentData<Game.Voxel.Model.VoxelGrid>(entity);
            centre = math.transform(EntityManager.GetComponentData<LocalToWorld>(entity).Value, ((float3)grid.CellMin + (float3)(grid.CellMax + 1)) * (.5f * grid.CellSize));
            // the longer horizontal half-extent: a hull seen from behind and above, not the bounding sphere of a tall or long box
            var extent = (float3)(grid.CellMax + 1 - grid.CellMin) * grid.CellSize;
            radius = math.max(extent.x, extent.z) * .5f;
            return true;
        }
        static bool Project(in MainCameraState camera, float3 world, out UnityEngine.Vector2 point, out float depth)
        {
            float4 clip = math.mul(camera.ViewProjection, new float4(world, 1));
            point = default; depth = clip.w;
            if (!(clip.w > camera.NearClip)) return false;
            float2 ndc = clip.xy / clip.w;
            point = new UnityEngine.Vector2(ndc.x * .5f + .5f, .5f - ndc.y * .5f);
            return math.all(math.abs(ndc) < 1.2f);
        }
        /// <summary>B: where a primary-group round meets the target, from the player's first projectile weapon (its speed is re-read
        /// every second); two fixed-point steps of the intercept time, as the weapons' own lead converges.</summary>
        bool Lead(Entity player, Entity target, double now, out float3 intercept)
        {
            intercept = default;
            if (player == Entity.Null || !Bounds(target, out float3 centre, out _) || !EntityManager.HasComponent<LocalToWorld>(player)) return false;
            if (now - leadSpeedAt > 1) { leadSpeedAt = now; leadSpeed = ProjectileSpeed(player); }
            if (!(leadSpeed > 1)) return false;
            float3 shooter = EntityManager.GetComponentData<LocalToWorld>(player).Position;
            float3 relative = Velocity(target) - Velocity(player);
            float time = math.distance(centre, shooter) / leadSpeed;
            for (int i = 0; i < 2; i++) time = math.distance(centre + relative * time, shooter) / leadSpeed;
            if (!(time < 6)) return false;
            intercept = centre + relative * time;
            return math.distancesq(intercept, centre) > .25f;   // a still target needs no pip
        }
        float3 Velocity(Entity entity) => EntityManager.HasComponent<Unity.Physics.PhysicsVelocity>(entity) ? EntityManager.GetComponentData<Unity.Physics.PhysicsVelocity>(entity).Linear : float3.zero;
        /// <summary>ADR-0122: the line under the speed: the phase, or why a press was refused (2.5 s).</summary>
        static string CruiseLine(in HudSnapshot value) => value.CruisePhase == 2 ? "CRUISE" : value.CruisePhase == 1 ? "CRUISE · SPOOLING"
            : value.CruiseBlockAgeTicks >= 150 ? "" : value.CruiseBlock switch
            { 1 => "CRUISE · TOO CLOSE", 2 => "CRUISE · MASS LOCKED", 3 => "CRUISE · HIT", _ => "" };
        float ProjectileSpeed(Entity player)
        {
            if (!SystemAPI.TryGetSingleton<Game.Simulation.Weapons.WeaponDefsRef>(out var defs) || !defs.Value.IsCreated) return 0;
            using var blocks = weaponBlocks.ToComponentDataArray<Game.Voxel.Model.FunctionalBlock>(Allocator.Temp);
            using var mounts = weaponBlocks.ToComponentDataArray<Game.Simulation.Weapons.WeaponMount>(Allocator.Temp);
            ref var weapons = ref defs.Value.Value.Weapons;
            for (int i = 0; i < blocks.Length; i++)
            {
                if (blocks[i].Grid != player || mounts[i].Initialized == 0 || mounts[i].WeaponIndex == 0 || mounts[i].WeaponIndex >= weapons.Length) continue;
                ref var weapon = ref weapons[mounts[i].WeaponIndex];
                if (weapon.Delivery == Game.Data.Definitions.WeaponDelivery.Projectile && weapon.ProjectileSpeed > 0) return weapon.ProjectileSpeed;
            }
            return 0;
        }
        internal void Apply(in HudSnapshot value, NativeArray<HudRadarBlip> contacts, double3 playerPosition, bool hasPosition, float yaw)
        {
            if (model == null) return;
            using var scope = marker.Auto();
            bool ship = (value.Flags & HudFlags.HasShip) != 0;
            model.HasShip = ship; model.IsHulk = (value.Flags & HudFlags.IsHulk) != 0;
            if (!model.IsHulk)
            {
                model.HullFraction = Fraction(value.Hull01, 500); model.HullPercent = Rounded(value.Hull01 * 100);
                model.ShieldValue = Rounded(value.ShieldHp); model.ShieldMax = Rounded(value.ShieldMax);
                model.ShieldFraction = Fraction(value.ShieldMax > 0 ? value.ShieldHp / value.ShieldMax : 0, 500);
                model.EnergyValue = Rounded(value.Energy); model.EnergyMax = Rounded(value.EnergyMax);
                model.EnergyFraction = Fraction(value.EnergyMax > 0 ? value.Energy / value.EnergyMax : 0, 500);
            }
            model.ShieldStatus = value.ShieldStatus; model.SpeedMps = Rounded(value.SpeedMps);
            model.Boosting = (value.Flags & HudFlags.Boosting) != 0;
            model.Cruising = ship && value.CruisePhase == 2; model.CruiseText = ship ? CruiseLine(value) : "";
            model.HasTarget = ship && (value.Flags & HudFlags.HasTarget) != 0;
            model.TargetName = !model.HasTarget ? "" : value.TargetPilot.Length > 0 ? PilotText(value.TargetPilot) : names.TargetName(value.TargetNameHash, value.TargetFaction);
            model.TargetRelation = value.TargetRelation;
            model.TargetHullFraction = Fraction(value.TargetHull01, 500); model.TargetShieldFraction = Fraction(value.TargetShield01, 500);
            model.TargetDistanceM = Rounded(value.TargetDistanceM);
            model.HasInteraction = ship && (value.Flags & HudFlags.HasInteraction) != 0;
            var interaction = names.Interaction(value);
            model.InteractionTargetName = interaction.Name; model.InteractionDetailText = interaction.Detail; model.InteractionBlocked = interaction.Blocked;
            model.InteractionVerbText = value.InteractionKind switch { 1 => "Dock", 2 => "Inspect", 3 => "Jump", 4 => "Hail", 5 => "Salvage", _ => "" };
            model.InteractionKeyText = interactKey; model.InteractionInRange = value.InteractionInRange != 0;
            model.StarSystemName = names.SystemName(value.StarSystemIdValue);
            var route = names.Route; model.HasRoute = route.Visible; model.RouteText = route.Text; model.RouteBlocked = route.Blocked;
            for (int i = 0; i < model.WeaponGroups.Count; i++)
            {
                var row = model.WeaponGroups[i]; var group = i < value.WeaponGroups.Length ? value.WeaponGroups[i] : default;
                row.GroupNumber = i + 1; row.WeaponCount = group.WeaponCount; row.ReadyFraction = Fraction(group.Ready01, 100);
                row.Firing = group.Firing != 0; row.KeyText = i < weaponKeys.Length ? weaponKeys[i] : "";
            }
            for (int i = 0; i < model.Abilities.Count; i++)
            {
                var row = model.Abilities[i]; var slot = i < value.Abilities.Length ? value.Abilities[i] : default;
                var definition = names.Ability(slot.Ability);
                row.HasAbility = ship && definition != null; row.Name = definition?.DisplayName ?? ""; row.Icon = definition?.Icon;
                row.CooldownFraction = Fraction(slot.Cooldown01, 100); row.Charges = slot.Charges; row.Ready = slot.Ready != 0; row.Active = slot.Active != 0;
                row.ChannelFraction = Fraction(slot.Channel01, 200); row.Blocked = slot.Blocked != 0;
                row.Note = slot.Blocked == Game.Simulation.Abilities.AbilityBlockReason.NoHullPlating ? "NO HULL" : "";
                row.KeyText = i < abilityKeys.Length ? abilityKeys[i] : "";
                if (i < tooltipFor.Length && (!ReferenceEquals(tooltipFor[i], definition) || tooltipBlocked[i] != slot.Blocked))
                {   // rebuilt only when the ability or its reason changes
                    tooltipFor[i] = definition; tooltipBlocked[i] = slot.Blocked; row.Tooltip = Tooltip(definition, slot.Blocked);
                }
            }
            model.RadarRangeM = displayRange;
            bool changed = !radarInitialized || lastSourceRadar != value.RadarVersion;
            int count = ship ? math.min(contacts.Length, radar.Length) : 0;
            int waypointIndex = -1;
            HudRadarBlipView waypoint = default;
            if (ship && hasPosition && names.TryGetNextHop(out var hop))
            {
                float2 offset = (float2)(hop.GatePositionM - playerPosition).xz;
                float range = model.RadarRangeM > 0 ? model.RadarRangeM : 2000;
                byte flags = 4;
                if (math.lengthsq(offset) > range * range) flags |= 2;
                waypointIndex = math.min(count, radar.Length - 1);
                for (int i = 0; i < count; i++) if (math.distancesq(contacts[i].OffsetXZ, offset) < .01f) { waypointIndex = i; break; }
                waypoint = new HudRadarBlipView { OffsetXZ = new UnityEngine.Vector2(offset.x, offset.y), Kind = 3, Flags = flags };
                count = math.max(count, waypointIndex + 1);
            }
            // Compare only the final contact value: a stationary waypoint must not toggle twice per frame.
            for (int i = 0; i < count; i++)
            {
                var next = waypoint;
                if (i != waypointIndex)
                {
                    var contact = contacts[i];
                    next = new HudRadarBlipView { OffsetXZ = new UnityEngine.Vector2(contact.OffsetXZ.x, contact.OffsetXZ.y), Kind = contact.Kind, Relation = contact.Relation, Flags = contact.Flags };
                }
                changed |= !Same(radar[i], next); radar[i] = next;
            }
            if (ship && hasPosition)
            {
                float range = model.RadarRangeM > 0 ? model.RadarRangeM : 2000;
                for (int m = 0; m < markers.Count && count < radar.Length; m++)
                {
                    float2 offset = (float2)(markers[m] - playerPosition).xz; byte flags = 0;
                    if (math.lengthsq(offset) > range * range) flags = 2;
                    var next = new HudRadarBlipView { OffsetXZ = new UnityEngine.Vector2(offset.x, offset.y), Kind = 3, Flags = flags, Label = markerLabels[m] };
                    changed |= !Same(radar[count], next); radar[count++] = next;
                }
            }
            changed |= radarCount != count; radarCount = count; lastSourceRadar = value.RadarVersion; radarInitialized = true;
            if (changed) model.RadarVersion = ++radarVersion;
            if (math.isfinite(yaw) && math.abs(UnityEngine.Mathf.DeltaAngle(model.RadarYawDeg, yaw)) >= .5f) model.RadarYawDeg = yaw;
        }
        // Co-op pilot names: cached so the HUD does not allocate a string every frame.
        FixedString32Bytes pilotKey; string pilotText = "";
        string PilotText(in FixedString32Bytes name)
        {
            if (name != pilotKey) { pilotKey = name; pilotText = name.ToString(); }
            return pilotText;
        }
        static bool Same(HudRadarBlipView a, HudRadarBlipView b) => a.OffsetXZ == b.OffsetXZ && a.Kind == b.Kind && a.Relation == b.Relation && a.Flags == b.Flags && ReferenceEquals(a.Label, b.Label);
        static int Rounded(float value) => math.isfinite(value) ? (int)math.clamp(math.round(value), int.MinValue, int.MaxValue) : 0;
        /// <summary>Name, description, cost and the blocked reason, for the ability slot's hover tooltip.</summary>
        static string Tooltip(Game.Data.Definitions.AbilityDefinition definition, byte blocked)
        {
            if (definition == null) return "";
            var text = new System.Text.StringBuilder(definition.DisplayName);
            if (!string.IsNullOrEmpty(definition.Description)) text.Append('\n').Append(definition.Description);
            text.Append('\n');
            if (definition.EnergyCost > 0) text.Append(definition.EnergyCost.ToString("0")).Append(" energy");
            if (definition.CooldownSeconds >= 1) text.Append(definition.EnergyCost > 0 ? " · " : "").Append(definition.CooldownSeconds.ToString("0")).Append(" s cooldown");
            if (blocked != 0) text.Append('\n').Append(Game.Simulation.Abilities.AbilityBlockReason.Text(blocked));
            return text.ToString();
        }
        static float Fraction(float value, int steps) => math.isfinite(value) ? math.round(math.saturate(value) * steps) / steps : 0;
        protected override void OnDestroy() => Unbind();
    }
}
