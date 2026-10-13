using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Game.App.DevRuntime;
using Game.Core.Components;
using Game.Data.Blocks;
using Game.Data.Combat;
using Game.Data.Components;
using Game.Voxel.Damage;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;
using Unity.Pipeline.Commands;

namespace Game.DevTools.Editor.Damage
{
    /// <summary>SPEC-13 DMG-017: one-line JSON diagnostics and requests through the real damage channel.</summary>
    public static class DamageCommands
    {
        // Editor main-thread requests use Entity.Null / Test attribution. Keep sequences unique until Play resets.
        static ulong nextSequence;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSequence() => nextSequence = 0;

        [CliCommand("gh_dmg_hash", "Print the canonical damage world hash as one JSON line (Play mode).")]
        public static string Hash() => InPlay("gh_dmg_hash", Hash);

        [CliCommand("gh_dmg_stats", "Print prior-tick and cumulative per-grid damage counters as one JSON line (Play mode).")]
        public static string Stats() => InPlay("gh_dmg_stats", Stats);

        [CliCommand("gh_dmg_hit", "Queue one damage event on a grid containing the local-frame world point (Play mode).")]
        public static string Hit([CliArg("x", "Local-frame world x in metres")] float x,
            [CliArg("y", "Local-frame world y in metres")] float y,
            [CliArg("z", "Local-frame world z in metres")] float z,
            [CliArg("amount", "Finite positive raw damage")] float amount,
            [CliArg("radius", "Finite nonnegative blast radius in metres; zero is a single-cell hit")] float radius)
            => InPlay("gh_dmg_hit", world => Hit(world, x, y, z, amount, radius));

        [CliCommand("gh_dmg_kill_core", "Queue lethal cell hits on the live core of a persistent grid (Play mode).")]
        public static string KillCore([CliArg("persistentId", "Unsigned decimal persistent grid id")] string persistentId)
            => InPlay("gh_dmg_kill_core", world => KillCore(world, persistentId));

        // Explicit-world entry points also drive the required isolated EditMode tests. No game internals are reflection-invoked.
        public static string Hash(World world) => Execute(world, "gh_dmg_hash", em =>
            Success("gh_dmg_hash").Append(",\"hash\":")
                .Append(DevJsonWriter.Quote("0x" + CellStateHasher.ComputeWorldHash(em).ToString("X16", CultureInfo.InvariantCulture)))
                .Append(",\"tick\":").Append(Tick(em).ToString(CultureInfo.InvariantCulture)).Append('}').ToString());

        public static string Stats(World world) => Execute(world, "gh_dmg_stats", em =>
        {
            if (!Singleton(em, out DamageStreams streams)) return Failure("gh_dmg_stats", "damage-unavailable", "Expected one DamageStreams singleton.");
            var text = Success("gh_dmg_stats").Append(",\"lastTick\":");
            TickStats(text, streams.LastTickStats);
            text.Append(",\"grids\":[");
            using var query = em.CreateEntityQuery(typeof(VoxelGrid), typeof(GridDamageStats), typeof(PersistentId));
            var roots = OrderedRoots(em, query);
            for (int i = 0; i < roots.Count; i++)
            {
                if (i != 0) text.Append(',');
                Entity root = roots[i];
                var grid = em.GetComponentData<VoxelGrid>(root);
                var stats = em.GetComponentData<GridDamageStats>(root);
                text.Append('{'); GridId(text, em, root);
                text.Append(",\"kind\":").Append(DevJsonWriter.Quote(grid.Kind.ToString()));
                Number(text, "lastDamageTick", stats.LastDamageTick);
                Number(text, "cellsDestroyed", stats.CellsDestroyed);
                Number(text, "cellsDetached", stats.CellsDetached);
                Number(text, "instancesDestroyed", stats.InstancesDestroyed);
                text.Append(",\"damageTaken\":").Append(DevJsonWriter.Number(stats.DamageTaken));
                text.Append(",\"lastDamageSource\":"); EntityJson(text, stats.LastDamageSource);
                text.Append(",\"lastDamageInstigator\":"); EntityJson(text, stats.LastDamageInstigator);
                text.Append('}');
            }
            return text.Append("]}").ToString();
        });

