using Game.Voxel.Model;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>Internal ownership transfers, pools and batch finalization (SPEC-12 §4.7, §5.8).</summary>
    internal static class ColliderPipeline
    {
        internal static int AddRecord(ref ColliderCacheContainers c, Entity root, uint serial, float cellSize,
            GridMotion motion, byte editorPreview = 0, BlobAssetReference<Collider> installed = default)
        {
            if (c.GridIndex.TryGetValue(serial, out int existing)) return existing;
            int index = c.Grids.Length;
            c.Grids.Add(new GridColliderRecord
            {
                Root = root, Serial = serial, CellSize = cellSize, Motion = motion, IsEditorPreview = editorPreview,
                Children = new UnsafeList<BrickChild>(32, Allocator.Persistent), Updates = new UnsafeList<int>(8, Allocator.Persistent),
                Installed = installed,
            });
            c.GridIndex.Add(serial, index);
            var stats = c.Stats.Value; stats.Grids++; stats.InstalledBytes += Bytes(installed); c.Stats.Value = stats;
            return index;
        }

        internal static int AllocateMask(ref ColliderCacheContainers c, in BrickMaskBits mask)
        {
            if (c.FreeMasks.Length == 0) { int index = c.Masks.Length; c.Masks.Add(mask); return index; }
            int slot = c.FreeMasks[c.FreeMasks.Length - 1]; c.FreeMasks.RemoveAt(c.FreeMasks.Length - 1);
            c.Masks[slot] = mask; return slot;
        }
        internal static void FreeMask(ref ColliderCacheContainers c, int slot)
        {
            if (slot < 0) return;
            c.Masks[slot] = default; c.FreeMasks.Add(slot);
        }
        internal static ulong CollectingKey(uint serial, uint installTick) => ((ulong)serial << 32) | installTick;

        internal static int GetOrCreateUpdate(ref ColliderCacheContainers c, uint serial, uint installTick, uint requestTick, ColliderPriority priority)
        {
            ulong key = CollectingKey(serial, installTick);
            if (c.Collecting.TryGetValue(key, out int found))
            {
                var update = c.Updates[found]; if (priority < update.Priority) update.Priority = priority;
                c.Updates[found] = update; return found;
            }
            var recordIndex = c.GridIndex[serial]; var record = c.Grids[recordIndex];
            int index; UnsafeList<UpdateBrick> bricks;
            if (c.FreeUpdates.Length != 0)
            {
                index = c.FreeUpdates[c.FreeUpdates.Length - 1]; c.FreeUpdates.RemoveAt(c.FreeUpdates.Length - 1);
                bricks = c.Updates[index].Bricks;
            }
            else { index = c.Updates.Length; c.Updates.Add(default); bricks = new UnsafeList<UpdateBrick>(32, Allocator.Persistent); }
            c.Updates[index] = new ColliderUpdate
            {
                State = UpdateState.Collecting, Priority = priority, Serial = serial, Seq = record.NextSeq++,
                CreateOrder = c.NextCreateOrder.Value, RequestTick = requestTick, InstallTick = installTick, Bricks = bricks,
            };
            c.NextCreateOrder.Value++;
            record.Updates.Add(index); c.Grids[recordIndex] = record; c.Collecting.Add(key, index);
            var stats = c.Stats.Value; stats.PendingUpdates++; c.Stats.Value = stats;
            return index;
        }

        internal static void SetSnapshot(ref ColliderCacheContainers c, int updateIndex, int3 coord, in BrickMaskBits mask, bool removed)
        {
            var update = c.Updates[updateIndex]; var key = new CollectKey { Update = updateIndex, Coord = coord };
            bool replace = c.CollectingBricks.TryGetValue(key, out int brickIndex);
            if (replace) FreeMask(ref c, update.Bricks[brickIndex].MaskSlot);
            var brick = new UpdateBrick { Coord = coord, MaskSlot = removed ? -1 : AllocateMask(ref c, mask) };
            if (replace) update.Bricks[brickIndex] = brick;
            else
            {
                c.CollectingBricks.Add(key, update.Bricks.Length); update.Bricks.Add(brick);
                var stats = c.Stats.Value; stats.PendingBricks++; c.Stats.Value = stats;
            }
            c.Updates[updateIndex] = update;
        }

        internal static void Seal(ref ColliderCacheContainers c, int index)
        {
            var update = c.Updates[index]; LeaveCollecting(ref c, index, update);
            update.State = UpdateState.Pending; c.Updates[index] = update;
        }
        static void LeaveCollecting(ref ColliderCacheContainers c, int index, in ColliderUpdate update)
        {
            if (c.Collecting.TryGetValue(CollectingKey(update.Serial, update.InstallTick), out int mapped) && mapped == index)
                c.Collecting.Remove(CollectingKey(update.Serial, update.InstallTick));
            for (int i = 0; i < update.Bricks.Length; i++)
                c.CollectingBricks.Remove(new CollectKey { Update = index, Coord = update.Bricks[i].Coord });
        }

        /// <summary>Moves the next snapshot into a batch value. Cleared slots cannot be freed twice during later discard.</summary>
        internal static BrickMaskBits TakeNextMask(ref ColliderCacheContainers c, int index, out bool removed)
        {
            var update = c.Updates[index]; int position = update.BricksScheduled;
            var brick = update.Bricks[position]; removed = brick.MaskSlot < 0;
            var mask = removed ? default : c.Masks[brick.MaskSlot]; FreeMask(ref c, brick.MaskSlot);
            brick.MaskSlot = -1; update.Bricks[position] = brick;
            update.BricksScheduled++; update.State = UpdateState.Building; c.Updates[index] = update;
            var stats = c.Stats.Value; stats.PendingBricks--; c.Stats.Value = stats;
            return mask;
        }

        internal static void Retire(ref ColliderCacheContainers c, BlobAssetReference<Collider> blob)
        {
            if (!blob.IsCreated) return;
            c.Retired.Add(blob); var stats = c.Stats.Value; stats.RetiredPendingBytes += Bytes(blob); c.Stats.Value = stats;
        }
        internal static long Bytes(BlobAssetReference<Collider> blob) => blob.IsCreated ? blob.Value.MemorySize : 0;

        /// <summary>Caller has moved every retired blob into DeferredDisposalQueue; the queue now owns them.</summary>
        internal static void ClearTransferredRetired(ref ColliderCacheContainers c)
        {
            c.Retired.Clear(); var stats = c.Stats.Value; stats.RetiredPendingBytes = 0; c.Stats.Value = stats;
        }

        /// <summary>Caller proves no in-flight batch references this slot. Pending compounds are retired unless ownership was moved first.</summary>
        internal static void FreeUpdate(ref ColliderCacheContainers c, int index)
        {
            var update = c.Updates[index];
            if (update.State == UpdateState.Free) return;
            LeaveCollecting(ref c, index, update);
            for (int i = 0; i < update.Bricks.Length; i++) FreeMask(ref c, update.Bricks[i].MaskSlot);
            if (c.GridIndex.TryGetValue(update.Serial, out int recordIndex))
            {
                var record = c.Grids[recordIndex];
                for (int i = 0; i < record.Updates.Length; i++)
                    if (record.Updates[i] == index) { record.Updates.RemoveAt(i); break; }
                c.Grids[recordIndex] = record;
            }
            var stats = c.Stats.Value; stats.PendingUpdates--; stats.PendingBricks -= update.Bricks.Length - update.BricksScheduled;
            stats.PendingCompoundBytes -= Bytes(update.Compound); c.Stats.Value = stats;
            Retire(ref c, update.Compound);
            update.Bricks.Clear(); c.Updates[index] = new ColliderUpdate { Bricks = update.Bricks }; c.FreeUpdates.Add(index);
        }

        internal static bool BatchReferences(in ColliderBatchContainers batch, int update)
        {
            for (int i = 0; i < batch.Bricks.Length; i++) if (batch.Bricks[i].Update == update) return true;
            for (int i = 0; i < batch.Assemblies.Length; i++) if (batch.Assemblies[i].Update == update) return true;
            return false;
        }

        internal static void RemoveRecord(ref ColliderCacheContainers c, int index, in ColliderBatchContainers batch)
        {
            var record = c.Grids[index]; c.GridIndex.Remove(record.Serial); c.Grids.RemoveAtSwapBack(index);
            if (index < c.Grids.Length) c.GridIndex[c.Grids[index].Serial] = index;
            var stats = c.Stats.Value; stats.Grids--; stats.InstalledBytes -= Bytes(record.Installed);
            for (int i = 0; i < record.Children.Length; i++)
            { stats.CachedBricks--; stats.CachedBoxes -= record.Children[i].BoxCount; stats.CachedBrickBytes -= Bytes(record.Children[i].Blob); }
            c.Stats.Value = stats; Retire(ref c, record.Installed);
            for (int i = 0; i < record.Children.Length; i++) Retire(ref c, record.Children[i].Blob);
            for (int i = 0; i < record.Updates.Length; i++)
            {
                int u = record.Updates[i];
                if (BatchReferences(batch, u))
                {
                    var update = c.Updates[u]; LeaveCollecting(ref c, u, update);
                    update.State = UpdateState.Discarded; c.Updates[u] = update;
                }
                else FreeUpdate(ref c, u);
            }
            record.Children.Dispose(); record.Updates.Dispose();
        }

        internal static int FindChild(in UnsafeList<BrickChild> children, int3 coord)
        {
            int low = 0, high = children.Length;
            while (low < high)
            { int mid = low + (high - low) / 2; if (GridColliderAssembler.CompareCoords(children[mid].Coord, coord) < 0) low = mid + 1; else high = mid; }
            return low;
        }
        static void ApplyChild(ref ColliderCacheContainers c, int recordIndex, int3 coord, in BrickBuildResult result)
        {
            var record = c.Grids[recordIndex]; int position = FindChild(record.Children, coord);
            bool exists = position < record.Children.Length && math.all(record.Children[position].Coord == coord);
            bool removed = result.Removed != 0 || !result.Blob.IsCreated;
            var stats = c.Stats.Value;
            BlobAssetReference<Collider> previous = default;
            if (exists)
            {
                var old = record.Children[position]; previous = old.Blob;
                stats.CachedBricks--; stats.CachedBoxes -= old.BoxCount; stats.CachedBrickBytes -= Bytes(old.Blob);
                record.Children.RemoveAt(position);
            }
            if (!removed)
            {
                record.Children.Add(default);
                for (int i = record.Children.Length - 1; i > position; i--) record.Children[i] = record.Children[i - 1];
                record.Children[position] = new BrickChild { Coord = coord, Blob = result.Blob, BoxCount = result.BoxCount };
                stats.CachedBricks++; stats.CachedBoxes += result.BoxCount; stats.CachedBrickBytes += Bytes(result.Blob);
            }
            c.Grids[recordIndex] = record; c.Stats.Value = stats; Retire(ref c, previous);
            if (removed) Retire(ref c, result.Blob);
        }

        /// <summary>After batch completion. Jobs pass only C/B, never Access or the main-thread batch State.</summary>
        internal static void Finalize(ref ColliderCacheContainers c, ref ColliderBatchContainers batch)
        {
            for (int i = 0; i < batch.Bricks.Length; i++)
            {
                var item = batch.Bricks[i]; var update = c.Updates[item.Update]; var result = batch.Results[i];
                if (update.State == UpdateState.Discarded || !c.GridIndex.TryGetValue(item.Serial, out int recordIndex))
                { Retire(ref c, result.Blob); update.State = UpdateState.Discarded; c.Updates[item.Update] = update; }
                else ApplyChild(ref c, recordIndex, item.Coord, result);
                batch.Results[i] = default;
            }
            for (int i = 0; i < batch.Assemblies.Length; i++)
            {
                var item = batch.Assemblies[i]; var update = c.Updates[item.Update]; var result = batch.AssemblyResults[i];
                if (update.State == UpdateState.Discarded || !c.GridIndex.ContainsKey(item.Serial))
                { Retire(ref c, result.Compound); update.State = UpdateState.Discarded; }
                else
                {
                    var stats = c.Stats.Value; stats.PendingCompoundBytes += Bytes(result.Compound) - Bytes(update.Compound); c.Stats.Value = stats;
                    Retire(ref c, update.Compound);
                    update.Compound = result.Compound; update.ChildCount = result.ChildCount; update.BoxCount = result.BoxCount; update.State = UpdateState.Built;
                }
                c.Updates[item.Update] = update; batch.AssemblyResults[i] = default;
            }
            // Includes partial discarded updates with brick results but no assembly; never recycle before all results are consumed.
            for (int i = 0; i < batch.Bricks.Length; i++)
            {
                int u = batch.Bricks[i].Update;
                if (c.Updates[u].State == UpdateState.Discarded) FreeUpdate(ref c, u);
            }
            for (int i = 0; i < batch.Assemblies.Length; i++)
            {
                int u = batch.Assemblies[i].Update;
                if (c.Updates[u].State == UpdateState.Discarded) FreeUpdate(ref c, u);
            }
            batch.Bricks.Clear(); batch.Masks.Clear(); batch.Results.Clear(); batch.Assemblies.Clear();
            batch.AssemblyChildren.Clear(); batch.AssemblyApply.Clear(); batch.AssemblyResults.Clear();
        }
        internal static void Finalize(ref ColliderBuildCache cache, ref ColliderBatchQueue batch) => Finalize(ref cache.C, ref batch.B);
    }
}
