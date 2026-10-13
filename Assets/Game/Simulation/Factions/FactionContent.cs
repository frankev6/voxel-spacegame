using System.Collections.Generic;
using Game.Data.Definitions;
using Game.Data.Services;
using Unity.Entities;

namespace Game.Simulation.Factions
{
    /// <summary>SPEC-33 §4.2 boot contract; ENC-003 owns faction map/matrix/standing creation.</summary>
    public static class FactionContent
    {
        // TODO(ENC-003): build the canonical faction singleton before WeaponContent.
        public static void CreateSingletons(EntityManager em, ContentCatalog catalog, EncounterConfigDefinition config, List<Entity> created)
            => throw new System.NotImplementedException("ENC-003");
    }
}
