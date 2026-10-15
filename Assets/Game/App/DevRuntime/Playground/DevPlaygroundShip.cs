#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Game.App.Flow;
using Game.App.Session;
using Game.Core;
using Game.Core.Flow;
using Game.Data.Definitions;
using Game.Persistence.Services;
using Game.Voxel.Grid;

namespace Game.App.DevRuntime.Playground
{
    /// <summary>QA-001 (owner: "how can I easily switch ships to test"): `playground ship=&lt;blueprint&gt;` resets the playground
    /// with the unspawned flagship replaced by a clone of any ship blueprint; `ship=starter` restores the configured start ship.
    /// The choice persists across playground resets of this Editor session and never touches a campaign fleet.</summary>
    internal sealed class DevPlaygroundShip : ISceneReady, IDisposable
    {
        readonly DevContext context;
        readonly SceneFlow flow;
        bool disposed;
        internal ContentId Override { get; private set; }

        internal DevPlaygroundShip(DevContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            if (!context.TryGetService(out flow)) throw new InvalidOperationException("SceneFlow unavailable.");
            flow.RegisterReady(this);
        }

        /// <summary>Validates and stores the choice; the caller resets the playground to apply it.</summary>
        internal string Select(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id == "starter" || id == "default") { Override = ContentId.None; return null; }
            if (!context.TryGetService<Game.Data.Services.ContentService>(out var content)) return "content unavailable";
            var ship = new ContentId(id);
            if (!content.Catalog.TryGet<ShipBlueprintDefinition>(ship, out _))
            {
                var ids = new System.Collections.Generic.List<string>();
                foreach (var definition in content.Catalog.All<ShipBlueprintDefinition>()) ids.Add(definition.IdString);
                ids.Sort(StringComparer.Ordinal);
                return "unknown ship blueprint '" + id + "'. Ships: starter, " + string.Join(", ", ids);
            }
            Override = ship; return null;
        }

        // Before the power trial (PlayerShip - 1) fits its shield, and before PlayerShipSpawner borrows the flagship.
        public int SceneReadyOrder => SceneHookOrder.PlayerShip - 2;
        public bool IsSceneReadyComplete(in SceneReadyContext entry) => true;
        public void OnSceneReady(in SceneReadyContext entry)
        {
            if (Override.IsNone || entry.Scene != GameScene.World || entry.Reason != SceneEntryReason.DevQuickStart
                || !context.TryGetService<GameSession>(out var session) || !session.IsSandbox || !session.SpawnPlayerInWorld
                || !IsPlaygroundProfile()) return;
            var ship = session.Fleet.Flagship;
            if (ship == null || ship.StructureHandle >= 0 || ship.Structure.IsPinned) return;
            if (!context.TryGetService<BlueprintLibrary>(out var library) || !context.TryGetService<Game.Data.Services.ContentService>(out var content)
                || !content.Catalog.TryGet<ShipBlueprintDefinition>(Override, out var definition) || !library.TryGet(Override.Hash, out var source))
            { GameLog.Warn(LogCategories.Dev, "Playground ship " + Override + " could not be decoded; flying the starter."); return; }
            session.Fleet.ReplaceStructure(ship.PersistentId, source.Clone());
            ship.SourceBlueprint = definition.Id;
            string name = string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.IdString : definition.DisplayName;
            ship.Name = name.Length > 32 ? name.Substring(0, 32) : name;
        }
        bool IsPlaygroundProfile()
        {
            if (!context.TryGetService<SaveService>(out var save)) return false;
            foreach (var profile in save.ListProfiles())
                if (profile.Id.Equals(save.ActiveProfile)) return profile.DisplayName == "dev-playground";
            return false;
        }
        public void Dispose() { if (disposed) return; disposed = true; flow.UnregisterReady(this); }
    }
}
#endif
