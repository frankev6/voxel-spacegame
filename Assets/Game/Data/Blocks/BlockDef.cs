namespace Game.Data.Blocks
{
    /// <summary>Baked block definition. Implements SPEC-04 §4.7 (architecture §6.2 BlockDef).</summary>
    public struct BlockDef
    {
        public ushort Index;                 // == position in BlockDefsBlob.Blocks; 0 = air
        public ushort MirrorIndex;           // chiral partner's index, or Index if achiral; 0 for air
        public BlockKind Kind;
        public BlockShape Shape;
        public BlockFaceMask AttachFaces;    // block-local faces
        public byte MaterialLayer;
        public byte ArmorClass;
        public uint AllowedOrientations;     // 24-bit mask
        public Unity.Mathematics.int3 MirrorOffset;   // Footprint(MirrorIndex) == { Sx(c) + MirrorOffset | c ∈ Footprint(Index) }
        public Unity.Mathematics.int3 BoundsMin;      // inclusive, unrotated footprint
        public Unity.Mathematics.int3 BoundsMax;      // inclusive
        public float CellHp;
        public float FunctionHp;             // 0 unless Functional
        public float CellMass;               // kg per cell
        public int PropMeshIndex;            // index into ContentService.PropMeshes, -1 = none
        public Game.Core.ContentHash IdHash; // ContentId.Hash: lets ECS code map a BlockIndex back to its content (diagnostics, saves go through the catalog)
        public Unity.Entities.BlobArray<Unity.Mathematics.int3> Cells;      // [0] == (0,0,0); rest sorted by (z, y, x)
        public Unity.Entities.BlobArray<BlockShape> Shapes; // resolved, parallel to canonical Cells (ADR-0028)
        public Unity.Entities.BlobArray<Unity.Mathematics.int3> Clearance;  // sorted by (z, y, x)
        public Unity.Entities.BlobArray<ushort> AbilityIndices;             // IndexOf<AbilityDefinition>; empty until DATA-010
        public ulong ModuleMask;             // bit (int)BlockModuleId set for every module of the block (§4.6); 0 for non-functional blocks
        public BlockCategory Category;       // display only

        public BlockShape GetShape(int footprintIndex) => Shapes.Length == 0 ? Shape : Shapes[footprintIndex];
        public bool RequiresInstance => Kind == BlockKind.Functional || Cells.Length > 1;
        public bool HasModule(BlockModuleId id) => (ModuleMask & (1ul << (int)id)) != 0;
        public bool IsCore => HasModule(BlockModuleId.Core);
    }
}
