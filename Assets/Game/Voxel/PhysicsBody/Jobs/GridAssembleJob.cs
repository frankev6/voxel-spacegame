using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>Copies cached children and overlays this batch's ordered results before compound creation (SPEC-12 §5.8).</summary>
    [BurstCompile]
    internal struct GridAssembleJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<AssemblyItem> Items;
        [ReadOnly] public NativeArray<BrickChild> Children;
        [ReadOnly] public NativeArray<int> Apply;
        [ReadOnly] public NativeArray<BrickBuildItem> Bricks;
        [ReadOnly] public NativeArray<BrickBuildResult> BrickResults;
        public NativeArray<AssemblyResult> Results;
        public void Execute(int index)
        {
            var item = Items[index]; var children = new NativeList<BrickChild>(item.ChildCount + item.ApplyCount, Allocator.Temp);
            try
            {
                for (int i = 0; i < item.ChildCount; i++) children.Add(Children[item.ChildStart + i]);
                for (int i = 0; i < item.ApplyCount; i++)
                {
                    int b = Apply[item.ApplyStart + i]; var brick = Bricks[b]; var result = BrickResults[b];
                    int low = 0, high = children.Length;
                    while (low < high)
                    { int mid = low + (high - low) / 2; if (GridColliderAssembler.CompareCoords(children[mid].Coord, brick.Coord) < 0) low = mid + 1; else high = mid; }
                    bool exists = low < children.Length && GridColliderAssembler.CompareCoords(children[low].Coord, brick.Coord) == 0;
                    if (result.Removed != 0 || !result.Blob.IsCreated) { if (exists) children.RemoveAt(low); continue; }
                    if (!exists) { children.Add(default); for (int j = children.Length - 1; j > low; j--) children[j] = children[j - 1]; }
                    children[low] = new BrickChild { Coord = brick.Coord, Blob = result.Blob, BoxCount = result.BoxCount };
                }
                int boxes = 0; for (int i = 0; i < children.Length; i++) boxes += children[i].BoxCount;
                Results[index] = new AssemblyResult
                {
                    Compound = children.Length == 0 ? default : GridColliderAssembler.Assemble(children.AsArray(), item.CellSize),
                    ChildCount = children.Length, BoxCount = boxes,
                };
            }
            finally { children.Dispose(); }
        }
    }
}
