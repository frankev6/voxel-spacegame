using System;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Transforms;
using Unity.Entities;
using Unity.Mathematics;
using Game.Simulation.Spatial;

namespace Game.Simulation.Weapons
{
    /// <summary>Implements SPEC-21 weapon contract: CombatHitTest.</summary>
    public struct CombatHitTest   // job field; Create in OnCreate, Update first in OnUpdate
    {
        public GridCellReader Reader;
        [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
        [ReadOnly] public ComponentLookup<GridSpawning> Spawning;
        [ReadOnly] public ComponentLookup<GridSource> Sources;
        public static CombatHitTest Create(ref SystemState state) => new CombatHitTest { Reader = GridCellReader.Create(ref state),
            Transforms = state.GetComponentLookup<LocalTransform>(true), Spawning = state.GetComponentLookup<GridSpawning>(true), Sources = state.GetComponentLookup<GridSource>(true) };
        public void Update(ref SystemState state) { Reader.Update(ref state); Transforms.Update(ref state); Spawning.Update(ref state); Sources.Update(ref state); }
        /// <summary>ADR-0053 bounded-range broad phase; current voxel cells remain authoritative.</summary>
        public CombatHit Segment(float3 p0, float3 p1, Entity ignore, NativeList<Entity> grids)
        {
            CombatHit best = default; best.Fraction = float.PositiveInfinity;
            foreach (var grid in grids)
            {
                if (grid == ignore || !Reader.Grids.HasComponent(grid) || !Transforms.HasComponent(grid) ||
                    (Spawning.HasComponent(grid) && Spawning.IsComponentEnabled(grid)) ||
                    (Sources.HasComponent(grid) && (Sources[grid].Flags & GridSpawnFlags.EditorPreview) != 0)) continue;
                var transform = Transforms[grid]; float size = Reader.Grids[grid].CellSize;
                float3 start = transform.InverseTransformPoint(p0), delta = transform.InverseTransformPoint(p1) - start;
                float length = math.length(delta); if (length < 1e-8f) continue;
                var query = new GridCellQuery { Reader = Reader, Grid = grid };
                if (!VoxelRaycast.Cast(ref query, start / size, delta / size, length / size, out var hit)) continue;
                float fraction = hit.DistanceCells * size / length;
                if (fraction > 1 || fraction > best.Fraction || (fraction == best.Fraction && grid.Index >= best.Grid.Index)) continue;
                best = new CombatHit { Kind = HitKind.Hull, Grid = grid, Fraction = fraction, WorldPoint = math.lerp(p0, p1, fraction) };
            }
            return best;
        }
        /// First hit through the spatial-hash broad phase (WPN-008 / AI-002).
        public CombatHit Segment(float3 p0, float3 p1, float inflate, Entity ignore, in SpatialHash hash, int maxCandidates) => throw new NotImplementedException();
    }
}
