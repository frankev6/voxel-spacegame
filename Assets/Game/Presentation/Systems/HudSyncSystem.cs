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
        public ReadOnlySpan<HudRadarBlipView> Blips => radar.AsSpan(0, radarCount);
        /// <summary>Extra point-of-interest markers in star-system coordinates, drawn as POI blips after the
        /// simulation contacts and clamped to the rim when beyond range (QA-001: the dev playground's beacons use the
        /// real radar instead of a second map). Replaces the previous set; an empty span clears it.</summary>
        /// <summary>Displayed radar range; contacts are gathered within 2000 m and those beyond this range sit on the rim.</summary>
        public float DisplayRangeM { get => displayRange; set => displayRange = math.isfinite(value) ? math.clamp(value, 100, 2000) : 2000; }
        float displayRange = 2000;
        public void SetMarkers(ReadOnlySpan<double3> positions)
        { markers.Clear(); for (int i = 0; i < positions.Length; i++) markers.Add(positions[i]); }
        protected override void OnCreate()
        {
            snapshots = GetEntityQuery(ComponentType.ReadOnly<HudSnapshot>(), ComponentType.ReadOnly<HudRadarBlip>());
            players = GetEntityQuery(ComponentType.ReadOnly<PlayerShip>(), ComponentType.ReadOnly<LocalTransform>());
            origins = GetEntityQuery(ComponentType.ReadOnly<FrameOrigin>());
            cameras = GetEntityQuery(ComponentType.ReadOnly<MainCameraState>());
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
            model.HasTarget = ship && (value.Flags & HudFlags.HasTarget) != 0;
            model.TargetName = model.HasTarget ? names.TargetName(value.TargetNameHash, value.TargetFaction) : "";
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
                row.KeyText = i < abilityKeys.Length ? abilityKeys[i] : "";
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
                if (math.lengthsq(offset) > range * range) { offset = math.normalizesafe(offset) * range; flags |= 2; }
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
                    if (math.lengthsq(offset) > range * range) { offset = math.normalizesafe(offset) * range; flags = 2; }
                    var next = new HudRadarBlipView { OffsetXZ = new UnityEngine.Vector2(offset.x, offset.y), Kind = 3, Flags = flags };
                    changed |= !Same(radar[count], next); radar[count++] = next;
                }
            }
            changed |= radarCount != count; radarCount = count; lastSourceRadar = value.RadarVersion; radarInitialized = true;
            if (changed) model.RadarVersion = ++radarVersion;
            if (math.isfinite(yaw) && math.abs(UnityEngine.Mathf.DeltaAngle(model.RadarYawDeg, yaw)) >= .5f) model.RadarYawDeg = yaw;
        }
        static bool Same(HudRadarBlipView a, HudRadarBlipView b) => a.OffsetXZ == b.OffsetXZ && a.Kind == b.Kind && a.Relation == b.Relation && a.Flags == b.Flags;
        static int Rounded(float value) => math.isfinite(value) ? (int)math.clamp(math.round(value), int.MinValue, int.MaxValue) : 0;
        static float Fraction(float value, int steps) => math.isfinite(value) ? math.round(math.saturate(value) * steps) / steps : 0;
        protected override void OnDestroy() => Unbind();
    }
}
