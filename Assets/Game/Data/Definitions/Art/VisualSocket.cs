namespace Game.Data.Definitions
{
    /// <summary>A visual attachment point imported from FBX empties. Implements SPEC-80 §4.3.</summary>
    [System.Serializable]
    public struct VisualSocket
    {
        public VisualSocketKind kind;
        public byte index;                                    // from the empty's name suffix, MUZZLE_00 → 0
        public UnityEngine.Vector3 position;                  // cells, block-local
        public UnityEngine.Vector3 forward;                   // unit vector, block-local
    }
}
