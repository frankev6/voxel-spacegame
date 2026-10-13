using Game.Voxel.PhysicsBody;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace Game.Voxel.Damage.Jobs
{
    /// <summary>SPEC-13 §5.5: deterministic X, then Y, then Z greedy coverage of chunk cells.</summary>
    internal static class ChunkBoxBuilder
    {
        internal struct Box { public int3 Min, Size; }

        // Input coordinates are relative to the island AABB minimum. Output owns no collider blobs.
        public static void Build(NativeArray<ChunkCell> cells, int maxBoxes, NativeList<Box> boxes)
        {
            boxes.Clear();
            if (cells.Length == 0) return;
            using var occupied = new NativeHashSet<int3>(cells.Length, Allocator.Temp);
            int3 min = new int3(255), max = 0;
            foreach (var cell in cells)
            {
                int3 p = new int3(cell.X, cell.Y, cell.Z);
                occupied.Add(p); min = math.min(min, p); max = math.max(max, p);
            }
            for (int z = min.z; z <= max.z; z++)
            for (int y = min.y; y <= max.y; y++)
            for (int x = min.x; x <= max.x; x++)
            {
                int3 start = new int3(x, y, z);
                if (!occupied.Contains(start)) continue;
                int3 size = new int3(1);
                while (x + size.x <= max.x && occupied.Contains(start + new int3(size.x, 0, 0))) size.x++;
                while (y + size.y <= max.y && LayerPresent(occupied, start, size, 1)) size.y++;
                while (z + size.z <= max.z && LayerPresent(occupied, start, size, 2)) size.z++;
                boxes.Add(new Box { Min = start, Size = size });
                if (boxes.Length > maxBoxes)
                {
                    boxes.Clear(); boxes.Add(new Box { Min = min, Size = max - min + 1 }); return;
                }
                for (int dz = 0; dz < size.z; dz++)
                for (int dy = 0; dy < size.y; dy++)
                for (int dx = 0; dx < size.x; dx++) occupied.Remove(start + new int3(dx, dy, dz));
            }
        }

        static bool LayerPresent(NativeHashSet<int3> occupied, int3 start, int3 size, int axis)
        {
            for (int y = 0; y < (axis == 1 ? 1 : size.y); y++)
            for (int x = 0; x < size.x; x++)
                if (!occupied.Contains(start + new int3(x, axis == 1 ? size.y : y, axis == 2 ? size.z : 0))) return false;
            return true;
        }

        public static BlobAssetReference<Collider> CreateCollider(NativeArray<ChunkCell> cells, float cellSize,
            float3 localOrigin, int maxBoxes)
        {
            using var boxes = new NativeList<Box>(Allocator.Temp);
            Build(cells, maxBoxes, boxes);
            using var children = new NativeArray<CompoundCollider.ColliderBlobInstance>(boxes.Length, Allocator.Temp);
            for (int i = 0; i < boxes.Length; i++)
            {
                var box = boxes[i];
                var collider = BoxCollider.Create(new BoxGeometry
                {
                    Center = localOrigin + ((float3)box.Min + (float3)box.Size * .5f) * cellSize,
                    Size = (float3)box.Size * cellSize, Orientation = quaternion.identity, BevelRadius = 0
                }, PhysicsLayers.ChunkBody, PhysicsLayers.ChunkMaterial);
                children[i] = new CompoundCollider.ColliderBlobInstance { Collider = collider, CompoundFromChild = RigidTransform.identity };
            }
            var result = CompoundCollider.Create(children);
            // Compound creation copies its children; only the returned compound survives the job.
            for (int i = 0; i < children.Length; i++) { var child = children[i].Collider; child.Dispose(); }
            return result;
        }
    }
}
