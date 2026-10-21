using System;
using Game.Core;
using Game.UI.Controls;
using Game.UI.Data;
using Game.UI.ViewModels;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Views
{
    /// <summary>The flight HUD after the redesign (2026-10-21, approved mockup): no plates in the corners. The reticle, the lead pip and
    /// the arcs at the player's ship are drawn by <see cref="HudMarks"/>; the target bracket, interaction tag and edge markers live on
    /// the world layer (WorldOverlaySystem). This view adds the numbers beside the arcs, the small ability strip, the location title
    /// card and the one-line objective toast. Fixed pools and direct updates (SPEC-61 §4.4/5.3).</summary>
    public sealed class FlightHudView : UIScreen
    {
        const int AbilityCount = 4;
        const long TitleMs = 4000, ObjectiveMs = 6000;
        readonly HudViewModel viewModel;
        readonly Func<string, string> bindingDisplay;
        readonly Slot[] abilities = new Slot[AbilityCount];
        readonly HudWeaponGroupViewModel[] weaponModels = new HudWeaponGroupViewModel[3];
        readonly HudAbilityViewModel[] abilityModels = new HudAbilityViewModel[AbilityCount];
        readonly EventHandler<BindablePropertyChangedEventArgs>[] abilityChanged = new EventHandler<BindablePropertyChangedEventArgs>[AbilityCount];
        readonly EventHandler<BindablePropertyChangedEventArgs> weaponChanged;
        readonly string[] abilityKeys = new string[AbilityCount];
        HudMarks marks;
        Label hullValue, shieldValue, energyValue, speed, title, titleDetail;
        VisualElement abilityRegion, titleRegion, objectiveRegion, hullReadout, shieldReadout, energyReadout, speedReadout;
        IVisualElementScheduledItem titleHide, objectiveHide;
        string shownSystem, shownZone, shownObjective;
        bool observing;
        public FlightHudView(HudViewModel vm, Func<string, string> bindingDisplay)
        {
            viewModel = vm ?? throw new ArgumentNullException(nameof(vm)); this.bindingDisplay = bindingDisplay;
            weaponChanged = (_, _) => marks?.MarkDirtyRepaint();
        }
        public override string Key => "hud";
        public override UILayer Layer => UILayer.Hud;
        public override InputContext Context => InputContext.Flight;
        public override bool PausesSimulation => false;
        public override bool BlocksWorldPointer => false;
        public override bool HidesHud => false;
        public override bool IsRoot => true;
        /// <summary>The arcs and the reticle, for tests and for the screenshot tools.</summary>
        public HudMarks Marks => marks;
        protected override void OnBuild()
        {
            Root.dataSource = viewModel; Root.pickingMode = PickingMode.Ignore;
            var hud = Root.Q<VisualElement>(className: "flight-hud") ?? Root;
            marks = new HudMarks(viewModel); hud.Insert(0, marks);
            hullValue = Q<Label>("hud-hull-value"); shieldValue = Q<Label>("hud-shield-value"); energyValue = Q<Label>("hud-energy-value"); speed = Q<Label>("hud-speed");
            hullReadout = Q<VisualElement>("hud-readout-hull"); shieldReadout = Q<VisualElement>("hud-readout-shield");
            energyReadout = Q<VisualElement>("hud-readout-energy"); speedReadout = Q<VisualElement>("hud-readout-speed");
            title = Q<Label>("hud-title"); titleDetail = Q<Label>("hud-title-detail"); titleRegion = Q<VisualElement>("hud-region-title");
            objectiveRegion = Q<VisualElement>("hud-region-objective"); abilityRegion = Q<VisualElement>("hud-region-abilities");
            var abilityRow = Q<VisualElement>("hud-ability-row");
            for (int i = 0; i < AbilityCount; i++)
            {
                int index = i; abilities[i] = new Slot { name = "hud-ability-" + (i + 1), pickingMode = PickingMode.Ignore, focusable = false }; abilityRow.Add(abilities[i]);
                abilityKeys[i] = bindingDisplay?.Invoke("Flight/Ability" + (i + 1)) ?? ""; abilityChanged[i] = (_, _) => Ability(index);
            }
            titleHide = Root.schedule.Execute(() => titleRegion.RemoveFromClassList("hud-title--shown")); titleHide.Pause();
            objectiveHide = Root.schedule.Execute(() => objectiveRegion.RemoveFromClassList("hud-objective--shown")); objectiveHide.Pause();
            Root.RegisterCallback<GeometryChangedEvent>(evt => { if (ReferenceEquals(evt.target, Root)) Readouts(); });
            Root.RegisterCallback<DetachFromPanelEvent>(evt => { if (ReferenceEquals(evt.target, Root)) Stop(); });
            Root.RegisterCallback<AttachToPanelEvent>(evt => { if (ReferenceEquals(evt.target, Root) && IsOpen) Start(); });
            Start();
        }
        public override void OnPush(object args) => Start();
        public override void OnPop() => Stop();
        public override bool OnBack() { Navigator.Push<PauseView>(); return true; }
        void Start()
        {
            if (observing) return; observing = true; viewModel.propertyChanged += Changed; ObservePools();
            Visibility(); Readouts(); Title(); Objective(); marks.MarkDirtyRepaint();
            for (int i = 0; i < AbilityCount; i++) Ability(i);
        }
        void Stop()
        {
            if (!observing) return; observing = false; viewModel.propertyChanged -= Changed;
            for (int i = 0; i < weaponModels.Length; i++) { if (weaponModels[i] != null) weaponModels[i].propertyChanged -= weaponChanged; weaponModels[i] = null; }
            for (int i = 0; i < AbilityCount; i++) { if (abilityModels[i] != null) abilityModels[i].propertyChanged -= abilityChanged[i]; abilityModels[i] = null; }
        }
        void ObservePools()
        {
            for (int i = 0; i < weaponModels.Length; i++)
            {
                var next = i < viewModel.WeaponGroups.Count ? viewModel.WeaponGroups[i] : null;
                if (ReferenceEquals(next, weaponModels[i])) continue;
                if (weaponModels[i] != null) weaponModels[i].propertyChanged -= weaponChanged; weaponModels[i] = next;
                if (next != null) next.propertyChanged += weaponChanged;
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
            if (ReferenceEquals(property, nameof(HudViewModel.MarksVersion))) { marks.MarkDirtyRepaint(); Readouts(); }
            else if (ReferenceEquals(property, nameof(HudViewModel.HasShip))) { Visibility(); Readouts(); marks.MarkDirtyRepaint(); }
            else if (ReferenceEquals(property, nameof(HudViewModel.HullPercent)) || ReferenceEquals(property, nameof(HudViewModel.HullFraction)) || ReferenceEquals(property, nameof(HudViewModel.ShieldFraction))
                || ReferenceEquals(property, nameof(HudViewModel.EnergyFraction)) || ReferenceEquals(property, nameof(HudViewModel.SpeedMps)) || ReferenceEquals(property, nameof(HudViewModel.Boosting))
                || ReferenceEquals(property, nameof(HudViewModel.IsHulk))) { Readouts(); marks.MarkDirtyRepaint(); }
            else if (ReferenceEquals(property, nameof(HudViewModel.StarSystemName)) || ReferenceEquals(property, nameof(HudViewModel.ZoneName))) Title();
            else if (ReferenceEquals(property, nameof(HudViewModel.HasObjective)) || ReferenceEquals(property, nameof(HudViewModel.ObjectiveTitle)) || ReferenceEquals(property, nameof(HudViewModel.ObjectiveText))) Objective();
            else if (ReferenceEquals(property, nameof(HudViewModel.WeaponGroups))) { ObservePools(); marks.MarkDirtyRepaint(); }
            else if (ReferenceEquals(property, nameof(HudViewModel.Abilities))) { ObservePools(); for (int i = 0; i < AbilityCount; i++) Ability(i); }
        }
        static void Show(VisualElement element, bool visible) => element.EnableInClassList(UIStyleClasses.IsHidden, !visible);
        void Visibility() => Show(abilityRegion, viewModel.HasShip);
        /// <summary>D/E: a number appears beside its arc only below the threshold (hull and shield 30 %, energy 25 %); the speed only while boosting.</summary>
        void Readouts()
        {
            bool ship = viewModel.HasShip && viewModel.ShowShip && !viewModel.IsHulk;
            float height = Root.resolvedStyle.height, width = Root.resolvedStyle.width;
            if (!(height > 0) || !(width > 0)) { height = 1080; width = 1920; }
            float radius = HudMarks.ArcRadius(viewModel, height);
            float x = viewModel.ShipPoint.x * width, y = viewModel.ShipPoint.y * height;
            bool hull = ship && viewModel.HullArc > .5f && viewModel.HullFraction < .3f;
            bool shield = ship && viewModel.ShieldArc > .5f && viewModel.ShieldFraction < .3f;
            bool energy = ship && viewModel.EnergyArc > .5f && viewModel.EnergyFraction < .25f;
            bool boost = ship && viewModel.Boosting;
            Place(hullReadout, hull, x - radius - 8, y);
            Place(shieldReadout, shield, x + radius + 8, y);
            Place(energyReadout, energy, x, y + radius + 14);
            Place(speedReadout, boost, x, y + radius + (energy ? 30 : 14));
            if (hull) hullValue.text = NumberStringCache.Get(Mathf.RoundToInt(viewModel.HullFraction * 100));
            if (shield) shieldValue.text = NumberStringCache.Get(Mathf.RoundToInt(viewModel.ShieldFraction * 100));
            if (energy) energyValue.text = NumberStringCache.Get(Mathf.RoundToInt(viewModel.EnergyFraction * 100));
            speed.text = NumberStringCache.Get(viewModel.SpeedMps);
        }
        static void Place(VisualElement element, bool visible, float x, float y)
        {
            Show(element, visible);
            if (!visible) return;
            element.style.left = x; element.style.top = y;
        }
        /// <summary>G: the system or zone name, 4 s on arrival, then gone (the map and pause screen still name it).</summary>
        void Title()
        {
            string system = viewModel.StarSystemName ?? "", zone = viewModel.ZoneName ?? "";
            if (system == (shownSystem ?? "") && zone == (shownZone ?? "")) return;
            bool zoneChanged = zone != (shownZone ?? "");
            shownSystem = system; shownZone = zone;
            string name = zoneChanged && zone.Length > 0 ? zone : system, detail = zoneChanged && zone.Length > 0 ? system : zone;
            if (name.Length == 0) { titleRegion.RemoveFromClassList("hud-title--shown"); return; }
            title.text = name.ToUpperInvariant(); titleDetail.text = detail.ToUpperInvariant(); Show(titleDetail, detail.Length > 0);
            titleRegion.AddToClassList("hud-title--shown"); titleHide.ExecuteLater(TitleMs);
        }
        /// <summary>H: one line for 6 s when the objective changes.</summary>
        void Objective()
        {
            string key = viewModel.HasObjective ? (viewModel.ObjectiveTitle ?? "") + "\n" + (viewModel.ObjectiveText ?? "") : "";
            if (key == (shownObjective ?? "")) return;
            shownObjective = key;
            if (key.Length == 0) { objectiveRegion.RemoveFromClassList("hud-objective--shown"); return; }
            objectiveRegion.AddToClassList("hud-objective--shown"); objectiveHide.ExecuteLater(ObjectiveMs);
        }
        void Ability(int index)
        {
            var model = abilityModels[index]; var slot = abilities[index]; Show(slot, model?.HasAbility == true); if (model == null) return;
            slot.SetIcon(model.Icon); slot.SetCount(model.Charges); slot.SetKeyHint(model.KeyText ?? abilityKeys[index]); slot.SetCooldown(model.CooldownFraction);
            slot.SetChannel(model.ChannelFraction); slot.SetNote(model.Note); slot.tooltip = model.Tooltip ?? "";
            slot.SetState(model.Active, false, false); slot.EnableInClassList(UIStyleClasses.IsReady, model.Ready);
            slot.EnableInClassList(UIStyleClasses.IsDisabled, model.Blocked && !model.Active);   // dimmed, still hoverable for its reason
        }
    }
}
