using System;
using Unity.Entities;

namespace Game.Simulation.Weapons
{
    /// <summary>Implements SPEC-21 weapon contract: WeaponContent.</summary>
    public static class WeaponContent
    {
        public static void CreateSingletons(EntityManager em, Game.Data.Services.ContentCatalog catalog, System.Collections.Generic.List<Entity> created) => throw new NotImplementedException();
        public static BlobAssetReference<WeaponDefsBlob> BuildBlob(Game.Data.Services.ContentCatalog catalog) => throw new NotImplementedException();   // §5.1
        public static float ComputeSustainedDps(in WeaponDef w) => throw new NotImplementedException();   // §4.9
    }
}
