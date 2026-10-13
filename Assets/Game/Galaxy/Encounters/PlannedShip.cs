namespace Game.Galaxy.Encounters
{
    /// <summary>SPEC-33 §4.4 managed composition output; ENC-006 fills it.</summary>
    public struct PlannedShip { public Game.Data.Definitions.ShipBlueprintDefinition Blueprint; public Game.Data.Definitions.AiProfileDefinition Profile; public float RadiusM; }
}
