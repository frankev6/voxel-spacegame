namespace Game.Data.Definitions
{
    /// <summary>A moving part of a prop mesh. Implements SPEC-80 §4.3.</summary>
    [System.Serializable]
    public struct PropPart
    {
        public PropPartRole role;
        public UnityEngine.Mesh meshLod0, meshLod1;           // same units and pivot rules as BlockDefinition.propMesh
        public UnityEngine.Vector3 pivot;                     // cells, block-local
    }
}
