using UnityEngine;

namespace Game.Data.Definitions
{
    /// <summary>Implements SPEC-30 §4.3.</summary>
    [System.Serializable] public struct ProceduralShapeParams
    {
        public ShapeKind kind;
        public UnityEngine.Vector3Int sizeCells;   // Ring: x = outer diameter, y = height, z = inner diameter; Cylinder: x = diameter, y = height; Box: x,y,z; Sphere: x = diameter
        public int wallThicknessCells;             // ≥ 1; 0 = solid
        public BlockDefinition hullBlock;          // structural block filling the shell
        public BlockDefinition coreBlock;          // functional Core block placed once (required when destructible)
        public byte colorIndex;                    // palette colour (SPEC-10 PaletteEntry.Color)
    }
}
