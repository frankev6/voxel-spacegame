namespace Game.Galaxy.Salvage
{
    /// <summary>SPEC-50 §5.6: boot value used by SalvageMarkSystem. ECO-007 owns its factory/boot row.</summary>
    public struct EconomyConfigRef : Unity.Entities.IComponentData { public float SalvageDistanceM; }
}
