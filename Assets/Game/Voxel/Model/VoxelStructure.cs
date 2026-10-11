using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System.Collections.Generic;
using Game.Data.Blocks;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.3. Managed owner of one structure's native storage; behaviour in §5.1–§5.4.
    /// VOXS content identity uses the canonical SPEC-05 payload (§5.4).</summary>
    public sealed class VoxelStructure : IDisposable
    {
        VoxelStructureData data;

        public VoxelStructure(float cellSize = 0.5f)
        {
            if (!math.isfinite(cellSize) || cellSize <= 0) throw new ArgumentOutOfRangeException(nameof(cellSize));
            try
            {
                data.Header = new NativeReference<StructureHeader>(Allocator.Persistent);
                data.Header.Value = new StructureHeader { CellSize = cellSize };
                data.Palette = new NativeList<PaletteEntry>(Allocator.Persistent);
                data.Palette.Add(default);
                data.Instances = new NativeList<BlockInstance>(Allocator.Persistent);
                data.FreeInstances = new NativeList<int>(Allocator.Persistent);
                data.Bricks = new NativeList<BrickRecord>(Allocator.Persistent);
                data.Masks = new NativeList<BrickMaskBits>(Allocator.Persistent);
                data.BrickIndex = new NativeHashMap<int3, int>(0, Allocator.Persistent);
                data.CellPages = new NativeList<Cell>(Allocator.Persistent);
                data.FreePages = new NativeList<int>(Allocator.Persistent);
            }
            catch { DisposeStorage(); throw; }
        }
        public VoxelStructureData Data => data;
        public float CellSize => data.Header.Value.CellSize;
        public uint Revision => data.Header.Value.Revision;
        public bool IsReadOnly => data.Header.Value.ReadOnly != 0;
        public bool IsPinned => data.Header.Value.PinCount != 0;
        public int BrickCount => data.Bricks.Length;
        public int SolidCellCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < data.Bricks.Length; i++) count += data.Bricks[i].SolidCount;
                return count;
            }
        }
        public bool TryGetCellBounds(out int3 min, out int3 max)
        {
            var header = data.Header.Value;
            if (header.BoundsValid == 0)
            {
                min = new int3(int.MaxValue); max = new int3(int.MinValue);
                for (int i = 0; i < data.Bricks.Length; i++)
                    if (data.Masks[i].TryGetBounds(out var lo, out var hi))
                    {
                        int3 origin = data.Bricks[i].Coord << BrickMath.Shift;
                        min = math.min(min, origin + lo); max = math.max(max, origin + hi);
                    }
                if (data.Bricks.Length == 0) min = max = default;
                header.CellMin = min; header.CellMax = max; header.BoundsValid = 1;
                data.Header.Value = header;
            }
            min = header.CellMin; max = header.CellMax;
            return data.Bricks.Length != 0;
        }
        public Cell GetCell(int3 cell)
        {
            if (!data.BrickIndex.TryGetValue(BrickMath.BrickOf(cell), out int index)) return default;
            var record = data.Bricks[index];
            return record.Page < 0 ? record.Uniform : data.CellPages[record.Page * BrickMath.CellsPerBrick + BrickMath.LocalIndex(BrickMath.LocalOf(cell))];
        }
        public void SetCell(int3 cell, Cell value)
        {
            RequireMutable();
            if (value.IsAir) value = default;
            int3 coord = BrickMath.BrickOf(cell);
            int local = BrickMath.LocalIndex(BrickMath.LocalOf(cell));
            bool exists = data.BrickIndex.TryGetValue(coord, out int index);
            var record = exists ? data.Bricks[index] : new BrickRecord { Coord = coord, Page = -1 };
            Cell old = !exists ? default : record.Page < 0 ? record.Uniform : data.CellPages[record.Page * BrickMath.CellsPerBrick + local];
            if (old.Equals(value)) return;
            if (record.Page < 0)
            {
                record.Page = TakePage();
                FillPage(record.Page, record.Uniform);
                record.Uniform = default;
            }
            if (!exists)
            {
                index = data.Bricks.Length;
                data.Bricks.Add(record); data.Masks.Add(default); data.BrickIndex.Add(coord, index);
            }
            data.CellPages[record.Page * BrickMath.CellsPerBrick + local] = value;
            var mask = data.Masks[index]; mask.Set(local, !value.IsAir); data.Masks[index] = mask;
            record.SolidCount = (ushort)(record.SolidCount + (value.IsAir ? 0 : 1) - (old.IsAir ? 0 : 1));
            record.ChangedRevision = Changed();
            data.Bricks[index] = record;
            if (record.SolidCount == 0) RemoveAt(index);
        }
        public PlaceResult TryPlaceBlock(in BlockPlacement p, in VoxelContent content)
        {
            RequireUnpinned();
            if (IsReadOnly) return PlaceResult.ReadOnly;
            if (p.Block == 0 || p.Block >= content.Blocks.Value.Blocks.Length) return PlaceResult.InvalidBlock;
            ref var def = ref content.Blocks.Value.Blocks[p.Block];
            if (p.Orient >= 24 || (def.AllowedOrientations & (1u << p.Orient)) == 0) return PlaceResult.OrientationNotAllowed;
            using var footprint = new NativeList<int3>(def.Cells.Length, Allocator.Temp);
            BlockPlacementMath.GetCells(ref def, ref content.Orientations.Value, p.Orient, p.Origin, footprint);
            for (int i = 0; i < footprint.Length; i++)
                if (math.any(footprint[i] < -1048576) || math.any(footprint[i] > 1048575)) return PlaceResult.OutOfRange;
            for (int i = 0; i < footprint.Length; i++) if (!GetCell(footprint[i]).IsAir) return PlaceResult.Occupied;
            if (!def.RequiresInstance)
            {
                ushort palette = FindOrAddPalette(new PaletteEntry { Block = p.Block, Orient = p.Orient, Color = p.Color });
                if (palette == 0) return PlaceResult.PaletteFull;
                SetCell(p.Origin, Cell.Palette(palette));
            }
            else
            {
                int slot;
                if (data.FreeInstances.Length != 0)
                {
                    int last = data.FreeInstances.Length - 1;
                    slot = data.FreeInstances[last]; data.FreeInstances.RemoveAt(last);
                }
                else
                {
                    if (data.Instances.Length > Cell.MaxInstanceIndex) return PlaceResult.InstancesFull;
                    slot = data.Instances.Length; data.Instances.Add(default);
                }
                data.Instances[slot] = new BlockInstance
                {
                    Block = p.Block, Orient = p.Orient, Color = p.Color, Origin = p.Origin,
                    FunctionHp = def.FunctionHp, CellsAlive = (ushort)footprint.Length
                };
                for (int i = 0; i < footprint.Length; i++)
                    SetCell(footprint[i], Cell.Instance(slot, 255, i == 0 ? (byte)CellFlags.InstanceRoot : (byte)0));
            }
            return PlaceResult.Ok;
        }
        public bool TryRemoveBlockAt(int3 cell, in VoxelContent content, out BlockPlacement removed)
        {
            RequireMutable();
            if (!TryGetBlockAt(cell, out removed, out int instanceIndex)) return false;
            if (instanceIndex < 0) SetCell(cell, default);
            else
            {
                ref var def = ref content.Blocks.Value.Blocks[removed.Block];
                using var footprint = new NativeList<int3>(def.Cells.Length, Allocator.Temp);
                BlockPlacementMath.GetCells(ref def, ref content.Orientations.Value, removed.Orient, removed.Origin, footprint);
                for (int i = 0; i < footprint.Length; i++)
                {
                    Cell value = GetCell(footprint[i]);
                    if (value.IsInstance && value.InstanceIndex == instanceIndex) SetCell(footprint[i], default);
                }
                data.Instances[instanceIndex] = default; data.FreeInstances.Add(instanceIndex);
            }
            return true;
        }
        public bool TryGetBlockAt(int3 cell, out BlockPlacement block, out int instanceIndex)
        {
            Cell value = GetCell(cell); block = default; instanceIndex = -1;
            if (value.IsAir) return false;
            if (value.IsInstance)
            {
                instanceIndex = value.InstanceIndex;
                var instance = data.Instances[instanceIndex];
                block = new BlockPlacement { Block = instance.Block, Origin = instance.Origin, Orient = instance.Orient, Color = instance.Color };
            }
            else
            {
                var entry = data.Palette[value.PaletteIndex];
                block = new BlockPlacement { Block = entry.Block, Origin = cell, Orient = entry.Orient, Color = entry.Color };
            }
            return true;
        }
        public ushort FindOrAddPalette(PaletteEntry e)
        {
            RequireMutable();
            for (int i = 1; i < data.Palette.Length; i++)
            {
                var entry = data.Palette[i];
                if (entry.Block == e.Block && entry.Orient == e.Orient && entry.Color == e.Color) return (ushort)i;
            }
            if (data.Palette.Length > Cell.MaxPaletteIndex) return 0;
            ushort index = (ushort)data.Palette.Length; data.Palette.Add(e); Changed(); return index;
        }
        public void SetBrickDense(int3 coord, NativeArray<Cell> cells4096)
        {
            RequireMutable();
            if (cells4096.Length != BrickMath.CellsPerBrick) throw new ArgumentException("A brick must contain 4096 cells", nameof(cells4096));
            var mask = new BrickMaskBits();
            int count = 0;
            for (int i = 0; i < cells4096.Length; i++)
                if (!cells4096[i].IsAir) { mask.Set(i, true); count++; }
            if (count == 0) { RemoveBrick(coord); return; }
            bool exists = data.BrickIndex.TryGetValue(coord, out int index);
            var record = exists ? data.Bricks[index] : new BrickRecord { Coord = coord, Page = -1 };
            if (exists)
            {
                bool same = true;
                for (int i = 0; i < cells4096.Length && same; i++)
                {
                    Cell old = record.Page < 0 ? record.Uniform : data.CellPages[record.Page * BrickMath.CellsPerBrick + i];
                    same = old.Equals(cells4096[i].IsAir ? default : cells4096[i]);
                }
                if (same) return;
            }
            if (record.Page < 0) record.Page = TakePage();
            for (int i = 0; i < cells4096.Length; i++)
                data.CellPages[record.Page * BrickMath.CellsPerBrick + i] = cells4096[i].IsAir ? default : cells4096[i];
            record.Uniform = default; record.SolidCount = (ushort)count; record.ChangedRevision = Changed();
            if (exists) { data.Bricks[index] = record; data.Masks[index] = mask; }
            else { data.BrickIndex.Add(coord, data.Bricks.Length); data.Bricks.Add(record); data.Masks.Add(mask); }
        }
        public void SetBrickUniform(int3 coord, Cell value)
        {
            RequireMutable();
            if (value.IsAir) throw new ArgumentException("Uniform bricks must be solid", nameof(value));
            bool exists = data.BrickIndex.TryGetValue(coord, out int index);
            if (exists)
            {
                var old = data.Bricks[index];
                if (old.Page < 0 && old.Uniform.Equals(value)) return;
                if (old.Page >= 0) data.FreePages.Add(old.Page);
            }
            var record = new BrickRecord { Coord = coord, Page = -1, Uniform = value, SolidCount = BrickMath.CellsPerBrick, ChangedRevision = Changed() };
            if (exists) { data.Bricks[index] = record; data.Masks[index] = BrickMaskBits.Full(); }
            else { data.BrickIndex.Add(coord, data.Bricks.Length); data.Bricks.Add(record); data.Masks.Add(BrickMaskBits.Full()); }
        }
        public bool RemoveBrick(int3 coord)
        {
            RequireMutable();
            if (!data.BrickIndex.TryGetValue(coord, out int index)) return false;
            RemoveAt(index); Changed(); return true;
        }
        public void OptimizeStorage()
        {
            RequireMutable();
            for (int i = 0; i < data.Bricks.Length; i++) OptimizeRecord(i);
        }
        internal void OptimizeBrick(int3 coord)
        {
            RequireMutable();
            if (data.BrickIndex.TryGetValue(coord, out int index)) OptimizeRecord(index);
        }
        void OptimizeRecord(int index)
        {
            var record = data.Bricks[index];
            if (record.Page < 0 || record.SolidCount != BrickMath.CellsPerBrick) return;
            int start = record.Page * BrickMath.CellsPerBrick; Cell value = data.CellPages[start];
            for (int j = 1; j < BrickMath.CellsPerBrick; j++) if (!value.Equals(data.CellPages[start + j])) return;
            data.FreePages.Add(record.Page); record.Page = -1; record.Uniform = value; data.Bricks[index] = record;
        }
        internal void CopyRuntimeMetadata(float cellSize, NativeArray<PaletteEntry> palette, NativeArray<BlockInstance> instances)
        {
            RequireMutable();
            bool changed = data.Palette.Length != palette.Length || data.Instances.Length != instances.Length || CellSize != cellSize;
            if (!changed)
                for (int i = 0; i < palette.Length; i++)
                {
                    var a = data.Palette[i]; var b = palette[i];
                    if (a.Block != b.Block || a.Orient != b.Orient || a.Color != b.Color) { changed = true; break; }
                }
            data.Palette.Clear(); data.Palette.AddRange(palette);
            int oldLength = data.Instances.Length; data.Instances.ResizeUninitialized(instances.Length); data.FreeInstances.Clear();
            for (int i = 0; i < instances.Length; i++)
            {
                var value = instances[i]; value.Functional = Unity.Entities.Entity.Null;
                if (i < oldLength && !changed)
                {
                    var old = data.Instances[i];
                    changed = old.Block != value.Block || old.Orient != value.Orient || old.Color != value.Color || math.any(old.Origin != value.Origin)
                        || old.FunctionHp != value.FunctionHp || old.CellsAlive != value.CellsAlive || old.Flags != value.Flags || old.Reserved != value.Reserved;
                }
                data.Instances[i] = value; if (value.Block == 0) data.FreeInstances.Add(i);
            }
            if (changed) { var header = data.Header.Value; header.CellSize = cellSize; data.Header.Value = header; Changed(); }
        }
        public void Compact()
        {
            RequireMutable();
            OptimizeStorage();
            using var order = SortedBrickOrder(Allocator.Temp);
            using var compact = new VoxelStructure(CellSize);
            var next = compact.data;
            var paletteMap = new NativeArray<ushort>(data.Palette.Length, Allocator.Temp);
            var instanceMap = default(NativeArray<int>);
            try
            {
                instanceMap = new NativeArray<int>(data.Instances.Length, Allocator.Temp);
                for (int k = 0; k < order.Length; k++)
                {
                    var record = data.Bricks[order[k]];
                    int length = record.Page < 0 ? 1 : BrickMath.CellsPerBrick;
                    for (int i = 0; i < length; i++)
                    {
                        Cell value = record.Page < 0 ? record.Uniform : data.CellPages[record.Page * BrickMath.CellsPerBrick + i];
                        if (value.IsAir || value.IsInstance || paletteMap[value.PaletteIndex] != 0) continue;
                        paletteMap[value.PaletteIndex] = (ushort)next.Palette.Length;
                        next.Palette.Add(data.Palette[value.PaletteIndex]);
                    }
                }
                for (int i = 0; i < data.Instances.Length; i++)
                    if (data.Instances[i].Block != 0)
                    { instanceMap[i] = next.Instances.Length; next.Instances.Add(data.Instances[i]); }
                var header = data.Header.Value;
                header.Revision++; header.BoundsValid = 0; header.MassRevision = 0;
                next.Header.Value = header;
                for (int k = 0; k < order.Length; k++)
                {
                    var record = data.Bricks[order[k]];
                    if (record.Page < 0) record.Uniform = Remap(record.Uniform, paletteMap, instanceMap);
                    else
                    {
                        int start = record.Page * BrickMath.CellsPerBrick;
                        record.Page = next.CellPages.Length / BrickMath.CellsPerBrick;
                        for (int i = 0; i < BrickMath.CellsPerBrick; i++) next.CellPages.Add(Remap(data.CellPages[start + i], paletteMap, instanceMap));
                    }
                    record.ChangedRevision = header.Revision;
                    next.BrickIndex.Add(record.Coord, next.Bricks.Length);
                    next.Bricks.Add(record); next.Masks.Add(data.Masks[order[k]]);
                }
                // Capacity setters round up; TrimExcess uses the package’s exact-size reallocation.
                next.Palette.TrimExcess(); next.Instances.TrimExcess(); next.FreeInstances.TrimExcess();
                next.Bricks.TrimExcess(); next.Masks.TrimExcess(); next.CellPages.TrimExcess();
                next.FreePages.TrimExcess(); next.BrickIndex.TrimExcess();
                // Transfer both owners only after the replacement is complete. The temporary owner frees the old storage.
                compact.data = data; data = next;
            }
            finally { paletteMap.Dispose(); if (instanceMap.IsCreated) instanceMap.Dispose(); }
        }
        static Cell Remap(Cell value, NativeArray<ushort> paletteMap, NativeArray<int> instanceMap)
        {
            if (value.IsAir) return default;
            value.Ref = value.IsInstance ? (ushort)(Cell.InstanceBit | instanceMap[value.InstanceIndex]) : paletteMap[value.PaletteIndex];
            return value;
        }
        public void SetReadOnly()
        {
            RequireUnpinned(); var header = data.Header.Value; header.ReadOnly = 1; data.Header.Value = header;
        }
        public void Pin()
        {
            var header = data.Header.Value; header.PinCount = checked(header.PinCount + 1); data.Header.Value = header;
        }
        public void Unpin()
        {
            var header = data.Header.Value;
            if (header.PinCount == 0) throw new InvalidOperationException("Structure is not pinned");
            header.PinCount--; data.Header.Value = header;
        }
        public NativeArray<int> SortedBrickOrder(Allocator a)
        {
            var order = new NativeArray<int>(data.Bricks.Length, a, NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < order.Length; i++) order[i] = i;
            order.Sort(new BrickOrder { Bricks = data.Bricks.AsArray() });
            return order;
        }
        struct BrickOrder : IComparer<int>
        {
            public NativeArray<BrickRecord> Bricks;
            public int Compare(int a, int b)
            {
                int3 x = Bricks[a].Coord, y = Bricks[b].Coord;
                int z = x.z.CompareTo(y.z); if (z != 0) return z;
                int v = x.y.CompareTo(y.y); return v != 0 ? v : x.x.CompareTo(y.x);
            }
        }
        public void GetBlocks(NativeList<StructureBlock> output)
        {
            output.Clear();
            using var order = SortedBrickOrder(Allocator.Temp);
            for (int k = 0; k < order.Length; k++)
            {
                var record = data.Bricks[order[k]];
                for (int i = 0; i < BrickMath.CellsPerBrick; i++)
                {
                    Cell cell = record.Page < 0 ? record.Uniform : data.CellPages[record.Page * BrickMath.CellsPerBrick + i];
                    if (cell.IsAir || cell.IsInstance) continue;
                    var palette = data.Palette[cell.PaletteIndex];
                    output.Add(new StructureBlock
                    {
                        Placement = new BlockPlacement { Block = palette.Block, Origin = BrickMath.CellOf(record.Coord, i), Orient = palette.Orient, Color = palette.Color },
                        InstanceIndex = -1
                    });
                }
            }
            for (int i = 0; i < data.Instances.Length; i++)
            {
                var instance = data.Instances[i];
                if (instance.Block == 0) continue;
                output.Add(new StructureBlock
                {
                    Placement = new BlockPlacement { Block = instance.Block, Origin = instance.Origin, Orient = instance.Orient, Color = instance.Color },
                    InstanceIndex = i
                });
            }
        }
        public VoxelStructure Clone()
        {
            var clone = new VoxelStructure(CellSize);
            try
            {
                var header = data.Header.Value; header.PinCount = 0; header.ReadOnly = 0; clone.data.Header.Value = header;
                Copy(data.Palette, clone.data.Palette); Copy(data.Instances, clone.data.Instances); Copy(data.FreeInstances, clone.data.FreeInstances);
                Copy(data.Bricks, clone.data.Bricks); Copy(data.Masks, clone.data.Masks); Copy(data.CellPages, clone.data.CellPages); Copy(data.FreePages, clone.data.FreePages);
                for (int i = 0; i < data.Bricks.Length; i++) clone.data.BrickIndex.Add(data.Bricks[i].Coord, i);
                clone.data.BrickIndex.TrimExcess(); return clone;
            }
            catch { clone.Dispose(); throw; }
        }
        static void Copy<T>(NativeList<T> source, NativeList<T> target) where T : unmanaged
        { target.Clear(); target.AddRange(source.AsArray()); target.TrimExcess(); }                           // deep copy; not pinned, not read-only
        public static StructureDiff Diff(VoxelStructure from, VoxelStructure to, Allocator a)
        {
            var result = new StructureDiff();
            try
            {
                result.Removed = new NativeList<BlockPlacement>(a); result.Added = new NativeList<BlockPlacement>(a);
                new StructureDiffJob { From = from.data, To = to.data, Removed = result.Removed, Added = result.Added }.Run();
                return result;
            }
            catch { result.Dispose(); throw; }
        }
        public StructureMassData GetMass(in VoxelContent content)
        {
            var header = data.Header.Value;
            if (header.MassRevision == header.Revision) return header.Mass;
            using var results = new NativeArray<StructureMassData>(data.Bricks.Length, Allocator.TempJob);
            new StructureMassJob
            {
                Bricks = data.Bricks.AsArray(), Cells = data.CellPages.AsArray(), Palette = data.Palette.AsArray(),
                Instances = data.Instances.AsArray(), Blocks = content.Blocks, CellSize = CellSize, Results = results
            }.Schedule(data.Bricks.Length, 1).Complete();
            using var order = SortedBrickOrder(Allocator.Temp);
            var total = new StructureMassData();
            for (int i = 0; i < order.Length; i++) MassAccumulator.Add(ref total, results[order[i]]);
            header.Mass = total; header.MassRevision = header.Revision; data.Header.Value = header;
            return total;
        }   // cached by Revision
        public ulong ComputeContentHash(Game.Voxel.Data.VoxsBlockNames names) => Game.Core.StableHash.Hash64(VoxelStructureCodec.Encode(this, names, Game.Voxel.Data.VoxsEncodeOptions.ForSaveChunk));

        public long NativeBytes => !data.IsCreated ? 0 : UnsafeUtility.SizeOf<StructureHeader>()
            + (long)data.Palette.Capacity * UnsafeUtility.SizeOf<PaletteEntry>()
            + (long)data.Instances.Capacity * UnsafeUtility.SizeOf<BlockInstance>()
            + (long)data.FreeInstances.Capacity * sizeof(int)
            + (long)data.Bricks.Capacity * UnsafeUtility.SizeOf<BrickRecord>()
            + (long)data.Masks.Capacity * UnsafeUtility.SizeOf<BrickMaskBits>()
            + (long)data.BrickIndex.Capacity * 20
            + (long)data.CellPages.Capacity * UnsafeUtility.SizeOf<Cell>()
            + (long)data.FreePages.Capacity * sizeof(int);
        public void Clear()
        {
            RequireMutable();
            data.Palette.Clear(); data.Palette.Add(default); data.Instances.Clear(); data.FreeInstances.Clear();
            data.Bricks.Clear(); data.Masks.Clear(); data.BrickIndex.Clear(); data.CellPages.Clear(); data.FreePages.Clear();
            Changed();
        }
        public void Dispose()
        {
            if (!data.IsCreated) return;
            RequireUnpinned(); DisposeStorage();
        }
        void DisposeStorage()
        {
            if (data.Header.IsCreated) data.Header.Dispose();
            if (data.Palette.IsCreated) data.Palette.Dispose();
            if (data.Instances.IsCreated) data.Instances.Dispose();
            if (data.FreeInstances.IsCreated) data.FreeInstances.Dispose();
            if (data.Bricks.IsCreated) data.Bricks.Dispose();
            if (data.Masks.IsCreated) data.Masks.Dispose();
            if (data.BrickIndex.IsCreated) data.BrickIndex.Dispose();
            if (data.CellPages.IsCreated) data.CellPages.Dispose();
            if (data.FreePages.IsCreated) data.FreePages.Dispose();
            data = default;
        }
        void RequireUnpinned()
        {
            if (!data.IsCreated) throw new ObjectDisposedException(nameof(VoxelStructure));
            if (IsPinned) throw new InvalidOperationException("Structure is pinned");
        }
        void RequireMutable()
        {
            RequireUnpinned();
            if (IsReadOnly) throw new InvalidOperationException("Structure is read-only");
        }
        uint Changed()
        {
            var header = data.Header.Value;
            header.Revision++; header.BoundsValid = 0; header.MassRevision = 0;
            data.Header.Value = header; return header.Revision;
        }
        int TakePage()
        {
            if (data.FreePages.Length > 0)
            {
                int last = data.FreePages.Length - 1, page = data.FreePages[last];
                data.FreePages.RemoveAt(last); return page;
            }
            int next = data.CellPages.Length / BrickMath.CellsPerBrick;
            data.CellPages.Resize(data.CellPages.Length + BrickMath.CellsPerBrick, NativeArrayOptions.ClearMemory);
            return next;
        }
        void FillPage(int page, Cell value)
        {
            int start = page * BrickMath.CellsPerBrick;
            for (int i = 0; i < BrickMath.CellsPerBrick; i++) data.CellPages[start + i] = value;
        }
        void RemoveAt(int index)
        {
            var record = data.Bricks[index];
            if (record.Page >= 0) data.FreePages.Add(record.Page);
            data.Bricks.RemoveAtSwapBack(index); data.Masks.RemoveAtSwapBack(index);
            data.BrickIndex.Remove(record.Coord);
            if (index < data.Bricks.Length) data.BrickIndex[data.Bricks[index].Coord] = index;
        }
    }
}
