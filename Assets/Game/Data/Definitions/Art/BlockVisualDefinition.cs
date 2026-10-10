namespace Game.Data.Definitions
{
    /// <summary>Art-only extras for one block. Implements SPEC-80 §4.3. Id = "vis_" + block id.</summary>
    [ContentIdPrefix("vis_")]
    [UnityEngine.CreateAssetMenu(menuName = "Game/Art/Block Visual", fileName = "vis_new")]
    public sealed class BlockVisualDefinition : ContentDefinition
    {
        [UnityEngine.SerializeField] private BlockDefinition block;
        [UnityEngine.SerializeField] private UnityEngine.Mesh propMeshLod1;            // LOD1 of block.PropMesh; null if no prop mesh
        [UnityEngine.SerializeField] private PropPart[] parts = System.Array.Empty<PropPart>();
        [UnityEngine.SerializeField] private VisualSocket[] sockets = System.Array.Empty<VisualSocket>();
        [UnityEngine.SerializeField] private byte defaultPaint;                         // paint index on placement (0 = team primary)
        [UnityEngine.SerializeField] private bool paintable = true;                     // false = paint tool skips this block

        public BlockDefinition Block => block;
        public UnityEngine.Mesh PropMeshLod1 => propMeshLod1;
        public System.Collections.Generic.IReadOnlyList<PropPart> Parts => parts;
        public System.Collections.Generic.IReadOnlyList<VisualSocket> Sockets => sockets;
        public byte DefaultPaint => defaultPaint;
        public bool Paintable => paintable;
    }
}
