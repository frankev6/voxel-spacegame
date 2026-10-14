using System;
using Game.Core;
using Game.UI.Controls;
using Game.UI.Data;
using Game.UI.ViewModels;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Views
{
    /// <summary>SPEC-61 §4.4/5.3: fixed HUD pools and direct numerical updates; gameplay is supplied by HUD-009.</summary>
    public sealed class FlightHudView : UIScreen
    {
        const int WeaponCount = 5, AbilityCount = 4, BlipCount = 64;
        static readonly ProfilerMarker RadarMarker = new(ProfilerCategory.Scripts, "FlightHudView.Radar");
        readonly HudViewModel viewModel;
        readonly IHudRadarSource radar;
        readonly Func<string, string> bindingDisplay;
        readonly Slot[] weapons = new Slot[WeaponCount], abilities = new Slot[AbilityCount];
        readonly Label[] weaponNumbers = new Label[WeaponCount];
        readonly VisualElement[] blips = new VisualElement[BlipCount];
        readonly HudWeaponGroupViewModel[] weaponModels = new HudWeaponGroupViewModel[WeaponCount];
        readonly HudAbilityViewModel[] abilityModels = new HudAbilityViewModel[AbilityCount];
        readonly EventHandler<BindablePropertyChangedEventArgs>[] weaponChanged = new EventHandler<BindablePropertyChangedEventArgs>[WeaponCount], abilityChanged = new EventHandler<BindablePropertyChangedEventArgs>[AbilityCount];
        readonly string[] weaponKeys = new string[WeaponCount], abilityKeys = new string[AbilityCount];
        Meter hull, shield, energy, targetHull, targetShield;
        Label speed, shieldStatus, targetDistance, radarRange, interactionDetail;
        VisualElement objectiveRegion, statusRegion, weaponRegion, radarRegion, targetRegion, promptRegion, targetRelation, objectiveTitle, objectiveText, route;
        KeyHint interactionKey;
        bool observing;
        public FlightHudView(HudViewModel vm, IHudRadarSource radar, Func<string, string> bindingDisplay)
        { viewModel = vm ?? throw new ArgumentNullException(nameof(vm)); this.radar = radar; this.bindingDisplay = bindingDisplay; }
        public override string Key => "hud";
        public override UILayer Layer => UILayer.Hud;
        public override InputContext Context => InputContext.Flight;
        public override bool PausesSimulation => false;
        public override bool BlocksWorldPointer => false;
        public override bool HidesHud => false;
        public override bool IsRoot => true;
        protected override void OnBuild()
        {
            Root.dataSource = viewModel; Root.pickingMode = PickingMode.Ignore;
            hull = Q<Meter>("hud-hull-meter"); shield = Q<Meter>("hud-shield-meter"); energy = Q<Meter>("hud-energy-meter");
            targetHull = Q<Meter>("hud-target-hull"); targetShield = Q<Meter>("hud-target-shield");
            speed = Q<Label>("hud-speed"); shieldStatus = Q<Label>("hud-shield-status"); targetDistance = Q<Label>("hud-target-distance"); radarRange = Q<Label>("hud-radar-range");
            objectiveRegion = Q<VisualElement>("hud-region-objective"); objectiveTitle = Q<Label>("hud-objective-title"); objectiveText = Q<Label>("hud-objective-text"); route = Q<Label>("hud-route");
            statusRegion = Q<VisualElement>("hud-region-status"); weaponRegion = Q<VisualElement>("hud-region-weapons"); radarRegion = Q<VisualElement>("hud-region-radar");
            targetRegion = Q<VisualElement>("hud-region-target"); promptRegion = Q<VisualElement>("hud-region-prompt"); targetRelation = Q<VisualElement>("hud-target-relation"); interactionKey = Q<KeyHint>("hud-interaction-key");
            interactionDetail = Q<Label>("hud-interaction-detail");
            var weaponRow = Q<VisualElement>("hud-weapon-row"); var abilityRow = Q<VisualElement>("hud-ability-row"); var radarCanvas = Q<VisualElement>("hud-radar");
            for (int i = 0; i < WeaponCount; i++)
            {
                int index = i; weapons[i] = MakeSlot("hud-weapon-group-" + (i + 1)); weaponRow.Add(weapons[i]);
                var number = weaponNumbers[i] = new Label { name = weapons[i].name + "-number", pickingMode = PickingMode.Ignore }; number.AddToClassList("hud-slot-number"); weapons[i].Add(number);
                string path = i == 0 ? "Flight/FirePrimary" : i == 1 ? "Flight/FireSecondary" : i == 2 ? "Flight/FireTertiary" : "";
                weaponKeys[i] = path.Length > 0 ? bindingDisplay?.Invoke(path) ?? "" : "";
                weaponChanged[i] = (_, _) => Weapon(index);
            }
            for (int i = 0; i < AbilityCount; i++)
            {
                int index = i; abilities[i] = MakeSlot("hud-ability-" + (i + 1)); abilityRow.Add(abilities[i]);
                abilityKeys[i] = bindingDisplay?.Invoke("Flight/Ability" + (i + 1)) ?? ""; abilityChanged[i] = (_, _) => Ability(index);
            }
            for (int i = 0; i < BlipCount; i++)
            {
                var blip = blips[i] = new VisualElement { name = "hud-radar-blip-" + i, pickingMode = PickingMode.Ignore };
                blip.AddToClassList("marker"); blip.AddToClassList("hud-blip"); blip.AddToClassList(UIStyleClasses.IsHidden); radarCanvas.Add(blip);
            }
            interactionKey.Refresh(bindingDisplay ?? (_ => ""));
            Root.RegisterCallback<GeometryChangedEvent>(Geometry);
            Root.RegisterCallback<DetachFromPanelEvent>(evt => { if (ReferenceEquals(evt.target, Root)) Stop(); });
            Root.RegisterCallback<AttachToPanelEvent>(evt => { if (ReferenceEquals(evt.target, Root) && IsOpen) Start(); });
            Start();
        }
        static Slot MakeSlot(string name) => new() { name = name, pickingMode = PickingMode.Ignore, focusable = false };
        public override void OnPush(object args) => Start();
        public override void OnPop() => Stop();
        public override bool OnBack() { Navigator.Push<PauseView>(); return true; }
        void Geometry(GeometryChangedEvent evt)
        { if (ReferenceEquals(evt.target, Root)) Root.EnableInClassList("hud-narrow", evt.newRect.width < 1700); }
        void Start()
        {
            if (observing) return; observing = true; viewModel.propertyChanged += Changed; ObservePools();
            Visibility(); PlayerMeters(); speed.text = NumberStringCache.Get(viewModel.SpeedMps); ShieldStatus(); TargetMeters(); targetDistance.text = NumberStringCache.Get(viewModel.TargetDistanceM);
            Relation(); Prompt(); route.EnableInClassList("is-warning", viewModel.RouteBlocked);
            speed.EnableInClassList("hud-speed--boosting", viewModel.Boosting);
            radarRange.text = NumberStringCache.Get(Mathf.RoundToInt(viewModel.RadarRangeM)); Radar();
            for (int i = 0; i < WeaponCount; i++) Weapon(i); for (int i = 0; i < AbilityCount; i++) Ability(i);
        }
        void Stop()
        {
            if (!observing) return; observing = false; viewModel.propertyChanged -= Changed;
            for (int i = 0; i < WeaponCount; i++) { if (weaponModels[i] != null) weaponModels[i].propertyChanged -= weaponChanged[i]; weaponModels[i] = null; }
            for (int i = 0; i < AbilityCount; i++) { if (abilityModels[i] != null) abilityModels[i].propertyChanged -= abilityChanged[i]; abilityModels[i] = null; }
        }
        void ObservePools()
        {
            for (int i = 0; i < WeaponCount; i++)
            {
                var next = i < viewModel.WeaponGroups.Count ? viewModel.WeaponGroups[i] : null;
                if (ReferenceEquals(next, weaponModels[i])) continue;
                if (weaponModels[i] != null) weaponModels[i].propertyChanged -= weaponChanged[i]; weaponModels[i] = next;
                if (next != null) next.propertyChanged += weaponChanged[i];
            }
            for (int i = 0; i < AbilityCount; i++)
            {
                var next = i < viewModel.Abilities.Count ? viewModel.Abilities[i] : null;
                if (ReferenceEquals(next, abilityModels[i])) continue;
                if (abilityModels[i] != null) abilityModels[i].propertyChanged -= abilityChanged[i]; abilityModels[i] = next;
                if (next != null) next.propertyChanged += abilityChanged[i];
            }
        }
        void Changed(object sender, BindablePropertyChangedEventArgs args)
        {
            string property = args.propertyName;
            if (ReferenceEquals(property, nameof(HudViewModel.HasShip)) || ReferenceEquals(property, nameof(HudViewModel.HasTarget)) || ReferenceEquals(property, nameof(HudViewModel.HasInteraction)) || ReferenceEquals(property, nameof(HudViewModel.HasObjective)) || ReferenceEquals(property, nameof(HudViewModel.HasRoute))) { Visibility(); }
            else if (ReferenceEquals(property, nameof(HudViewModel.HullPercent)) || ReferenceEquals(property, nameof(HudViewModel.HullFraction)) || ReferenceEquals(property, nameof(HudViewModel.ShieldValue)) || ReferenceEquals(property, nameof(HudViewModel.ShieldMax)) || ReferenceEquals(property, nameof(HudViewModel.ShieldFraction)) || ReferenceEquals(property, nameof(HudViewModel.EnergyValue)) || ReferenceEquals(property, nameof(HudViewModel.EnergyMax)) || ReferenceEquals(property, nameof(HudViewModel.EnergyFraction)) || ReferenceEquals(property, nameof(HudViewModel.IsHulk))) { PlayerMeters(); }
            else if (ReferenceEquals(property, nameof(HudViewModel.SpeedMps))) { speed.text = NumberStringCache.Get(viewModel.SpeedMps); }
            else if (ReferenceEquals(property, nameof(HudViewModel.Boosting))) { speed.EnableInClassList("hud-speed--boosting", viewModel.Boosting); }
            else if (ReferenceEquals(property, nameof(HudViewModel.ShieldStatus))) { ShieldStatus(); }
            else if (ReferenceEquals(property, nameof(HudViewModel.TargetHullFraction)) || ReferenceEquals(property, nameof(HudViewModel.TargetShieldFraction))) { TargetMeters(); }
            else if (ReferenceEquals(property, nameof(HudViewModel.TargetDistanceM))) { targetDistance.text = NumberStringCache.Get(viewModel.TargetDistanceM); }
            else if (ReferenceEquals(property, nameof(HudViewModel.TargetRelation))) { Relation(); }
            else if (ReferenceEquals(property, nameof(HudViewModel.InteractionInRange)) || ReferenceEquals(property, nameof(HudViewModel.InteractionBlocked))) { Prompt(); }
            else if (ReferenceEquals(property, nameof(HudViewModel.RouteBlocked))) { route.EnableInClassList("is-warning", viewModel.RouteBlocked); }
            else if (ReferenceEquals(property, nameof(HudViewModel.WeaponGroups))) { ObservePools(); for (int i = 0; i < WeaponCount; i++) Weapon(i); }
            else if (ReferenceEquals(property, nameof(HudViewModel.Abilities))) { ObservePools(); for (int i = 0; i < AbilityCount; i++) Ability(i); }
            else if (ReferenceEquals(property, nameof(HudViewModel.RadarRangeM))) { radarRange.text = NumberStringCache.Get(Mathf.RoundToInt(viewModel.RadarRangeM)); Radar(); }
            else if (ReferenceEquals(property, nameof(HudViewModel.RadarYawDeg)) || ReferenceEquals(property, nameof(HudViewModel.RadarVersion))) { Radar(); }
        }
        static void Show(VisualElement element, bool visible) => element.EnableInClassList(UIStyleClasses.IsHidden, !visible);
        void Visibility()
        {
            Show(objectiveRegion, viewModel.HasObjective || viewModel.HasRoute); Show(objectiveTitle, viewModel.HasObjective); Show(objectiveText, viewModel.HasObjective); Show(route, viewModel.HasRoute);
            Show(statusRegion, viewModel.HasShip); Show(weaponRegion, viewModel.HasShip); Show(radarRegion, viewModel.HasShip);
            Show(targetRegion, viewModel.HasShip && viewModel.HasTarget); Show(promptRegion, viewModel.HasShip && viewModel.HasInteraction);
        }
        void PlayerMeters()
        {
            if (viewModel.IsHulk) return;
            hull.SetValue(viewModel.HullPercent, 100); hull.SetFraction(viewModel.HullFraction);
            shield.SetValue(viewModel.ShieldValue, viewModel.ShieldMax); shield.SetFraction(viewModel.ShieldFraction);
            energy.SetValue(viewModel.EnergyValue, viewModel.EnergyMax); energy.SetFraction(viewModel.EnergyFraction);
        }
        void ShieldStatus() => shieldStatus.text = viewModel.ShieldStatus switch { 1 => "Online", 2 => "Recharging", 3 => "Offline", _ => "No shields" };
        void TargetMeters() { targetHull.SetFraction(viewModel.TargetHullFraction); targetShield.SetFraction(viewModel.TargetShieldFraction); }
        void Relation()
        {
            targetRelation.EnableInClassList("marker--ally", viewModel.TargetRelation == 1); targetRelation.EnableInClassList("marker--enemy", viewModel.TargetRelation == 2);
            targetRelation.EnableInClassList("marker--neutral", viewModel.TargetRelation != 1 && viewModel.TargetRelation != 2);
        }
        void Prompt()
        {
            promptRegion.EnableInClassList("hud-prompt--out-of-range", !viewModel.InteractionInRange);
            interactionDetail.EnableInClassList("is-warning", viewModel.InteractionBlocked);
        }
        void Weapon(int index)
        {
            var model = weaponModels[index]; var slot = weapons[index]; Show(slot, model != null); if (model == null) return;
            weaponNumbers[index].text = NumberStringCache.Get(model.GroupNumber > 0 ? model.GroupNumber : index + 1);
            slot.SetCount(model.WeaponCount); slot.SetKeyHint(model.KeyText ?? weaponKeys[index]); slot.SetCooldown(1 - model.ReadyFraction);
            slot.SetState(model.Firing, false, false); slot.EnableInClassList(UIStyleClasses.IsEmpty, model.WeaponCount == 0); slot.EnableInClassList(UIStyleClasses.IsReady, model.WeaponCount > 0 && model.ReadyFraction >= 1);
        }
        void Ability(int index)
        {
            var model = abilityModels[index]; var slot = abilities[index]; Show(slot, model?.HasAbility == true); if (model == null) return;
            slot.SetIcon(model.Icon); slot.SetCount(model.Charges); slot.SetKeyHint(model.KeyText ?? abilityKeys[index]); slot.SetCooldown(model.CooldownFraction);
            slot.SetState(model.Active, false, false); slot.EnableInClassList(UIStyleClasses.IsReady, model.Ready);
        }
        void Radar()
        {
            using var scope = RadarMarker.Auto();
            ReadOnlySpan<HudRadarBlipView> contacts = radar != null ? radar.Blips : ReadOnlySpan<HudRadarBlipView>.Empty;
            float range = viewModel.RadarRangeM; if (!float.IsFinite(range) || range <= 0) range = 2000;
            float yaw = viewModel.RadarYawDeg; if (!float.IsFinite(yaw)) yaw = 0;
            float radians = yaw * Mathf.Deg2Rad, cos = Mathf.Cos(radians), sin = Mathf.Sin(radians);
            for (int i = 0; i < BlipCount; i++)
            {
                var blip = blips[i]; Show(blip, i < contacts.Length); if (i >= contacts.Length) continue;
                var contact = contacts[i]; var offset = contact.OffsetXZ; if (!float.IsFinite(offset.x) || !float.IsFinite(offset.y)) offset = Vector2.zero;
                bool beyond = (contact.Flags & 2) != 0 || offset.sqrMagnitude > range * range;
                if (offset.sqrMagnitude > range * range) offset = offset.normalized * range;
                float x = (offset.x * cos + offset.y * sin) * 120 / range, y = (-offset.x * sin + offset.y * cos) * 120 / range;
                blip.style.translate = new Translate(x, -y, 0);
                blip.EnableInClassList("marker--ally", contact.Relation == 1); blip.EnableInClassList("marker--enemy", contact.Relation == 2); blip.EnableInClassList("marker--neutral", contact.Relation != 1 && contact.Relation != 2);
                blip.EnableInClassList("hud-blip--ship", contact.Kind == 0); blip.EnableInClassList("hud-blip--station", contact.Kind == 1); blip.EnableInClassList("hud-blip--body", contact.Kind == 2); blip.EnableInClassList("hud-blip--poi", contact.Kind == 3);
                blip.EnableInClassList("hud-blip--beyond", beyond); blip.EnableInClassList("hud-blip--waypoint", (contact.Flags & 4) != 0);
            }
        }
    }
}
