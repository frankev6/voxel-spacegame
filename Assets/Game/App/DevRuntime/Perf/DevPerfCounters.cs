#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Game.Core.Diagnostics;
using Game.Data.Blocks;
using Game.Presentation.Audio;
using Game.Presentation.Vfx;
using Game.Simulation.Weapons;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Entities;
using Unity.Profiling;

namespace Game.App.DevRuntime.Perf
{
    /// <summary>DEV-011: diagnostic snapshots; owns generic recorders/queries and borrows game counters.</summary>
    public sealed class DevPerfCounters : IDisposable
    {
        ProfilerRecorder main, draws, triangles, gc, memory;
        readonly Dictionary<Type, EntityQuery> queries = new();
        readonly List<(string name, double ms)> markers = new();
        World queryWorld;
        bool disposed;
        public DevPerfCounters()
        {
            try
            {
                main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
                draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
                triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
                gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
                memory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
            }
            catch { Dispose(); throw; }
        }
        static double Last(ProfilerRecorder recorder) => recorder.Valid && recorder.Count > 0 ? recorder.LastValue : double.NaN;
        static double AverageMs(ProfilerRecorder recorder)
        {
            if (!recorder.Valid || recorder.Count == 0) return double.NaN;
            double total = 0; for (int i = 0; i < recorder.Count; i++) total += recorder.GetSample(i).Value;
            return total / recorder.Count * 1e-6;
        }
        EntityQuery Query<T>() where T : unmanaged, IComponentData
        {
            if (!queries.TryGetValue(typeof(T), out var query))
            {
                query = queryWorld.EntityManager.CreateEntityQuery(new EntityQueryDesc {
                    All = new[] { ComponentType.ReadOnly<T>() }, Options = EntityQueryOptions.IncludeDisabledEntities });
                queries.Add(typeof(T), query);
            }
            return query;
        }
        internal static FxStats Fx(DevContext ctx) => ctx.World != null && ctx.World.IsCreated
            ? ctx.World.GetExistingSystemManaged<FxUploadSystem>()?.Stats : null;
        internal static AudioStats Audio(DevContext ctx)
        {
            if (ctx.World == null || !ctx.World.IsCreated) return null;
            var system = ctx.World.GetExistingSystemManaged<AudioEventDrainSystem>();
            return system != null ? system.Stats : ctx.TryGetService<AudioService>(out var service) ? service.Stats : null;
        }
        struct VoxelCounts
        {
            public int Ships, Stations, Asteroids, HulkGrids, Bricks, Dirty, MeshDirty, ColliderDirty, Chunks, Hulks;
            public long Cells;
        }
        VoxelCounts ReadVoxel(EntityManager em)
        {
            var count = new VoxelCounts();
            using var grids = Query<VoxelGrid>().ToEntityArray(Allocator.Temp);
            foreach (var root in grids)
            {
                var kind = em.GetComponentData<VoxelGrid>(root).Kind;
                switch (kind) { case GridKind.Ship: count.Ships++; break; case GridKind.Station: count.Stations++; break;
                    case GridKind.Asteroid: count.Asteroids++; break; case GridKind.Hulk: count.HulkGrids++; break; }
                if (kind == GridKind.Hulk || em.HasComponent<Hulk>(root)) count.Hulks++;
            }
            using var bricks = Query<Brick>().ToEntityArray(Allocator.Temp); count.Bricks = bricks.Length;
            foreach (var entity in bricks)
            {
                count.Cells += em.GetComponentData<Brick>(entity).SolidCount;
                if (!em.HasComponent<BrickDirty>(entity) || !em.IsComponentEnabled<BrickDirty>(entity)) continue;
                var flags = em.GetComponentData<BrickDirty>(entity).Flags;
                bool mesh = (flags & BrickDirtyFlags.Mesh) != 0, collider = (flags & BrickDirtyFlags.Collider) != 0;
                if (mesh || collider) count.Dirty++; if (mesh) count.MeshDirty++; if (collider) count.ColliderDirty++;
            }
            using var chunks = Query<ChunkBody>().ToEntityArray(Allocator.Temp);
            foreach (var entity in chunks)
                if (!em.HasComponent<Disabled>(entity) && em.GetComponentData<ChunkBody>(entity).Live != 0) count.Chunks++;
            return count;
        }
        int ReadProjectiles()
        {
            if (!Query<ProjectileStore>().TryGetSingleton<ProjectileStore>(out var store)) return -1;
            store.CompleteAccess(); return store.Stats.IsCreated ? store.Stats.Value.Live : -1;
        }
        int ReadBeams(EntityManager em)
        {
            int count = 0; using var entities = Query<BeamState>().ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
                if (!em.HasComponent<Disabled>(entity) && em.GetComponentData<BeamState>(entity).Active != 0) count++;
            return count;
        }
        static string Display(double value, string format = "R") => double.IsFinite(value) ? value.ToString(format, CultureInfo.InvariantCulture) : "unavailable";
        static void Metric(StringBuilder text, string name, double value, bool comma = true)
        { if (comma) text.Append(','); text.Append(DevJsonWriter.Quote(name)).Append(':').Append(DevJsonWriter.Number(value)); }
        public string Format(DevContext ctx, bool voxel, bool combat, bool includeMarkers, bool json)
        {
            if (disposed) throw new ObjectDisposedException(nameof(DevPerfCounters));
            var world = ctx.World;
            if (!ReferenceEquals(world, queryWorld)) { ReleaseQueries(); queryWorld = world; }
            bool live = world != null && world.IsCreated;
            if (live) world.EntityManager.CompleteAllTrackedJobs();
            var em = live ? world.EntityManager : default;
            double mainMs = AverageMs(main), drawCount = Last(draws), tris = Last(triangles), allocated = Last(gc), usedMemory = Last(memory);
            int entities = live ? em.UniversalQuery.CalculateEntityCount() : 0;
            var voxels = voxel && live ? ReadVoxel(em) : default;
            int projectiles = combat && live ? ReadProjectiles() : -1, beams = combat && live ? ReadBeams(em) : 0;
            var fx = combat ? Fx(ctx) : null; var audio = combat ? Audio(ctx) : null;
            if (includeMarkers)
            {
                PerfMarkers.StartRecording(); PerfMarkers.SampleLastFrame(markers, int.MaxValue);
                markers.Sort((a, b) => { int c = b.ms.CompareTo(a.ms); return c != 0 ? c : string.CompareOrdinal(a.name, b.name); });
                if (markers.Count > 15) markers.RemoveRange(15, markers.Count - 15);
            }
            var text = new StringBuilder();
            if (json)
            {
                text.Append('{'); Metric(text, "mainMs", mainMs, false); Metric(text, "draws", drawCount); Metric(text, "triangles", tris);
                Metric(text, "gcAllocBytes", allocated); Metric(text, "systemMemoryBytes", usedMemory); Metric(text, "entities", entities);
                text.Append(",\"worldAvailable\":").Append(live ? "true" : "false");
                if (voxel)
                {
                    text.Append(",\"voxel\":{"); Metric(text, "ships", voxels.Ships, false); Metric(text, "stations", voxels.Stations);
                    Metric(text, "asteroids", voxels.Asteroids); Metric(text, "hulkGrids", voxels.HulkGrids); Metric(text, "bricks", voxels.Bricks);
                    Metric(text, "solidCells", voxels.Cells); Metric(text, "dirtyBricks", voxels.Dirty); Metric(text, "meshDirty", voxels.MeshDirty);
                    Metric(text, "colliderDirty", voxels.ColliderDirty); Metric(text, "chunkBodies", voxels.Chunks); Metric(text, "hulks", voxels.Hulks); text.Append('}');
                }
                if (combat)
                {
                    text.Append(",\"combat\":{"); Metric(text, "projectiles", projectiles < 0 ? double.NaN : projectiles, false); Metric(text, "activeBeams", beams);
                    text.Append(",\"fx\":");
                    if (fx == null) text.Append("null");
                    else
                    {
                        text.Append('{'); Metric(text, "drained", fx.EventsDrained, false); Metric(text, "culled", fx.Culled); Metric(text, "budgetDropped", fx.BudgetDropped);
                        Metric(text, "debrisLiveEstimate", fx.DebrisLiveEstimate); text.Append(",\"bound\":").Append(fx.Bound ? "true" : "false").Append('}');
                    }
                    text.Append(",\"audio\":");
                    if (audio == null) text.Append("null");
                    else
                    {
                        text.Append('{'); Metric(text, "drained", audio.EventsDrained, false); Metric(text, "started", audio.Started);
                        Metric(text, "activeSfxVoices", audio.ActiveSfxVoices); Metric(text, "activeLoopVoices", audio.ActiveLoopVoices);
                        text.Append(",\"bound\":").Append(audio.Bound ? "true" : "false").Append('}');
                    }
                    text.Append('}');
                }
                if (includeMarkers)
                {
                    text.Append(",\"markers\":[");
                    for (int i = 0; i < markers.Count; i++)
                    { if (i != 0) text.Append(','); text.Append("{\"name\":").Append(DevJsonWriter.Quote(markers[i].name)); Metric(text, "ms", markers[i].ms); text.Append('}'); }
                    text.Append(']');
                }
                return text.Append('}').ToString();
            }
            text.Append("main=").Append(Display(mainMs, "F2")).Append("ms draws=").Append(Display(drawCount)).Append(" tris=").Append(Display(tris / 1e6, "F2"))
                .Append("M gcAlloc/frame=").Append(Display(allocated)).Append("B sysMem=").Append(Display(usedMemory / 1e6, "F0"))
                .Append("MB entities=").Append(entities.ToString(CultureInfo.InvariantCulture));
            if (!live) text.Append(" (world unavailable)");
            if (voxel) text.Append("\nvoxel: ships=").Append(voxels.Ships).Append(" stations=").Append(voxels.Stations).Append(" asteroids=").Append(voxels.Asteroids)
                .Append(" hulkGrids=").Append(voxels.HulkGrids).Append(" bricks=").Append(voxels.Bricks).Append(" solidCells=").Append(voxels.Cells)
                .Append(" dirtyBricks=").Append(voxels.Dirty).Append(" meshDirty=").Append(voxels.MeshDirty).Append(" colliderDirty=").Append(voxels.ColliderDirty)
                .Append(" chunkBodies=").Append(voxels.Chunks).Append(" hulks=").Append(voxels.Hulks);
            if (combat)
            {
                text.Append("\ncombat: projectiles=").Append(projectiles < 0 ? "unavailable" : projectiles.ToString(CultureInfo.InvariantCulture)).Append(" activeBeams=").Append(beams);
                text.Append("\nfx: ").Append(fx == null ? "unavailable" : $"drained={fx.EventsDrained} culled={fx.Culled} budgetDropped={fx.BudgetDropped} debrisLiveEstimate={fx.DebrisLiveEstimate} bound={fx.Bound}");
                text.Append("\naudio: ").Append(audio == null ? "unavailable" : $"drained={audio.EventsDrained} started={audio.Started} activeSfxVoices={audio.ActiveSfxVoices} activeLoopVoices={audio.ActiveLoopVoices} bound={audio.Bound}");
            }
            if (includeMarkers)
            {
                text.Append("\nmarkers (last frame, ms):");
                if (markers.Count == 0) text.Append(" none available");
                foreach (var marker in markers) text.Append('\n').Append(marker.name).Append('=').Append(Display(marker.ms, "F3"));
            }
            return text.ToString();
        }
        void ReleaseQueries()
        {
            if (queryWorld != null && queryWorld.IsCreated) foreach (var query in queries.Values) query.Dispose();
            queries.Clear(); queryWorld = null;
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            ReleaseQueries();
            if (main.Valid) main.Dispose(); if (draws.Valid) draws.Dispose(); if (triangles.Valid) triangles.Dispose();
            if (gc.Valid) gc.Dispose(); if (memory.Valid) memory.Dispose();
            // PerfMarkers owns shared marker recorders; this reader must not stop another diagnostic consumer.
        }
    }
}
#endif
