using System;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Voxel.Damage.Jobs
{
    internal struct RestorePending : IComparable<RestorePending>
    {
        public CellRestoreRequest Request;
        public ulong Id;
        public int3 Min, Max;
        public long Cursor;
        public byte Started, Done;
        public int CompareTo(RestorePending other)
        {
            int c = Request.Grid.Index.CompareTo(other.Request.Grid.Index); if (c != 0) return c;
            c = ((byte)Request.Producer).CompareTo((byte)other.Request.Producer); if (c != 0) return c;
            c = Request.Sequence.CompareTo(other.Request.Sequence); return c != 0 ? c : Id.CompareTo(other.Id);
        }
    }
    internal struct RestoreSpan
    {
        public CellRestoreRequest Request;
        public ulong Id;
        public int3 Min, Max;
        public long Start;
        public int Count;
    }
    internal struct RestoreHealedInstance { public ulong RequestId; public int Instance; }

    /// <summary>SPEC-13 §5.3.1: reserve a deterministic global visit budget before exclusive grid jobs run.</summary>
    [BurstCompile] internal unsafe struct PlanRestoresJob : IJob
    {
        [ReadOnly] public NativeList<CellRestoreRequest> Incoming;
        [ReadOnly] public ComponentLookup<VoxelGrid> Grids;
        [ReadOnly] public ComponentLookup<GridSpawning> Spawning;
        public NativeList<RestorePending> Pending;
        public NativeList<RestoreSpan> Spans;
        public NativeList<RestoreHealedInstance> Healed;
        public NativeReference<ulong> NextId;
        public int Budget;
        [NativeDisableUnsafePtrRestriction] public DamageStreamsData* Data;
        public void Execute()
        {
            Spans.Clear();
            int keep = 0;
            for (int i = 0; i < Pending.Length; i++) if (Pending[i].Done == 0) Pending[keep++] = Pending[i];
            Pending.ResizeUninitialized(keep);
            for (int i = 0; i < Incoming.Length; i++)
            {
                ulong id = NextId.Value + 1; NextId.Value = id;
                Pending.Add(new RestorePending { Request = Incoming[i], Id = id });
            }
            Pending.Sort(); int remaining = math.max(0, Budget);
            var retainedIds = new NativeHashSet<ulong>(math.max(1, Pending.Length), Allocator.Temp);
            for (int i = 0; i < Pending.Length; i++)
            {
                var pending = Pending[i]; var request = pending.Request;
                if (!Grids.HasComponent(request.Grid) || request.Mode != CellRestoreMode.Heal && request.Mode != CellRestoreMode.Place ||
                    request.Mode == CellRestoreMode.Heal && (!math.isfinite(request.HpFraction) || request.HpFraction <= 0 || request.HpFraction > 1 || math.any(request.CellMin > request.CellMax)))
                { pending.Done = 1; Pending[i] = pending; Data->Current.RestoresDropped++; continue; }
                if (Spawning.HasComponent(request.Grid) && Spawning.IsComponentEnabled(request.Grid))
                { retainedIds.Add(pending.Id); Data->Current.RestoresDeferred++; continue; }
                if (pending.Started == 0)
                {
                    var grid = Grids[request.Grid];
                    pending.Min = request.Mode == CellRestoreMode.Heal ? math.max(request.CellMin, grid.BrickMin * 16) : request.CellMin;
                    pending.Max = request.Mode == CellRestoreMode.Heal ? math.min(request.CellMax, (grid.BrickMin + grid.BrickDims) * 16 - 1) : request.CellMin;
                    pending.Started = 1;
                }
                long total = 0;
                if (math.all(pending.Max >= pending.Min))
                {
                    long x = (long)pending.Max.x - pending.Min.x + 1, y = (long)pending.Max.y - pending.Min.y + 1, z = (long)pending.Max.z - pending.Min.z + 1;
                    total = x * y * z;
                }
                int count = (int)Math.Min((long)remaining, Math.Max(0L, total - pending.Cursor));
                if (count > 0)
                {
                    Spans.Add(new RestoreSpan { Request = request, Id = pending.Id, Min = pending.Min, Max = pending.Max, Start = pending.Cursor, Count = count });
                    pending.Cursor += count; remaining -= count;
                }
                pending.Done = (byte)(pending.Cursor >= total ? 1 : 0); Pending[i] = pending;
                if (pending.Done == 0) { retainedIds.Add(pending.Id); Data->Current.RestoresDeferred++; }
                // Current spans still need earlier instance-heal records even if this is their final fragment.
                if (count > 0) retainedIds.Add(pending.Id);
            }
            keep = 0;
            for (int i = 0; i < Healed.Length; i++) if (retainedIds.Contains(Healed[i].RequestId)) Healed[keep++] = Healed[i];
            Healed.ResizeUninitialized(keep); retainedIds.Dispose();
        }
    }
}
