using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Definitions
{
    /// <summary>A place's mechanism rules (ADR-0119 decision 2, SPEC-91 §4.6): when a trigger fires, do its actions. Placed with a
    /// group id by HazardSpawn; MechanismSystem (authority) evaluates it. Places change only through these rules (decision 9).</summary>
    [ContentIdPrefix("mechanism_")]
    [CreateAssetMenu(menuName = "Game/Content/Hazards/Mechanism", fileName = "mechanism_new")]
    public sealed class MechanismDefinition : ContentDefinition
    {
        /// <summary>What fires a rule. Append only.</summary>
        public enum TriggerKind : byte
        {
            SwitchDamaged = 0,      // a Switch block of Group takes its trigger damage
            InstanceDestroyed = 1,  // a block instance of Group is destroyed (Value: the block's ContentId hash, 0 = any)
            ZoneEntered = 2,        // a ship enters the zone of Group whose Id is Value
            GateCrossed = 3,        // a ship crosses the gate whose Id is Value
            Timer = 4,              // every Value seconds, offset by Offset seconds
            RaceLap = 5,            // the race leader starts lap Value
            Narrative = 6,          // the narrative condition Condition holds (checked once per second)
        }

        /// <summary>One action; the kinds mirror Game.Simulation.Mechanisms.MechanismActionKind (same values, append only).</summary>
        public enum ActionKind : byte
        {
            MoverPlay = 0, MoverReverse = 1, MoverSpeed = 2, ZoneEnable = 3, ZoneDisable = 4, ZonePeriod = 5,
            MinesArm = 6, MinesDisarm = 7, TargetPolicy = 8, PortalJam = 9, SpawnHazardGroup = 10, NarrativeEffect = 11,
        }

        [System.Serializable] public struct Trigger
        {
            public TriggerKind Kind;
            public ushort Group;            // 0 = the group the mechanism is placed with
            public float Value;
            public float Offset;            // Timer only: seconds
            public string Condition;        // Narrative only: condition id
        }

        [System.Serializable] public struct Action
        {
            public ActionKind Kind;
            public ushort Group;            // the group it acts on; 0 = the mechanism's own group
            public float Value;             // meaning per kind: SPEC-91 §4.6
            public float DelaySeconds;      // after the trigger
        }

        [System.Serializable] public struct Rule
        {
            public Trigger When;
            public Action[] Do;
        }

        [SerializeField] private Rule[] rules = System.Array.Empty<Rule>();
        public System.Collections.Generic.IReadOnlyList<Rule> Rules => rules;

        public override void Validate(ValidationScope scope, ContentValidationContext context)
        {
            base.Validate(scope, context);
            if (rules == null || rules.Length == 0) { scope.Error("HAZ-E004", nameof(rules), "A mechanism needs at least one rule."); return; }
            for (int i = 0; i < rules.Length; i++)
            {
                var r = rules[i];
                if (r.Do == null || r.Do.Length == 0) scope.Error("HAZ-E004", $"rules[{i}].Do", "A rule needs at least one action.");
                if (r.When.Kind == TriggerKind.Timer && !(r.When.Value > 0)) scope.Error("HAZ-E004", $"rules[{i}].When.Value", "A timer needs a positive period.");
                if (r.When.Kind == TriggerKind.Narrative && string.IsNullOrEmpty(r.When.Condition))
                    scope.Error("HAZ-E004", $"rules[{i}].When.Condition", "A narrative trigger needs a condition id.");
            }
        }
    }
}
