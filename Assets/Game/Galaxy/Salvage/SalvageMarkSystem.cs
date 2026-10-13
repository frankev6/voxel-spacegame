namespace Game.Galaxy.Salvage
{
    /// <summary>SPEC-50 §4.5: inert contract shell. ECO-007 enables and places the structural marking system.</summary>
    [Unity.Entities.DisableAutoCreation]
    public partial class SalvageMarkSystem : Unity.Entities.SystemBase
    {
        protected override void OnUpdate() => throw new System.NotImplementedException("ECO-007");
    }
}
