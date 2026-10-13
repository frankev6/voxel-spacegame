namespace Game.Data.Definitions
{
    /// <summary>SPEC-33 §4.1 weighted faction ship and optional AI profile.</summary>
    [System.Serializable]
    public struct DoctrineEntry { public ShipBlueprintDefinition blueprint; public float weight; public AiProfileDefinition aiProfile; }
}
