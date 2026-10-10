namespace Game.Data.Definitions
{
    /// <summary>A placeable block. Implements SPEC-04 §4.5. Category is always ItemCategory.Block.</summary>
    [ContentIdPrefix("block_", "weapon_")]
    [UnityEngine.CreateAssetMenu(menuName = "Game/Content/Block", fileName = "block_new")]
    public sealed class BlockDefinition : ItemDefinition
    {
        [UnityEngine.Header("Shape")]
        [UnityEngine.SerializeField] private Game.Data.Blocks.BlockKind kind;
        [UnityEngine.SerializeField] private Game.Data.Blocks.BlockShape shape;                  // Structural only; Cube otherwise
        [UnityEngine.SerializeField] private UnityEngine.Vector3Int[] cells = { UnityEngine.Vector3Int.zero }; // unrotated footprint, must contain (0,0,0)
        [UnityEngine.SerializeField] private UnityEngine.Vector3Int[] clearance = System.Array.Empty<UnityEngine.Vector3Int>(); // must-stay-empty cells, block-local
        [UnityEngine.SerializeField] private Game.Data.Blocks.OrientationMask allowedOrientations = Game.Data.Blocks.OrientationMask.All;
        [UnityEngine.SerializeField] private Game.Data.Blocks.BlockFaceMask attachFaces = Game.Data.Blocks.BlockFaceMask.All;
        [UnityEngine.SerializeField] private BlockDefinition mirrorPartner;                       // null = self (achiral)

        [UnityEngine.Header("Durability and mass")]
        [UnityEngine.SerializeField, UnityEngine.Min(1f)] private float cellHp = 100f;           // HP of ONE cell; Cell.Hp 255 == cellHp
        [UnityEngine.SerializeField, UnityEngine.Min(0f)] private float functionHp;              // whole-block function HP (Functional only)
        [UnityEngine.SerializeField, UnityEngine.Min(0.01f)] private float cellMass = 25f;       // kg per cell
        [UnityEngine.SerializeField] private byte armorClass;                                     // 0 = standard; meaning defined by SPEC-13

        [UnityEngine.Header("Presentation")]
        [UnityEngine.SerializeField] private byte materialLayer;                                   // 0..63, structural texture-array layer (SPEC-11)
        [UnityEngine.SerializeField] private UnityEngine.Mesh propMesh;                           // Prop/Functional: pivot at root cell centre

        [UnityEngine.Header("Function")]
        [UnityEngine.SerializeField] private Game.Data.Blocks.BlockCategory blockCategory;    // display only (§4.6); rules in §6.4
        [UnityEngine.SerializeReference] private Game.Data.Blocks.BlockModule[] modules = System.Array.Empty<Game.Data.Blocks.BlockModule>(); // §4.6; empty unless Functional

        // DATA-010 (M2) adds: [SerializeField] AbilityDefinition[] grantedAbilities; (the weapon link lives in WeaponMountModule)

        public Game.Data.Blocks.BlockKind Kind => kind;
        public Game.Data.Blocks.BlockShape Shape => shape;
        public System.Collections.Generic.IReadOnlyList<UnityEngine.Vector3Int> Cells => cells;
        public System.Collections.Generic.IReadOnlyList<UnityEngine.Vector3Int> Clearance => clearance;
        public Game.Data.Blocks.OrientationMask AllowedOrientations => allowedOrientations;
        public Game.Data.Blocks.BlockFaceMask AttachFaces => attachFaces;
        public BlockDefinition MirrorPartner => mirrorPartner != null ? mirrorPartner : this;
        public float CellHp => cellHp; public float FunctionHp => functionHp; public float CellMass => cellMass;
        public byte ArmorClass => armorClass; public byte MaterialLayer => materialLayer;
        public UnityEngine.Mesh PropMesh => propMesh;
        public Game.Data.Blocks.BlockCategory BlockCategory => blockCategory;
        public System.Collections.Generic.IReadOnlyList<Game.Data.Blocks.BlockModule> Modules => modules;
        /// <summary>First module of type T, else false.</summary>
        public bool TryGetModule<T>(out T module) where T : Game.Data.Blocks.BlockModule { throw new System.NotImplementedException("DATA-004"); }
        public bool RequiresInstance => kind == Game.Data.Blocks.BlockKind.Functional || cells.Length > 1;
    }
}
