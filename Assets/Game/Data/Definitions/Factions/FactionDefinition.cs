using UnityEngine;

namespace Game.Data.Definitions
{
    /// <summary>SPEC-33 §4.1 contract; ENC-002 owns validation.</summary>
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

        // TODO(ENC-002): implement the definition checks from SPEC-33 §4.1.
        public override void Validate(Game.Data.Validation.ValidationScope scope, Game.Data.Validation.ContentValidationContext context)
        {
            base.Validate(scope, context);
            throw new System.NotImplementedException("ENC-002");
        }
    }
}