        public static string Hit(World world, float x, float y, float z, float amount, float radius)
        {
            var point = new float3(x, y, z);
            if (!math.all(math.isfinite(point)) || !math.isfinite(amount) || amount <= 0 || !math.isfinite(radius) || radius < 0)
                return Failure("gh_dmg_hit", "invalid-argument", "Point, positive amount and nonnegative radius must be finite.");
            return Execute(world, "gh_dmg_hit", em =>
            {
                if (!Singleton(em, out DamageStreams streams)) return Failure("gh_dmg_hit", "damage-unavailable", "Expected one DamageStreams singleton.");
                using var query = em.CreateEntityQuery(typeof(VoxelGrid), typeof(LocalTransform), typeof(PersistentId));
                foreach (Entity root in OrderedRoots(em, query))
                {
                    if (!CanDamage(em, root)) continue;
                    var grid = em.GetComponentData<VoxelGrid>(root);
                    if (!math.isfinite(grid.CellSize) || grid.CellSize <= 0) continue;
                    var local = em.GetComponentData<LocalTransform>(root).InverseTransformPoint(point);
                    var cellPoint = local / grid.CellSize;
                    // Half-open spawn bounds; holes inside the bounds remain valid blast centres.
                    if (!math.all(math.isfinite(cellPoint)) || math.any(cellPoint < (float3)grid.CellMin) ||
                        math.any(cellPoint >= (float3)grid.CellMax + 1f)) continue;
                    if (!ReserveSequences(1, out uint sequence)) return Failure("gh_dmg_hit", "sequence-exhausted", "Restart Play before issuing more damage requests.");
                    streams.Damage.PostFromMainThread(Event(root, local, amount, radius, sequence));
                    return Queued("gh_dmg_hit", em, root, 1, sequence);
                }
                return Failure("gh_dmg_hit", "grid-not-found", "No ready damageable grid contains that world point.");
            });
        }

        public static string KillCore(World world, string persistentId)
        {
            if (!ulong.TryParse(persistentId, NumberStyles.None, CultureInfo.InvariantCulture, out ulong id) || id == 0)
                return Failure("gh_dmg_kill_core", "invalid-argument", "persistentId must be a nonzero unsigned decimal id.");
            return Execute(world, "gh_dmg_kill_core", em =>
            {
                if (!Singleton(em, out DamageStreams streams) || !Singleton(em, out BlockDefsRef blocks) || !blocks.Value.IsCreated ||
                    !Singleton(em, out DamageConfig config))
                    return Failure("gh_dmg_kill_core", "damage-unavailable", "Damage streams, block definitions and damage configuration must be ready.");
                using var query = em.CreateEntityQuery(typeof(VoxelGrid), typeof(PersistentId));
                using var roots = query.ToEntityArray(Allocator.Temp);
                Entity root = Entity.Null;
                foreach (var candidate in roots)
                    if (em.GetComponentData<PersistentId>(candidate).Value == id)
                    {
                        if (root != Entity.Null) return Failure("gh_dmg_kill_core", "ambiguous-id", "More than one grid has that persistent id.");
                        root = candidate;
                    }
                if (root == Entity.Null) return Failure("gh_dmg_kill_core", "grid-not-found", "No grid has that persistent id.");
                if (!CanDamage(em, root) || !em.HasBuffer<BlockInstance>(root))
                    return Failure("gh_dmg_kill_core", "grid-not-ready", "The grid is spawning, immune or lacks instance storage.");
                var instances = em.GetBuffer<BlockInstance>(root);
                int coreIndex = -1;
                for (int i = 0; i < instances.Length; i++)
                {
                    var instance = instances[i];
                    if (instance.Block == 0 || instance.Block >= blocks.Value.Value.Blocks.Length ||
                        (instance.Flags & BlockInstanceFlags.Destroyed) != 0 || instance.FunctionHp <= 0) continue;
                    if (blocks.Value.Value.Blocks[instance.Block].IsCore) { coreIndex = i; break; }
                }
                if (coreIndex < 0) return Failure("gh_dmg_kill_core", "core-not-found", "The grid has no live core instance.");
                var core = instances[coreIndex];
                ref var definition = ref blocks.Value.Value.Blocks[core.Block];
                float multiplier = config.Multiplier(DamageType.Kinetic, definition.ArmorClass);
                double raw = 2d * definition.CellHp / multiplier;
                if (!(multiplier > 0) || !float.IsFinite(multiplier) || !(raw > 0) || raw > float.MaxValue || double.IsNaN(raw))
                    return Failure("gh_dmg_kill_core", "core-damage-unsupported", "The core does not permit a finite lethal kinetic cell hit.");
                var grid = em.GetComponentData<VoxelGrid>(root);
                if (!float.IsFinite(grid.CellSize) || grid.CellSize <= 0 || core.Orient >= 24)
                    return Failure("gh_dmg_kill_core", "grid-not-ready", "Core orientation and cell size must be valid.");
                var points = new List<float3>(definition.Cells.Length);
                for (int i = 0; i < definition.Cells.Length; i++)
                {
                    int3 cell = core.Origin + OrientationTables.Rotate(definition.Cells[i], core.Orient);
                    if (TryCell(em, root, grid, cell, out var value) && value.IsInstance && value.InstanceIndex == coreIndex)
                    {
                        float3 local = ((float3)cell + .5f) * grid.CellSize;
                        if (!math.all(math.isfinite(local))) return Failure("gh_dmg_kill_core", "grid-not-ready", "Core cell centres must be finite.");
                        points.Add(local);
                    }
                }
                if (points.Count == 0) return Failure("gh_dmg_kill_core", "core-not-found", "The core has no remaining solid cells.");
                if (!ReserveSequences(points.Count, out uint first)) return Failure("gh_dmg_kill_core", "sequence-exhausted", "Restart Play before issuing more damage requests.");
                // Removal deducts only remaining cell HP. Hit each remaining footprint cell so multi-cell cores also collapse.
                for (int i = 0; i < points.Count; i++)
                    streams.Damage.PostFromMainThread(Event(root, points[i], (float)raw, 0, first + (uint)i));
                return Queued("gh_dmg_kill_core", em, root, points.Count, first);
            });
        }

