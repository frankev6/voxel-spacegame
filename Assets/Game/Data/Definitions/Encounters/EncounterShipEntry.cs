namespace Game.Data.Definitions
{
    /// <summary>SPEC-33 §4.1 authored blueprint/profile and inclusive ship-count range.</summary>
    [System.Serializable]
    public struct EncounterShipEntry { public ShipBlueprintDefinition blueprint; public AiProfileDefinition aiProfile; public int countMin, countMax; }
}
