using UnityEngine;
using System;
using System.Collections.Generic;

namespace Game.Data.Definitions
{
    /// <summary>SPEC-33 §4.1 faction data and local validation; runtime relations are owned by ENC-003.</summary>
    [ContentIdPrefix("faction_")]
    [CreateAssetMenu(menuName = "Game/Content/Faction", fileName = "faction_new")]
    public sealed class FactionDefinition : ContentDefinition
    {
        [SerializeField] private string displayName;
        [SerializeField, UnityEngine.TextArea(2, 5)] private string description;
        [SerializeField] private bool isPlayerFaction;                 // exactly one (faction_ark)
        [SerializeField] private string shipNamePrefix;                // e.g. "VSS"; shown before blueprint names (SPEC-61)
        [SerializeField] private string mapColorToken = "--color-faction-neutral";  // USS token for map/HUD (SPEC-61)
        [SerializeField] private UnityEngine.Color liveryColor = UnityEngine.Color.gray;  // art data for SPEC-80 liveries
        [SerializeField, UnityEngine.Range(-100, 100)] private int basePlayerRelation;
        [SerializeField] private bool relationLocked;                  // relation never changes (raiders, xeno)
        [SerializeField] private System.Collections.Generic.List<FactionStanceEntry> stances = new();   // toward non-player factions
        [SerializeField] private System.Collections.Generic.List<DoctrineEntry> doctrine = new();
        [SerializeField] private AiProfileDefinition defaultAiProfile; // null = AiConfig default
        [SerializeField] private System.Collections.Generic.List<AuthoredStarSystemDefinition> ownedStarSystems = new(); // M3 territory
        [SerializeField, UnityEngine.Range(0, 64)] private int territorySeeds;        // 0 = no procedural territory
        [SerializeField, UnityEngine.Range(0, 8)] private int territoryRadiusHops = 2;

        public string DisplayName => displayName;
        public string Description => description;
        public bool IsPlayerFaction => isPlayerFaction;
        public string ShipNamePrefix => shipNamePrefix;
        public string MapColorToken => mapColorToken;
        public UnityEngine.Color LiveryColor => liveryColor;
        public int BasePlayerRelation => basePlayerRelation;
        public bool RelationLocked => relationLocked;
        public System.Collections.Generic.IReadOnlyList<FactionStanceEntry> Stances => stances;
        public System.Collections.Generic.IReadOnlyList<DoctrineEntry> Doctrine => doctrine;
        public AiProfileDefinition DefaultAiProfile => defaultAiProfile;
        public System.Collections.Generic.IReadOnlyList<AuthoredStarSystemDefinition> OwnedStarSystems => ownedStarSystems;
        public int TerritorySeeds => territorySeeds;
        public int TerritoryRadiusHops => territoryRadiusHops;

        public override void Validate(Game.Data.Validation.ValidationScope scope, Game.Data.Validation.ContentValidationContext context)
        {
            base.Validate(scope, context);
            if (isPlayerFaction && !relationLocked) scope.Error("ENC-E001", nameof(relationLocked), "The player faction's relation must be locked.");
            if (string.IsNullOrEmpty(mapColorToken) || !mapColorToken.StartsWith("--color-", StringComparison.Ordinal))
                scope.Error("ENC-E006", nameof(mapColorToken), "Map color token must start with --color-.");
            if (stances == null) scope.Error("ENC-E003", nameof(stances), "Stance list is null.");
            else
            {
                var seen = new Dictionary<FactionDefinition, FactionStanceValue>();
                for (int i = 0; i < stances.Count; i++)
                {
                    var entry = stances[i];
                    string field = "stances[" + i + "]";
                    if (isPlayerFaction || entry.other == null || entry.other == this || entry.other.IsPlayerFaction ||
                        !Enum.IsDefined(typeof(FactionStanceValue), entry.stance))
                    { scope.Error("ENC-E003", field, "Static stances require two distinct non-player factions and a valid stance."); continue; }
                    if (seen.TryGetValue(entry.other, out var previous) && previous != entry.stance)
                        scope.Error("ENC-E003", field, "The same faction pair has conflicting stances.");
                    else seen[entry.other] = entry.stance;
                }
            }
            if (doctrine == null) scope.Error("ENC-E004", nameof(doctrine), "Doctrine list is null.");
            else
                for (int i = 0; i < doctrine.Count; i++)
                {
                    var entry = doctrine[i];
                    if (entry.blueprint == null) scope.Error("ENC-E004", "doctrine[" + i + "].blueprint", "Doctrine blueprint is not set.");
                    if (!float.IsFinite(entry.weight) || entry.weight <= 0)
                        scope.Error("ENC-E004", "doctrine[" + i + "].weight", "Doctrine weight must be finite and positive.");
                }
        }
    }
}
