using System;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.3. Managed owner of one structure's native storage; behaviour in §5.1–§5.4.
    /// ComputeContentHash (§5.4) is added by VOX-009 together with the SPEC-05 VOXS types it needs (SPEC-10 open question 14).</summary>
    public sealed class VoxelStructure : IDisposable
    {
        public VoxelStructure(float cellSize = 0.5f) { throw new System.NotImplementedException("VOX-005"); }            // Allocator.Persistent; Palette gets entry 0
        public VoxelStructureData Data => throw new NotImplementedException("VOX-005");
        public float CellSize => throw new NotImplementedException("VOX-005");
        public uint Revision => throw new NotImplementedException("VOX-005");
        public bool IsReadOnly => throw new NotImplementedException("VOX-005");
        public bool IsPinned => throw new NotImplementedException("VOX-005");
        public int BrickCount => throw new NotImplementedException("VOX-005");
        public int SolidCellCount => throw new NotImplementedException("VOX-005");   // Σ SolidCount, O(bricks)
        public bool TryGetCellBounds(out int3 min, out int3 max) { throw new System.NotImplementedException("VOX-005"); }
        public Cell GetCell(int3 cell) { throw new System.NotImplementedException("VOX-005"); }
        public void SetCell(int3 cell, Cell value) { throw new System.NotImplementedException("VOX-005"); }              // raw write (codec, editor internals, tests)
        public PlaceResult TryPlaceBlock(in BlockPlacement p, in VoxelContent content) { throw new System.NotImplementedException("VOX-006"); }
        public bool TryRemoveBlockAt(int3 cell, in VoxelContent content, out BlockPlacement removed) { throw new System.NotImplementedException("VOX-006"); }
        public bool TryGetBlockAt(int3 cell, out BlockPlacement block, out int instanceIndex) { throw new System.NotImplementedException("VOX-006"); }
        public ushort FindOrAddPalette(PaletteEntry e) { throw new System.NotImplementedException("VOX-006"); }          // returns 0 if full (32767 entries used)
        public void SetBrickDense(int3 coord, NativeArray<Cell> cells4096) { throw new System.NotImplementedException("VOX-005"); }   // replaces/creates a brick; deletes it if all air
        public void SetBrickUniform(int3 coord, Cell value) { throw new System.NotImplementedException("VOX-005"); }     // value must be solid
        public bool RemoveBrick(int3 coord) { throw new System.NotImplementedException("VOX-005"); }
        public void OptimizeStorage() { throw new System.NotImplementedException("VOX-005"); }                           // full identical dense bricks → uniform; frees pages
        public void Compact() { throw new System.NotImplementedException("VOX-006"); }                                   // §5.3
        public void SetReadOnly() { throw new System.NotImplementedException("VOX-005"); }                               // irreversible
        public void Pin() { throw new System.NotImplementedException("VOX-005"); } public void Unpin() { throw new System.NotImplementedException("VOX-005"); }   // PinCount ±1 (Unpin below 0 throws)
        public NativeArray<int> SortedBrickOrder(Allocator a) { throw new System.NotImplementedException("VOX-005"); }   // indices into Bricks sorted by (z, y, x)
        public void GetBlocks(NativeList<StructureBlock> output) { throw new System.NotImplementedException("VOX-007"); }// §5.4 canonical order
        public VoxelStructure Clone() { throw new System.NotImplementedException("VOX-007"); }                           // deep copy; not pinned, not read-only
        public static StructureDiff Diff(VoxelStructure from, VoxelStructure to, Allocator a) { throw new System.NotImplementedException("VOX-007"); }
        public StructureMassData GetMass(in VoxelContent content) { throw new System.NotImplementedException("VOX-007"); }   // cached by Revision
        public long NativeBytes => throw new NotImplementedException("VOX-005");     // Σ capacities × element sizes of all containers
        public void Clear() { throw new System.NotImplementedException("VOX-005"); }
        public void Dispose() { throw new System.NotImplementedException("VOX-005"); }                                   // throws InvalidOperationException if pinned; second call is a no-op
    }
}
