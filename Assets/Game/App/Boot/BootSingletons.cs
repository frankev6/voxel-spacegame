using System.Collections.Generic;
using Game.Data.Components;
using Unity.Collections;
using Unity.Entities;

namespace Game.App.Boot
{
    /// <summary>Creates the configuration singletons at boot, in the table order of SPEC-01 §5.3; every entity it makes (or that
    /// ContentService.CreateSingletons made in step 2) carries BootSingleton. Implements SPEC-01 §4.5, §5.3.</summary>
    internal static class BootSingletons
    {
        internal static void CreateAll(EntityManager em, Game.Data.Services.ContentCatalog catalog)
        {
            var created = new List<Entity>();

            // Row 1: BlockDefsRef, OrientationTablesRef (ContentService.CreateSingletons, step 2; DATA-007).
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<BlockDefsRef>()))
            using (var entities = q.ToEntityArray(Allocator.Temp))
                foreach (var e in entities) created.Add(e);

            // TODO(VOX-010): VoxelConfig = VoxelConfigFactory.Create(catalog).
            // Later rows (REN, PHY, DMG, PWR, FLT, ENC-001, WPN, AI, ABL, SYS, ECO-007, FND-008) are added by their tasks (SPEC-01 §5.3).

            foreach (var e in created)
                if (!em.HasComponent<BootSingleton>(e)) em.AddComponent<BootSingleton>(e);
        }

        /// <summary>Writes a component-value singleton with the editor name "Cfg.&lt;Type&gt;" and records it (for factory rows).</summary>
        internal static Entity Create<T>(EntityManager em, T value, List<Entity> created) where T : unmanaged, IComponentData
        {
            var e = em.CreateSingleton(value);
#if UNITY_EDITOR
            em.SetName(e, "Cfg." + typeof(T).Name);
#endif
            created.Add(e);
            return e;
        }
    }
}
