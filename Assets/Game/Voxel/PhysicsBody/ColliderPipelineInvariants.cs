using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>Debug ownership/order audit after the caller has completed cache and batch access (SPEC-12 §4.7).</summary>
    internal static class ColliderPipelineInvariants
    {
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_EDITOR")]
        internal static void Check(in ColliderBuildCache cache, in ColliderBatchQueue batch) => Check(cache.C, batch.B);

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_EDITOR")]
        internal static void Check(in ColliderCacheContainers c, in ColliderBatchContainers batch)
        {
            using var owners = new NativeHashSet<ulong>(32, Allocator.Temp);
            using var listed = new NativeHashSet<int>(32, Allocator.Temp);
            using var freeUpdates = new NativeHashSet<int>(32, Allocator.Temp);
            using var maskSlots = new NativeHashSet<int>(32, Allocator.Temp);
            var totals = new ColliderPipelineStats();
            Require(c.GridIndex.Count == c.Grids.Length);
            for (int i = 0; i < c.Grids.Length; i++)
            {
                var record = c.Grids[i]; Require(c.GridIndex.TryGetValue(record.Serial, out int mapped) && mapped == i);
                totals.Grids++; totals.InstalledBytes += Own(record.Installed, owners);
                for (int j = 0; j < record.Children.Length; j++)
                {
                    var child = record.Children[j]; Require(child.Blob.IsCreated);
                    if (j != 0) Require(GridColliderAssembler.CompareCoords(record.Children[j - 1].Coord, child.Coord) < 0);
                    totals.CachedBricks++; totals.CachedBoxes += child.BoxCount; totals.CachedBrickBytes += Own(child.Blob, owners);
                }
                uint previous = 0; bool nonBuilt = false;
                for (int j = 0; j < record.Updates.Length; j++)
                {
                    int index = record.Updates[j]; Require(index >= 0 && index < c.Updates.Length && listed.Add(index));
                    var update = c.Updates[index]; Require(update.State != UpdateState.Free && update.Serial == record.Serial);
                    if (j != 0) Require(update.Seq > previous);
                    previous = update.Seq;
                    if (update.State == UpdateState.Built) Require(!nonBuilt); else nonBuilt = true;
                }
            }
            for (int i = 0; i < c.FreeUpdates.Length; i++)
            {
                int index = c.FreeUpdates[i]; Require(index >= 0 && index < c.Updates.Length && freeUpdates.Add(index));
                Require(c.Updates[index].State == UpdateState.Free);
            }
            for (int i = 0; i < c.FreeMasks.Length; i++)
            {
                int slot = c.FreeMasks[i]; Require(slot >= 0 && slot < c.Masks.Length && maskSlots.Add(slot));
            }
            int collecting = 0, collectingBricks = 0;
            for (int i = 0; i < c.Updates.Length; i++)
            {
                var update = c.Updates[i];
                if (update.State == UpdateState.Free)
                { Require(freeUpdates.Contains(i) && update.Bricks.Length == 0 && !update.Compound.IsCreated); continue; }
                Require(update.State == UpdateState.Discarded || listed.Contains(i));
                Require(update.BricksScheduled >= 0 && update.BricksScheduled <= update.Bricks.Length);
                totals.PendingUpdates++; totals.PendingBricks += update.Bricks.Length - update.BricksScheduled;
                totals.PendingCompoundBytes += Own(update.Compound, owners);
                bool isCollecting = update.State == UpdateState.Collecting;
                if (isCollecting)
                {
                    collecting++;
                    Require(c.Collecting.TryGetValue(ColliderPipeline.CollectingKey(update.Serial, update.InstallTick), out int mapped) && mapped == i);
                    Require(update.BricksScheduled == 0);
                }
                for (int j = 0; j < update.Bricks.Length; j++)
                {
                    var brick = update.Bricks[j];
                    if (j < update.BricksScheduled) Require(brick.MaskSlot == -1);
                    if (brick.MaskSlot >= 0) Require(brick.MaskSlot < c.Masks.Length && maskSlots.Add(brick.MaskSlot));
                    if (isCollecting)
                    {
                        collectingBricks++;
                        Require(c.CollectingBricks.TryGetValue(new CollectKey { Update = i, Coord = brick.Coord }, out int mapped) && mapped == j);
                    }
                }
            }
            Require(maskSlots.Count == c.Masks.Length);
            Require(collecting == c.Collecting.Count && collectingBricks == c.CollectingBricks.Count);
            Require(batch.Bricks.Length == batch.Masks.Length && batch.Bricks.Length == batch.Results.Length);
            Require(batch.Assemblies.Length == batch.AssemblyResults.Length);
            for (int i = 0; i < batch.Bricks.Length; i++)
            {
                var item = batch.Bricks[i]; CheckBatchUpdate(c, item.Update, item.Serial);
                Own(batch.Results[i].Blob, owners);
            }
            for (int i = 0; i < batch.Assemblies.Length; i++)
            {
                var item = batch.Assemblies[i]; CheckBatchUpdate(c, item.Update, item.Serial);
                Own(batch.AssemblyResults[i].Compound, owners);
            }
            // AssemblyChildren are borrowed snapshots; their duplicate references are deliberately not owners.
            for (int i = 0; i < c.Retired.Length; i++) totals.RetiredPendingBytes += Own(c.Retired[i], owners);
            var stats = c.Stats.Value;
            Require(stats.Grids == totals.Grids && stats.CachedBricks == totals.CachedBricks && stats.CachedBoxes == totals.CachedBoxes);
            Require(stats.PendingUpdates == totals.PendingUpdates && stats.PendingBricks == totals.PendingBricks);
            Require(stats.CachedBrickBytes == totals.CachedBrickBytes && stats.InstalledBytes == totals.InstalledBytes);
            Require(stats.PendingCompoundBytes == totals.PendingCompoundBytes && stats.RetiredPendingBytes == totals.RetiredPendingBytes);
        }
        static void CheckBatchUpdate(in ColliderCacheContainers c, int index, uint serial)
        {
            Require(index >= 0 && index < c.Updates.Length);
            var update = c.Updates[index]; Require(update.Serial == serial);
            Require(update.State == UpdateState.Building || update.State == UpdateState.Built || update.State == UpdateState.Discarded);
        }
        static unsafe long Own(BlobAssetReference<Collider> blob, NativeHashSet<ulong> owners)
        {
            if (!blob.IsCreated) return 0;
            Require(owners.Add((ulong)blob.GetUnsafePtr())); return blob.Value.MemorySize;
        }
        static void Require(bool condition)
        {
            if (!condition) throw new InvalidOperationException("Collider pipeline invariant failed.");
        }
    }
}
