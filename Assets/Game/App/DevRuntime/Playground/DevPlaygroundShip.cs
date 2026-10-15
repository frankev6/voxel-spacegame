#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Game.App.Flow;
using Game.App.Session;
using Game.Core;
using Game.Core.Flow;
using Game.Data.Definitions;
using Game.Persistence.Services;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Unity.Mathematics;

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
            var clone = source.Clone();
            // Owner: "when i ran "playground ship=ship_nocturne_requiem", it doesn't work, only worked with the wraith". The
            // shield fit commits through the ship editor, which refuses a ship in several pieces; older content (ship_red_elite)
            // still carries loose cells, so only the largest face-connected piece flies here.
            int loose = KeepLargestPiece(clone, new VoxelContent { Blocks = content.BlockDefs, Orientations = content.Orientations });
            if (loose > 0) GameLog.Warn(LogCategories.Dev, "Playground ship " + Override + ": removed " + loose + " cells not connected to the hull.");
            session.Fleet.ReplaceStructure(ship.PersistentId, clone);
            ship.SourceBlueprint = definition.Id;
            string name = string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.IdString : definition.DisplayName;
            ship.Name = name.Length > 32 ? name.Substring(0, 32) : name;
        }
        static int KeepLargestPiece(VoxelStructure structure, VoxelContent content)
        {
            if (!structure.TryGetCellBounds(out var min, out var max)) return 0;
            var solid = new HashSet<int3>();
            for (int z = min.z; z <= max.z; z++) for (int y = min.y; y <= max.y; y++) for (int x = min.x; x <= max.x; x++)
                if (!structure.GetCell(new int3(x, y, z)).IsAir) solid.Add(new int3(x, y, z));
            var pieces = new List<List<int3>>(); var stack = new Stack<int3>(); var unvisited = new HashSet<int3>(solid);
            foreach (var start in solid)
            {
                if (!unvisited.Remove(start)) continue;
                var piece = new List<int3>(); stack.Push(start);
                while (stack.Count > 0)
                {
                    var c = stack.Pop(); piece.Add(c);
                    for (int d = 0; d < 6; d++)
                    {
                        var n = c; n[d >> 1] += (d & 1) == 0 ? 1 : -1;
                        if (unvisited.Remove(n)) stack.Push(n);
                    }
                }
                pieces.Add(piece);
            }
            if (pieces.Count < 2) return 0;
            pieces.Sort((a, b) => b.Count.CompareTo(a.Count));
            int before = structure.SolidCellCount;
            for (int i = 1; i < pieces.Count; i++)
                foreach (var cell in pieces[i]) if (!structure.GetCell(cell).IsAir) structure.TryRemoveBlockAt(cell, content, out _);
            return before - structure.SolidCellCount;
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