        static DamageEvent Event(Entity root, float3 local, float amount, float radius, uint sequence) => new DamageEvent
        {
            Grid = root, LocalPoint = local, LocalDir = default, Amount = amount, Radius = radius, Penetration = 1,
            Type = DamageType.Kinetic, Source = Entity.Null, Instigator = Entity.Null, Producer = ProducerKind.Test, Sequence = sequence
        };

        static string InPlay(string command, Func<World, string> run)
        {
            if (!EditorApplication.isPlaying) return Failure(command, "not-playing", "The Editor must be playing.");
            return run(World.DefaultGameObjectInjectionWorld);
        }

        static string Execute(World world, string command, Func<EntityManager, string> run)
        {
            if (world == null || !world.IsCreated) return Failure(command, "world-unavailable", "No live ECS world is available.");
            try
            {
                var em = world.EntityManager;
                em.CompleteAllTrackedJobs(); // Editor command boundary, outside world updates; never a fixed-step sync point.
                return run(em);
            }
            catch (Exception error) { return Failure(command, "command-error", error.Message); }
        }

        static bool Singleton<T>(EntityManager em, out T value) where T : unmanaged, IComponentData
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            if (query.CalculateEntityCount() != 1) { value = default; return false; }
            value = query.GetSingleton<T>(); return true;
        }
        static uint Tick(EntityManager em) => Singleton(em, out SimClock clock) ? clock.Tick : 0;
        static bool CanDamage(EntityManager em, Entity root) => !em.HasComponent<DamageImmune>(root) &&
            !(em.HasComponent<GridSpawning>(root) && em.IsComponentEnabled<GridSpawning>(root));
        static List<Entity> OrderedRoots(EntityManager em, EntityQuery query)
        {
            using var entities = query.ToEntityArray(Allocator.Temp);
            var roots = new List<Entity>(entities.Length);
            foreach (Entity root in entities) if (em.GetComponentData<PersistentId>(root).Value != 0) roots.Add(root);
            roots.Sort((a, b) =>
            {
                int order = em.GetComponentData<PersistentId>(a).Value.CompareTo(em.GetComponentData<PersistentId>(b).Value);
                if (order == 0) order = em.GetComponentData<VoxelGrid>(a).Serial.CompareTo(em.GetComponentData<VoxelGrid>(b).Serial);
                return order == 0 ? a.Index.CompareTo(b.Index) : order;
            });
            return roots;
        }
        static bool TryCell(EntityManager em, Entity root, in VoxelGrid grid, int3 cell, out Cell value)
        {
            value = default;
            if (!em.HasBuffer<BrickSlot>(root)) return false;
            int slot = BrickMath.SlotIndex(BrickMath.BrickOf(cell), grid.BrickMin, grid.BrickDims);
            var slots = em.GetBuffer<BrickSlot>(root);
            if (slot < 0 || slot >= slots.Length) return false;
            Entity brick = slots[slot].Brick;
            if (brick == Entity.Null || !em.Exists(brick) || !em.HasBuffer<BrickCells>(brick) || !em.HasBuffer<BrickSparseCells>(brick)) return false;
            value = BrickWrite.Get(em.GetBuffer<BrickCells>(brick), em.GetBuffer<BrickSparseCells>(brick), BrickMath.LocalIndex(BrickMath.LocalOf(cell)));
            return !value.IsAir;
        }
        static bool ReserveSequences(int count, out uint first)
        {
            first = 0;
            if (count <= 0 || nextSequence + (ulong)count - 1 > uint.MaxValue) return false;
            first = (uint)nextSequence; nextSequence += (ulong)count; return true;
        }

