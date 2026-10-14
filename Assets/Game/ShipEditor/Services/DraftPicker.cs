using System;
using Game.Data.Blocks;
using Game.Voxel.Model;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Game.ShipEditor.Services
{
    /// <summary>SPEC-51 §5.10: bounded grid-cell DDA; borrows the current draft only for this call.</summary>
    public static class DraftPicker
    {
        public enum HitKind : byte { None, Block, Plane }
        public readonly struct Hit
        {
            public readonly HitKind Kind;
            public readonly int3 Cell, Normal;
            public readonly float Distance;
            public int3 PlacementCell => Kind == HitKind.Block ? Cell + Normal : Cell;
            internal Hit(HitKind kind, int3 cell, int3 normal, double distance)
            { Kind = kind; Cell = cell; Normal = normal; Distance = (float)distance; }
        }
        const int MaxSteps = 4096;
        static readonly ProfilerMarker Marker = new("DraftPicker.Raycast");
        public static Hit Raycast(VoxelStructure draft, EditorContext context, Ray gridRay,
            bool sliceEnabled = false, int sliceLevel = 0, bool xRay = false)
        {
            if (draft == null) throw new ArgumentNullException(nameof(draft));
            if (context == null) throw new ArgumentNullException(nameof(context));
            using var marker = Marker.Auto();
            double3 origin = (float3)gridRay.origin, direction = (float3)gridRay.direction;
            if (!math.all(math.isfinite(origin)) || !math.all(math.isfinite(direction))) return default;
            double length = math.length(direction); if (length == 0) return default;
            direction /= length;
            bool hasBounds = draft.TryGetCellBounds(out var min, out var max) && math.all(min <= max);
            double3 boxMin = hasBounds ? (double3)min - 1 : new double3(-16);
            double3 boxMax = hasBounds ? (double3)max + 2 : new double3(16);
            if (Intersect(origin, direction, boxMin, boxMax, out double enter, out double exit, out int3 normal))
            {
                double distance = math.max(0, enter);
                double3 point = origin + direction * distance;
                int3 cell = (int3)math.floor(point), step = (int3)math.sign(direction);
                double3 delta = math.abs(1 / direction), next = new(double.PositiveInfinity);
                if (enter < 0) normal = OppositeDominant(direction, step);
                for (int axis = 0; axis < 3; axis++)
                {
                    // A negative ray on an exact boundary enters the cell below that boundary.
                    if (step[axis] < 0 && point[axis] == math.floor(point[axis])) cell[axis]--;
                    if (step[axis] != 0)
                        next[axis] = ((double)cell[axis] + (step[axis] > 0 ? 1 : 0) - origin[axis]) / direction[axis];
                }
                int limit = math.clamp(context.Config.MaxPickSteps, 1, MaxSteps);
                for (int visited = 0; visited < limit && distance <= exit; visited++)
                {
                    if ((!sliceEnabled || cell.y <= sliceLevel) && !draft.GetCell(cell).IsAir &&
                        (!xRay || draft.TryGetBlockAt(cell, out var placed, out _) &&
                            context.Definitions.Value.Blocks[placed.Block].Kind != BlockKind.Structural))
                        return new Hit(HitKind.Block, cell, normal, distance);
                    // Match the canonical voxel DDA's X/Y/Z boundary tie order.
                    int axis = next.x <= next.y && next.x <= next.z ? 0 : next.y <= next.z ? 1 : 2;
                    distance = next[axis]; next[axis] += delta[axis]; cell[axis] += step[axis];
                    normal = default; normal[axis] = -step[axis];
                }
            }
            int plane = sliceEnabled ? sliceLevel : hasBounds ? min.y : 0;
            if (direction.y == 0) return default;
            double planeDistance = (plane - origin.y) / direction.y;
            if (planeDistance < 0 || planeDistance > MaxSteps) return default;
            double3 planePoint = math.floor(origin + direction * planeDistance);
            if (math.any(planePoint < int.MinValue) || math.any(planePoint > int.MaxValue)) return default;
            var planeCell = (int3)planePoint; planeCell.y = plane;
            return new Hit(HitKind.Plane, planeCell, new int3(0, 1, 0), planeDistance);
        }
        static int3 OppositeDominant(double3 direction, int3 step)
        {
            double3 magnitude = math.abs(direction);
            int axis = magnitude.x >= magnitude.y && magnitude.x >= magnitude.z ? 0 : magnitude.y >= magnitude.z ? 1 : 2;
            int3 normal = default; normal[axis] = -step[axis]; return normal;
        }
        static bool Intersect(double3 origin, double3 direction, double3 min, double3 max,
            out double enter, out double exit, out int3 normal)
        {
            enter = double.NegativeInfinity; exit = double.PositiveInfinity; normal = default;
            for (int axis = 0; axis < 3; axis++)
            {
                if (direction[axis] == 0)
                { if (origin[axis] < min[axis] || origin[axis] >= max[axis]) return false; continue; }
                double a = (min[axis] - origin[axis]) / direction[axis];
                double b = (max[axis] - origin[axis]) / direction[axis];
                double near = math.min(a, b), far = math.max(a, b);
                if (near > enter)
                { enter = near; normal = default; normal[axis] = direction[axis] > 0 ? -1 : 1; }
                exit = math.min(exit, far); if (enter > exit) return false;
            }
            return exit >= math.max(0, enter);
        }
    }
}
