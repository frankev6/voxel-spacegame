using System.Collections.Generic;
using Unity.Properties;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>Bindable screen data; cached notifications and app-lifetime identity (SPEC-61 §4.2).</summary>
    public sealed class HudViewModel : ViewModelBase
    {
        static readonly BindablePropertyChangedEventArgs HasShipChanged = new BindablePropertyChangedEventArgs(nameof(HasShip));
        bool hasShip;
        [CreateProperty] public bool HasShip { get => hasShip; set => Set(ref hasShip, value, HasShipChanged); }

        static readonly BindablePropertyChangedEventArgs IsHulkChanged = new BindablePropertyChangedEventArgs(nameof(IsHulk));
        bool isHulk;
        [CreateProperty] public bool IsHulk { get => isHulk; set => Set(ref isHulk, value, IsHulkChanged); }

        static readonly BindablePropertyChangedEventArgs HullPercentChanged = new BindablePropertyChangedEventArgs(nameof(HullPercent));
        int hullPercent;
        [CreateProperty] public int HullPercent { get => hullPercent; set => Set(ref hullPercent, value, HullPercentChanged); }

        static readonly BindablePropertyChangedEventArgs HullFractionChanged = new BindablePropertyChangedEventArgs(nameof(HullFraction));
        float hullFraction;
        [CreateProperty] public float HullFraction { get => hullFraction; set => Set(ref hullFraction, value, HullFractionChanged); }

        static readonly BindablePropertyChangedEventArgs ShieldValueChanged = new BindablePropertyChangedEventArgs(nameof(ShieldValue));
        int shieldValue;
        [CreateProperty] public int ShieldValue { get => shieldValue; set => Set(ref shieldValue, value, ShieldValueChanged); }

        static readonly BindablePropertyChangedEventArgs ShieldMaxChanged = new BindablePropertyChangedEventArgs(nameof(ShieldMax));
        int shieldMax;
        [CreateProperty] public int ShieldMax { get => shieldMax; set => Set(ref shieldMax, value, ShieldMaxChanged); }

        static readonly BindablePropertyChangedEventArgs ShieldFractionChanged = new BindablePropertyChangedEventArgs(nameof(ShieldFraction));
        float shieldFraction;
        [CreateProperty] public float ShieldFraction { get => shieldFraction; set => Set(ref shieldFraction, value, ShieldFractionChanged); }

        static readonly BindablePropertyChangedEventArgs ShieldStatusChanged = new BindablePropertyChangedEventArgs(nameof(ShieldStatus));
        int shieldStatus;
        [CreateProperty] public int ShieldStatus { get => shieldStatus; set => Set(ref shieldStatus, value, ShieldStatusChanged); }

        static readonly BindablePropertyChangedEventArgs EnergyValueChanged = new BindablePropertyChangedEventArgs(nameof(EnergyValue));
        int energyValue;
        [CreateProperty] public int EnergyValue { get => energyValue; set => Set(ref energyValue, value, EnergyValueChanged); }

        static readonly BindablePropertyChangedEventArgs EnergyMaxChanged = new BindablePropertyChangedEventArgs(nameof(EnergyMax));
        int energyMax;
        [CreateProperty] public int EnergyMax { get => energyMax; set => Set(ref energyMax, value, EnergyMaxChanged); }

        static readonly BindablePropertyChangedEventArgs EnergyFractionChanged = new BindablePropertyChangedEventArgs(nameof(EnergyFraction));
        float energyFraction;
        [CreateProperty] public float EnergyFraction { get => energyFraction; set => Set(ref energyFraction, value, EnergyFractionChanged); }

        static readonly BindablePropertyChangedEventArgs SpeedMpsChanged = new BindablePropertyChangedEventArgs(nameof(SpeedMps));
        int speedMps;
        [CreateProperty] public int SpeedMps { get => speedMps; set => Set(ref speedMps, value, SpeedMpsChanged); }

        static readonly BindablePropertyChangedEventArgs BoostingChanged = new BindablePropertyChangedEventArgs(nameof(Boosting));
        bool boosting;
        [CreateProperty] public bool Boosting { get => boosting; set => Set(ref boosting, value, BoostingChanged); }

        static readonly BindablePropertyChangedEventArgs HasTargetChanged = new BindablePropertyChangedEventArgs(nameof(HasTarget));
        bool hasTarget;
        [CreateProperty] public bool HasTarget { get => hasTarget; set => Set(ref hasTarget, value, HasTargetChanged); }

        static readonly BindablePropertyChangedEventArgs TargetNameChanged = new BindablePropertyChangedEventArgs(nameof(TargetName));
        string targetName;
        [CreateProperty] public string TargetName { get => targetName; set => Set(ref targetName, value, TargetNameChanged); }

        static readonly BindablePropertyChangedEventArgs TargetRelationChanged = new BindablePropertyChangedEventArgs(nameof(TargetRelation));
        int targetRelation;
        [CreateProperty] public int TargetRelation { get => targetRelation; set => Set(ref targetRelation, value, TargetRelationChanged); }

        static readonly BindablePropertyChangedEventArgs TargetHullFractionChanged = new BindablePropertyChangedEventArgs(nameof(TargetHullFraction));
        float targetHullFraction;
        [CreateProperty] public float TargetHullFraction { get => targetHullFraction; set => Set(ref targetHullFraction, value, TargetHullFractionChanged); }

        static readonly BindablePropertyChangedEventArgs TargetShieldFractionChanged = new BindablePropertyChangedEventArgs(nameof(TargetShieldFraction));
        float targetShieldFraction;
        [CreateProperty] public float TargetShieldFraction { get => targetShieldFraction; set => Set(ref targetShieldFraction, value, TargetShieldFractionChanged); }

        static readonly BindablePropertyChangedEventArgs TargetDistanceMChanged = new BindablePropertyChangedEventArgs(nameof(TargetDistanceM));
        int targetDistanceM;
        [CreateProperty] public int TargetDistanceM { get => targetDistanceM; set => Set(ref targetDistanceM, value, TargetDistanceMChanged); }

        static readonly BindablePropertyChangedEventArgs HasInteractionChanged = new BindablePropertyChangedEventArgs(nameof(HasInteraction));
        bool hasInteraction;
        [CreateProperty] public bool HasInteraction { get => hasInteraction; set => Set(ref hasInteraction, value, HasInteractionChanged); }

        static readonly BindablePropertyChangedEventArgs InteractionVerbTextChanged = new BindablePropertyChangedEventArgs(nameof(InteractionVerbText));
        string interactionVerbText;
        [CreateProperty] public string InteractionVerbText { get => interactionVerbText; set => Set(ref interactionVerbText, value, InteractionVerbTextChanged); }

        static readonly BindablePropertyChangedEventArgs InteractionTargetNameChanged = new BindablePropertyChangedEventArgs(nameof(InteractionTargetName));
        string interactionTargetName;
        [CreateProperty] public string InteractionTargetName { get => interactionTargetName; set => Set(ref interactionTargetName, value, InteractionTargetNameChanged); }

        static readonly BindablePropertyChangedEventArgs InteractionKeyTextChanged = new BindablePropertyChangedEventArgs(nameof(InteractionKeyText));
        string interactionKeyText;
        [CreateProperty] public string InteractionKeyText { get => interactionKeyText; set => Set(ref interactionKeyText, value, InteractionKeyTextChanged); }

        static readonly BindablePropertyChangedEventArgs InteractionInRangeChanged = new BindablePropertyChangedEventArgs(nameof(InteractionInRange));
        bool interactionInRange;
        [CreateProperty] public bool InteractionInRange { get => interactionInRange; set => Set(ref interactionInRange, value, InteractionInRangeChanged); }

        static readonly BindablePropertyChangedEventArgs InteractionDetailTextChanged = new BindablePropertyChangedEventArgs(nameof(InteractionDetailText));
        string interactionDetailText;
        [CreateProperty] public string InteractionDetailText { get => interactionDetailText; set => Set(ref interactionDetailText, value, InteractionDetailTextChanged); }

        static readonly BindablePropertyChangedEventArgs InteractionBlockedChanged = new BindablePropertyChangedEventArgs(nameof(InteractionBlocked));
        bool interactionBlocked;
        [CreateProperty] public bool InteractionBlocked { get => interactionBlocked; set => Set(ref interactionBlocked, value, InteractionBlockedChanged); }

        static readonly BindablePropertyChangedEventArgs StarSystemNameChanged = new BindablePropertyChangedEventArgs(nameof(StarSystemName));
        string starSystemName;
        [CreateProperty] public string StarSystemName { get => starSystemName; set => Set(ref starSystemName, value, StarSystemNameChanged); }

        static readonly BindablePropertyChangedEventArgs ZoneNameChanged = new BindablePropertyChangedEventArgs(nameof(ZoneName));
        string zoneName;
        [CreateProperty] public string ZoneName { get => zoneName; set => Set(ref zoneName, value, ZoneNameChanged); }

        static readonly BindablePropertyChangedEventArgs HasObjectiveChanged = new BindablePropertyChangedEventArgs(nameof(HasObjective));
        bool hasObjective;
        [CreateProperty] public bool HasObjective { get => hasObjective; set => Set(ref hasObjective, value, HasObjectiveChanged); }

        static readonly BindablePropertyChangedEventArgs ObjectiveTitleChanged = new BindablePropertyChangedEventArgs(nameof(ObjectiveTitle));
        string objectiveTitle;
        [CreateProperty] public string ObjectiveTitle { get => objectiveTitle; set => Set(ref objectiveTitle, value, ObjectiveTitleChanged); }

        static readonly BindablePropertyChangedEventArgs ObjectiveTextChanged = new BindablePropertyChangedEventArgs(nameof(ObjectiveText));
        string objectiveText;
        [CreateProperty] public string ObjectiveText { get => objectiveText; set => Set(ref objectiveText, value, ObjectiveTextChanged); }

        static readonly BindablePropertyChangedEventArgs HasRouteChanged = new BindablePropertyChangedEventArgs(nameof(HasRoute));
        bool hasRoute;
        [CreateProperty] public bool HasRoute { get => hasRoute; set => Set(ref hasRoute, value, HasRouteChanged); }

        static readonly BindablePropertyChangedEventArgs RouteTextChanged = new BindablePropertyChangedEventArgs(nameof(RouteText));
        string routeText;
        [CreateProperty] public string RouteText { get => routeText; set => Set(ref routeText, value, RouteTextChanged); }

        static readonly BindablePropertyChangedEventArgs RouteBlockedChanged = new BindablePropertyChangedEventArgs(nameof(RouteBlocked));
        bool routeBlocked;
        [CreateProperty] public bool RouteBlocked { get => routeBlocked; set => Set(ref routeBlocked, value, RouteBlockedChanged); }

        static readonly BindablePropertyChangedEventArgs WeaponGroupsChanged = new BindablePropertyChangedEventArgs(nameof(WeaponGroups));
        [CreateProperty] public List<HudWeaponGroupViewModel> WeaponGroups { get; } = new List<HudWeaponGroupViewModel>(3);
        public void NotifyWeaponGroups() => Notify(WeaponGroupsChanged);

        static readonly BindablePropertyChangedEventArgs AbilitiesChanged = new BindablePropertyChangedEventArgs(nameof(Abilities));
        [CreateProperty] public List<HudAbilityViewModel> Abilities { get; } = new List<HudAbilityViewModel>(4);
        public void NotifyAbilities() => Notify(AbilitiesChanged);

        static readonly BindablePropertyChangedEventArgs RadarVersionChanged = new BindablePropertyChangedEventArgs(nameof(RadarVersion));
        int radarVersion;
        [CreateProperty] public int RadarVersion { get => radarVersion; set => Set(ref radarVersion, value, RadarVersionChanged); }

        static readonly BindablePropertyChangedEventArgs RadarRangeMChanged = new BindablePropertyChangedEventArgs(nameof(RadarRangeM));
        float radarRangeM = 2000;
        [CreateProperty] public float RadarRangeM { get => radarRangeM; set => Set(ref radarRangeM, value, RadarRangeMChanged); }

        static readonly BindablePropertyChangedEventArgs RadarYawDegChanged = new BindablePropertyChangedEventArgs(nameof(RadarYawDeg));
        float radarYawDeg;
        [CreateProperty] public float RadarYawDeg { get => radarYawDeg; set => Set(ref radarYawDeg, value, RadarYawDegChanged); }

        public HudViewModel()
        {
            for (int i = 0; i < 3; i++) WeaponGroups.Add(new HudWeaponGroupViewModel());
            for (int i = 0; i < 4; i++) Abilities.Add(new HudAbilityViewModel());
        }
    }
}
