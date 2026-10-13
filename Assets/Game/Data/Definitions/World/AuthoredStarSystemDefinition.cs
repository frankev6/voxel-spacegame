using UnityEngine;

namespace Game.Data.Definitions
{
    /// <summary>Implements SPEC-30 §4.3.</summary>
    [ContentIdPrefix("starsys_")]
    [UnityEngine.CreateAssetMenu(menuName = "Game/Content/World/Authored Star System", fileName = "starsys_new")]
    public sealed class AuthoredStarSystemDefinition : ContentDefinition
    {
        [SerializeField] private string displayName;
        [SerializeField] private UnityEngine.Vector2 positionLy;           // galaxy frame
        [SerializeField] private StarSystemTemplateDefinition template;    // supplies orbit/period/lane-ring parameters
        [SerializeField] private BodyTemplateDefinition star;
        [SerializeField] private System.Collections.Generic.List<AuthoredBody> bodies = new();
        [SerializeField] private bool hiddenUntilKnown, isStory, isHub, noEncounters;
        [SerializeField] private bool connectProcedurally;                  // joins the procedural lane graph
        [SerializeField, UnityEngine.Range(0, 3)] private int danger;
        [SerializeField] private string startBodyKey;                       // start star system only: spawn near this body
        [SerializeField] private UnityEngine.Vector3 startOffsetM = new UnityEngine.Vector3(0f, 0f, -400f); // from that body at UniversalTime 0
        [SerializeField] private float startYawDegrees;

        public string DisplayName => displayName;
        public UnityEngine.Vector2 PositionLy => positionLy;
        public StarSystemTemplateDefinition Template => template;
        public BodyTemplateDefinition Star => star;
        public System.Collections.Generic.IReadOnlyList<AuthoredBody> Bodies => bodies;
        public bool HiddenUntilKnown => hiddenUntilKnown;
        public bool IsStory => isStory;
        public bool IsHub => isHub;
        public bool NoEncounters => noEncounters;
        public bool ConnectProcedurally => connectProcedurally;
        public int Danger => danger;
        public string StartBodyKey => startBodyKey;
        public UnityEngine.Vector3 StartOffsetM => startOffsetM;
        public float StartYawDegrees => startYawDegrees;

        // TODO(GAL-003): validate the authored world fields and cross-content references.
        public override void Validate(Game.Data.Validation.ValidationScope scope, Game.Data.Validation.ContentValidationContext context)
        {
            base.Validate(scope, context);
            throw new System.NotImplementedException("GAL-003");
        }
    }
}
