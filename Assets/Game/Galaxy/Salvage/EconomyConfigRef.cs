namespace Game.Galaxy.Salvage
{
    /// <summary>SPEC-50 §5.6: boot values used by SalvageMarkSystem and SalvageReachSystem (QA-001 magnet cap). ECO-007 owns its factory/boot row.</summary>
    public struct EconomyConfigRef : Unity.Entities.IComponentData { public float SalvageDistanceM; public float SalvageReachBonusCapM; }
}
