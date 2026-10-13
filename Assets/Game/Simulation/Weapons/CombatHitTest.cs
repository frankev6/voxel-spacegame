using System.Threading;
using Game.Core;
using Game.Simulation.Power;
using Unity.Burst;
using UnityEngine;
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
        struct CandidateWarningContext { }
        static readonly SharedStatic<int> candidateWarning = SharedStatic<int>.GetOrCreate<CandidateWarningContext>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetWarnings() => candidateWarning.Data = 0;
        public GridCellReader Reader;
        [ReadOnly] public ComponentLookup<ShieldState> Shields;
        [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
        [ReadOnly] public ComponentLookup<GridSpawning> Spawning;
        [ReadOnly] public ComponentLookup<GridSource> Sources;
        public static CombatHitTest Create(ref SystemState state) => new CombatHitTest { Reader = GridCellReader.Create(ref state),
            Shields = state.GetComponentLookup<ShieldState>(true), Transforms = state.GetComponentLookup<LocalTransform>(true), Spawning = state.GetComponentLookup<GridSpawning>(true), Sources = state.GetComponentLookup<GridSource>(true) };
        public void Update(ref SystemState state) { Reader.Update(ref state); Shields.Update(ref state); Transforms.Update(ref state); Spawning.Update(ref state); Sources.Update(ref state); }
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
                float3 normal = -math.normalizesafe(delta, new float3(0, 1, 0));
                if (hit.EnteredFace < 6) { normal = float3.zero; normal[hit.EnteredFace / 2] = (hit.EnteredFace & 1) == 0 ? 1 : -1; }
                best = new CombatHit { Kind = HitKind.Hull, Grid = grid, Fraction = fraction, WorldPoint = math.lerp(p0, p1, fraction), WorldNormal = math.rotate(transform.Rotation, normal) };
            }
            return best;
        }
        /// First hit through the spatial-hash broad phase (WPN-008 / AI-002).
        public CombatHit Segment(float3 p0, float3 p1, float inflate, Entity ignore, in SpatialHash hash, int maxCandidates)
            => Segment(p0, p1, inflate, ignore, hash.AsReadOnly(), maxCandidates);

        /// <summary>ADR-0064 job-facing overload; only spatial data enter the job.</summary>
        public CombatHit Segment(float3 p0, float3 p1, float inflate, Entity ignore, in SpatialHashQueryData hash, int maxCandidates)
        {
            var indices = new FixedList512Bytes<int>();
            int total = hash.QuerySegment(p0, p1, inflate, ref indices);
            int limit = math.min(indices.Capacity, math.max(0, maxCandidates));
            if (total > limit && Interlocked.Exchange(ref candidateWarning.Data, 1) == 0)
            {
                FixedString128Bytes warning = "Weapons: hit-test candidates truncated in Entity.Index order";
                GameLog.Burst.Warn(warning);
            }
            CombatHit best = default; best.Fraction = float.PositiveInfinity;
            for (int i = 0; i < math.min(indices.Length, limit); i++)
            {
                var proxy = hash.Get(indices[i]); Entity grid = proxy.Entity;
                if (grid == ignore) continue;
                if (proxy.Kind == ProxyKind.Celestial)
                {
                    if (SphereEntry(p0, p1, proxy.Center, proxy.HullRadius, out float fraction)
                        && Earlier(fraction, grid, best))
                    {
                        float3 point = math.lerp(p0, p1, fraction);
                        best = new CombatHit { Kind = HitKind.Celestial, Grid = grid, Fraction = fraction,
                            WorldPoint = point, WorldNormal = math.normalizesafe(point - proxy.Center, -math.normalizesafe(p1 - p0)) };
                    }
                    continue;
                }
                if (!Reader.Grids.HasComponent(grid) || !Transforms.HasComponent(grid)
                    || (Spawning.HasComponent(grid) && Spawning.IsComponentEnabled(grid))
                    || (Sources.HasComponent(grid) && (Sources[grid].Flags & GridSpawnFlags.EditorPreview) != 0)) continue;
                var transform = Transforms[grid]; float size = Reader.Grids[grid].CellSize;
                float3 a = transform.InverseTransformPoint(p0), b = transform.InverseTransformPoint(p1);
                float3 delta = b - a; float length = math.length(delta);
                float shieldFraction = float.PositiveInfinity; float3 shieldNormal = default;
                if (Shields.HasComponent(grid))
                {
                    var shield = Shields[grid];
                    if (shield.Active == 1 && ShieldMath.SegmentEnters(a, b, shield.Center, shield.Radii, out float f))
                    {
                        shieldFraction = f;
                        shieldNormal = math.rotate(transform.Rotation, ShieldMath.Normal(math.lerp(a, b, f), shield.Center, shield.Radii));
                    }
                }
                float hullFraction = float.PositiveInfinity; float3 hullNormal = default;
                if (length > 1e-8f)
                {
                    var query = new GridCellQuery { Reader = Reader, Grid = grid };
                    if (VoxelRaycast.Cast(ref query, a / size, delta / size, length / size, out var hit))
                    {
                        hullFraction = hit.DistanceCells * size / length;
                        float3 normal = -math.normalizesafe(delta);
                        if (hit.EnteredFace < 6) { normal = float3.zero; normal[hit.EnteredFace / 2] = (hit.EnteredFace & 1) == 0 ? 1 : -1; }
                        hullNormal = math.rotate(transform.Rotation, normal);
                    }
                }
                bool useShield = shieldFraction <= hullFraction;
                float selected = useShield ? shieldFraction : hullFraction;
                if (selected > 1 || !Earlier(selected, grid, best)) continue;
                best = new CombatHit { Kind = useShield ? HitKind.Shield : HitKind.Hull, Grid = grid,
                    Fraction = selected, WorldPoint = math.lerp(p0, p1, selected),
                    ShieldLocalPoint = useShield ? math.lerp(a, b, selected) : default,
                    WorldNormal = useShield ? shieldNormal : hullNormal };
            }
            return best;
        }
        static bool Earlier(float fraction, Entity entity, in CombatHit best)
            => fraction < best.Fraction || (fraction == best.Fraction && entity.Index < best.Grid.Index);
        static bool SphereEntry(float3 p0, float3 p1, float3 center, float radius, out float fraction)
        {
            fraction = 0;
            double3 offset = (double3)p0 - center, delta = (double3)p1 - p0;
            double c = math.dot(offset, offset) - (double)radius * radius;
            if (c <= 0) return true;
            double a = math.dot(delta, delta); if (a == 0) return false;
            double b = math.dot(offset, delta), discriminant = b * b - a * c;
            if (discriminant < 0) return false;
            double entry = (-b - math.sqrt(discriminant)) / a;
            if (entry < 0 || entry > 1) return false;
            fraction = (float)entry; return true;
        }
    }
}
