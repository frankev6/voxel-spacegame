namespace Game.ShipEditor.Data
{
    /// <summary>Implements SPEC-51 §4.2. Editor contract.</summary>
    [UnityEngine.CreateAssetMenu(menuName = "Game/Ship Editor/Config")]
    public sealed class ShipEditorConfig : UnityEngine.ScriptableObject
    {
        public int MaxCells = 1_000_000;
        public UnityEngine.Vector3Int MaxExtentCells = new UnityEngine.Vector3Int(256, 128, 256);
        public UnityEngine.Vector3Int BuildLimitMin = new UnityEngine.Vector3Int(-512, -256, -512);
        public UnityEngine.Vector3Int BuildLimitMax = new UnityEngine.Vector3Int(511, 255, 511);
        public long HistoryBudgetBytes = 64L * 1024 * 1024;
        public int HistoryMaxEntries = 4096;
        public int SplitCheckBudgetCells = 8192;
        public int MaxBatchCells = 262_144;
        public int FillMaxCells = 65_536;
        public int LineMaxCells = 256;
        public int SyncRemeshBricksPerFrame = 8;
        public int AsyncRemeshBricksPerFrame = 64;
        public int OverlayMaxInstances = 65_536;
        public int PreviewOutlineThreshold = 4096;
        public float PresenterRefreshSeconds = 0.1f;
        public int MaxPickSteps = 4096;
        public UnityEngine.Material BrickMaterial, BrickXRayMaterial, OverlayMaterial;
        public UnityEngine.Color ValidColor, InvalidColor, ClearanceColor, MirrorGhostColor, SelectionColor,
                                 DisconnectedColor, DamagedColor, MirrorPlaneColor, GridColor;
    }
}
