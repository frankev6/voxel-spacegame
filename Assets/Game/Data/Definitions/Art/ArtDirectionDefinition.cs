namespace Game.Data.Definitions
{
    /// <summary>The root of one art direction. Exactly one is active. Implements SPEC-80 §4.1.</summary>
    [ContentIdPrefix("artdir_")]
    [UnityEngine.CreateAssetMenu(menuName = "Game/Art/Art Direction", fileName = "artdir_new")]
    public sealed class ArtDirectionDefinition : ContentDefinition
    {
        [UnityEngine.SerializeField] private string displayName;                    // "DRYDOCK"
        [UnityEngine.SerializeField] private bool isActive;                         // exactly one true (ART-E001)
        [UnityEngine.SerializeField] private BrickMaterialDefinition brickMaterial;
        [UnityEngine.SerializeField] private UnityEngine.Material propMaterial;      // M_Prop_Trim.mat (SPEC-11 prop shader)
        [UnityEngine.SerializeField] private UnityEngine.Material skyMaterial;       // M_Sky_Procedural.mat
        [UnityEngine.SerializeField] private UnityEngine.Color[] paintPalette = new UnityEngine.Color[32]; // indices 0..31, DESIGN-SYSTEM §10.13
        [UnityEngine.SerializeField] private ArtFunctionColors functionColors;
        [UnityEngine.SerializeField] private ArtFactionLook[] factionLooks = System.Array.Empty<ArtFactionLook>();
        [UnityEngine.SerializeField] private string fallbackFactionId = "faction_ark"; // look used when a faction has none
        [UnityEngine.SerializeField] private ArtDamageTypeColors damageTypeColors;
        [UnityEngine.SerializeField] private UnityEngine.Color[] spectralColors = new UnityEngine.Color[7]; // O, B, A, F, G, K, M
        [UnityEngine.SerializeField] private ArtLightingLook lighting;
        [UnityEngine.SerializeField] private ArtPostLook post;
        [UnityEngine.SerializeField] private float hulkPowerDownSeconds = 0.6f;     // 0.6 / 1.2 / 0.8

        public string DisplayName => displayName;
        public bool IsActive => isActive;
        public BrickMaterialDefinition BrickMaterial => brickMaterial;
        public UnityEngine.Material PropMaterial => propMaterial;
        public UnityEngine.Material SkyMaterial => skyMaterial;
        public System.Collections.Generic.IReadOnlyList<UnityEngine.Color> PaintPalette => paintPalette;
        public ArtFunctionColors FunctionColors => functionColors;
        public System.Collections.Generic.IReadOnlyList<ArtFactionLook> FactionLooks => factionLooks;
        public string FallbackFactionId => fallbackFactionId;
        public ArtDamageTypeColors DamageTypeColors => damageTypeColors;
        public System.Collections.Generic.IReadOnlyList<UnityEngine.Color> SpectralColors => spectralColors;
        public ArtLightingLook Lighting => lighting;
        public ArtPostLook Post => post;
        public float HulkPowerDownSeconds => hulkPowerDownSeconds;
    }
}
