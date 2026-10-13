using UnityEngine;

namespace Game.Data.Definitions
{
    /// <summary>Implements SPEC-30 §4.3.</summary>
    [ContentIdPrefix("bodyvis_")]
    [UnityEngine.CreateAssetMenu(menuName = "Game/Content/World/Body Visual", fileName = "bodyvis_new")]
    public sealed class BodyVisualDefinition : ContentDefinition
    {
        [SerializeField] private UnityEngine.Mesh mesh;                // placeholder; centred at the origin
        [SerializeField] private float meshRadius = 0.5f;              // radius of `mesh` in mesh units (Unity sphere = 0.5)
        [SerializeField] private UnityEngine.Material material;        // shared placeholder material (never instanced)
        [SerializeField] private UnityEngine.Color baseColor = UnityEngine.Color.gray;  // per-instance material property
        [SerializeField] private UnityEngine.Color emissionColor = UnityEngine.Color.black;
        [SerializeField] private bool castShadows;
        [SerializeField] private bool hideWhenGridPresent = true;      // impostor of a voxel body
        [SerializeField] private UnityEngine.Sprite mapIcon;
        [SerializeField] private string mapColorToken = "--color-map-body";   // USS token for map UI (SPEC-61)

        public UnityEngine.Mesh Mesh => mesh;
        public float MeshRadius => meshRadius;
        public UnityEngine.Material Material => material;
        public UnityEngine.Color BaseColor => baseColor;
        public UnityEngine.Color EmissionColor => emissionColor;
        public bool CastShadows => castShadows;
        public bool HideWhenGridPresent => hideWhenGridPresent;
        public UnityEngine.Sprite MapIcon => mapIcon;
        public string MapColorToken => mapColorToken;

        // TODO(GAL-003): validate the authored world fields and cross-content references.
        public override void Validate(Game.Data.Validation.ValidationScope scope, Game.Data.Validation.ContentValidationContext context)
        {
            base.Validate(scope, context);
            throw new System.NotImplementedException("GAL-003");
        }
    }
}