        static StringBuilder Success(string command) => new StringBuilder("{\"ok\":true,\"command\":").Append(DevJsonWriter.Quote(command));
        static string Failure(string command, string code, string message) => "{\"ok\":false,\"command\":" +
            DevJsonWriter.Quote(command) + ",\"error\":" + DevJsonWriter.Quote(code) + ",\"message\":" + DevJsonWriter.Quote(message) + "}";
        static void Number(StringBuilder text, string name, long value) => text.Append(',').Append(DevJsonWriter.Quote(name)).Append(':').Append(value.ToString(CultureInfo.InvariantCulture));
        static void EntityJson(StringBuilder text, Entity entity) => text.Append("{\"index\":").Append(entity.Index.ToString(CultureInfo.InvariantCulture))
            .Append(",\"version\":").Append(entity.Version.ToString(CultureInfo.InvariantCulture)).Append('}');
        static void GridId(StringBuilder text, EntityManager em, Entity root) => text.Append("\"persistentId\":")
            .Append(DevJsonWriter.Quote(em.GetComponentData<PersistentId>(root).Value.ToString(CultureInfo.InvariantCulture)));
        static string Queued(string command, EntityManager em, Entity root, int count, uint first)
        {
            var text = Success(command).Append(','); GridId(text, em, root);
            text.Append(",\"queued\":true"); Number(text, "queuedEvents", count); Number(text, "firstSequence", first); Number(text, "tick", Tick(em));
            return text.Append('}').ToString();
        }
        static void TickStats(StringBuilder text, in DamageTickStats stats)
        {
            text.Append("{\"tick\":").Append(stats.Tick.ToString(CultureInfo.InvariantCulture));
            Number(text, "eventsIn", stats.EventsIn); Number(text, "eventsDropped", stats.EventsDropped);
            Number(text, "gridsResolved", stats.GridsResolved); Number(text, "cellsDestroyed", stats.CellsDestroyed);
            Number(text, "cellsDetached", stats.CellsDetached); Number(text, "instancesDestroyed", stats.InstancesDestroyed);
            Number(text, "integrityGrids", stats.IntegrityGrids); Number(text, "integrityDeferred", stats.IntegrityDeferred);
            Number(text, "integritySizeDeferred", stats.IntegritySizeDeferred); Number(text, "islandsFound", stats.IslandsFound);
            Number(text, "hulksCreated", stats.HulksCreated); Number(text, "chunksCreated", stats.ChunksCreated);
            Number(text, "particleIslands", stats.ParticleIslands); Number(text, "splitsDeferred", stats.SplitsDeferred);
            Number(text, "debrisParticles", stats.DebrisParticles); Number(text, "ramEvents", stats.RamEvents);
            Number(text, "impulsesApplied", stats.ImpulsesApplied); Number(text, "impulsesDropped", stats.ImpulsesDropped);
            Number(text, "cellsHealed", stats.CellsHealed); Number(text, "cellsPlaced", stats.CellsPlaced);
            Number(text, "restoresDropped", stats.RestoresDropped); Number(text, "restoresDeferred", stats.RestoresDeferred);
            text.Append('}');
        }
    }
}
