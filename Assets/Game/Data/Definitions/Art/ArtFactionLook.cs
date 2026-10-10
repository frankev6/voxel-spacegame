namespace Game.Data.Definitions
{
    /// <summary>Team colours of one faction (paint slots 17, 18, 19). Implements SPEC-80 §4.1.</summary>
    [System.Serializable]
    public struct ArtFactionLook
    {
        public string factionId;                                  // FactionDefinition id, e.g. "faction_vestari" (validated, ART-E004)
        public UnityEngine.Color primary, secondary, accent;      // sRGB
        public UnityEngine.Color light;                           // faction light / seam colour, sRGB
    }
}
