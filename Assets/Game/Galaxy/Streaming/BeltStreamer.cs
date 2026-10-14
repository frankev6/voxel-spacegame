using System;
using System.Collections.Generic;
using Game.Data.Definitions;
using Game.Galaxy.Bodies;
using Game.Galaxy.Data;
using Game.Galaxy.Services;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;

namespace Game.Galaxy.Streaming
{
    /// <summary>SPEC-31 §5.5: bounded asynchronous sectors, shape jobs and canonical dynamic grid lifetimes.</summary>
    internal sealed class BeltStreamer : IDisposable
    {
        static readonly ProfilerMarker TickMarker = new ProfilerMarker(ProfilerCategory.Scripts, "StarSystemLoader.T3");
        sealed class SectorState
        {
            internal BodySpec Belt;
            internal BodyTemplateDefinition Template;
            internal double3 Centre;
            internal NativeList<AsteroidSpawnSpec> Descriptors;
            internal JobHandle Generation;
            internal bool Generating, Finished, Retiring;
            internal int Next;
            internal AsteroidBuilder.PendingShape Shape;
            internal readonly List<AsteroidGridSpawner.Lease> Grids = new();
        }
        readonly GalaxyService galaxy;
        readonly StarSystemDelta delta;
        readonly AsteroidBuilder shapes;
        readonly AsteroidGridSpawner spawner;
        readonly SortedDictionary<(ulong Body, int Sector), SectorState> sectors = new();
        readonly List<(ulong Body, int Sector)> removed = new();
        readonly List<int> wanted = new();
        bool closing;
        internal BeltStreamer(GalaxyService galaxy, StarSystemDelta delta, AsteroidBuilder shapes, AsteroidGridSpawner spawner)
        { this.galaxy = galaxy; this.delta = delta; this.shapes = shapes; this.spawner = spawner; }

