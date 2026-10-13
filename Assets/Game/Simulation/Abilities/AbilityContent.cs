using Unity.Entities;

namespace Game.Simulation.Abilities
{
    /// <summary>Implements SPEC-24 §4.2. Boot-time ability content baking.</summary>
    public static class AbilityContent
    {
        // TODO(ABL-002): bake the blob (entry 0 = none) and publish AbilityDefsRef through the boot-owned entity list.
        public static void CreateSingletons(EntityManager em, Game.Data.Services.ContentCatalog catalog, System.Collections.Generic.List<Entity> created)
            => throw new System.NotImplementedException("ABL-002");
    }
}