        internal void Tick(StarSystemContent content, double3 player, double3 origin, in StreamingConfig config, ref int spawnBudget)
        {
            using var marker = TickMarker.Auto();
            DrainRetired();
            if (closing) return;
            double unload = config.BubbleRadiusM * (double)config.BubbleUnloadFactor;
            foreach (var pair in sectors)
                if (!pair.Value.Retiring && math.distancesq(player, pair.Value.Centre) > unload * unload) Retire(pair.Value);

            int jobs = 0;
            foreach (var sector in sectors.Values)
            { if (sector.Generating) jobs++; if (sector.Shape != null) jobs++; }
            foreach (var sector in sectors.Values)
            {
                if (sector.Retiring || sector.Finished) continue;
                if (sector.Generating)
                {
                    if (!sector.Generation.IsCompleted) continue;
                    sector.Generation.Complete(); sector.Generating = false; jobs--;
                }
                if (sector.Shape != null)
                {
                    var descriptor = sector.Descriptors[sector.Next];
                    bool destroyed = IsDestroyed(descriptor.Id.LocalKey);
                    if (!sector.Shape.IsCompleted || !destroyed && spawnBudget <= 0) continue;
                    if (!sector.Shape.TryTake(out var source)) continue;
                    sector.Shape = null; jobs--;
                    if (destroyed) source.Dispose();
                    else
                    {
                        // Spawn consumes the source on both success and failure.
                        sector.Grids.Add(spawner.Spawn(descriptor, sector.Belt.TemplateIndex, source, origin));
                        spawnBudget--;
                    }
                    sector.Next++;
                }
                while (sector.Next < sector.Descriptors.Length && IsDestroyed(sector.Descriptors[sector.Next].Id.LocalKey)) sector.Next++;
                if (sector.Next == sector.Descriptors.Length)
                {
                    sector.Descriptors.Dispose(); sector.Finished = true;
                    continue;
                }
                if (jobs >= config.MaxAsteroidJobsInFlight) continue;
                var next = sector.Descriptors[sector.Next];
                sector.Shape = shapes.Schedule(next.RadiusM, .5f, sector.Template.AsteroidBlock,
                    sector.Template.AsteroidNoiseAmplitude, sector.Template.AsteroidNoiseFrequency, next.ShapeSeed);
                jobs++;
            }

            foreach (var belt in content.Bodies)
            {
                if (belt.Id.Role != BodyRole.Belt || (belt.State & BodyStateFlags.Destroyed) != 0) continue;
                var template = galaxy.GetBodyTemplate(belt.TemplateIndex);
                BeltSectorGeometry.CollectWanted(belt, template.SectorSizeM, player, config.BubbleRadiusM, wanted);
                foreach (int index in wanted)
                {
                    if (jobs >= config.MaxAsteroidJobsInFlight) return;
                    var key = (belt.Id.LocalKey, index);
                    if (sectors.ContainsKey(key)) continue; // Includes retiring sectors until all borrows are released.
                    var sector = new SectorState { Belt = belt, Template = template,
                        Centre = BeltSectorGeometry.Centre(belt, template.SectorSizeM, index),
                        Descriptors = new NativeList<AsteroidSpawnSpec>(8, Allocator.Persistent) };
                    try
                    {
                        int maximum = math.min(template.AsteroidsPerSectorMax, config.MaxAsteroidsPerSector);
                        sector.Generation = new AsteroidSectorJob { Belt = belt, Sector = index,
                            MinCount = math.min(template.AsteroidsPerSectorMin, maximum), MaxCount = maximum,
                            SectorSizeM = template.SectorSizeM, MinRadiusM = template.AsteroidRadiusMinM,
                            MaxRadiusM = template.AsteroidRadiusMaxM, Output = sector.Descriptors }.Schedule();
                        sector.Generating = true;
                        sectors.Add(key, sector); jobs++;
                    }
                    catch { sector.Generation.Complete(); sector.Descriptors.Dispose(); throw; }
                }
            }
        }
        bool IsDestroyed(ulong key)
        {
            var values = delta.Destroyed;
            int low = 0, high = values.Count - 1;
            while (low <= high)
            {
                int middle = low + (high - low) / 2;
                ulong value = values[middle];
                if (value == key) return true;
                if (value < key) low = middle + 1; else high = middle - 1;
            }
            return false;
        }
        void Retire(SectorState sector)
        {
            sector.Retiring = true;
            foreach (var grid in sector.Grids) spawner.Retire(grid);
        }
        internal void BeginRetirement()
        {
            if (closing) return;
            closing = true;
            foreach (var sector in sectors.Values) if (!sector.Retiring) Retire(sector);
        }
        internal bool DrainRetired()
        {
            removed.Clear();
            foreach (var pair in sectors)
            {
                var sector = pair.Value;
                if (!sector.Retiring) continue;
                if (sector.Generating)
                {
                    if (!sector.Generation.IsCompleted) continue;
                    sector.Generation.Complete(); sector.Generating = false;
                }
                if (sector.Shape != null)
                {
                    if (!sector.Shape.IsCompleted) continue;
                    sector.Shape.Dispose(); sector.Shape = null;
                }
                if (sector.Descriptors.IsCreated) sector.Descriptors.Dispose();
                for (int i = sector.Grids.Count - 1; i >= 0; i--)
                    if (spawner.TryRelease(sector.Grids[i])) sector.Grids.RemoveAt(i);
                if (sector.Grids.Count == 0) removed.Add(pair.Key);
            }
            foreach (var key in removed) sectors.Remove(key);
            return closing && sectors.Count == 0;
        }
        // Only scene/system shutdown uses this join. Ordinary distance retirement never waits for a running fill.
        internal void CompleteJobs()
        {
            foreach (var sector in sectors.Values)
            {
                if (sector.Generating) sector.Generation.Complete();
                sector.Shape?.Wait();
            }
        }
        public void Dispose()
        {
            BeginRetirement(); CompleteJobs();
            if (!DrainRetired()) throw new InvalidOperationException("Asteroid grids must retire before their generated sources.");
            spawner.Dispose();
        }
    }
}
