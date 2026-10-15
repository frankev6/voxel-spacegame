using Game.Net.Protocol;
using Game.Simulation;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Game.Voxel.Replica;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Net.Replication.Grids
{
    /// <summary>Header of a GridSpawn message (SPEC-90 NET-004).</summary>
    public struct GridSpawnHeader
    {
        public ulong NetId;
        public byte OwnerSlot;             // slot whose pilot flies it (0 = host player), 255 = nobody
        public GridKind Kind;
        public GridMotion Motion;          // Static or Kinematic on the guest
        public GridRoleHint Role;
        public Allegiance Allegiance;
        public byte HasAllegiance;
        public GridSpawnFlags Flags;
        public float CellSize;
        public ulong Blueprint;            // host GridSource.Blueprint, for names on the HUD; 0 = none
        public float3 Position, Linear, AngularWorld;
        public quaternion Rotation;
        public string Pilot;               // the player flying it, for names on the HUD; empty = nobody
        public byte IsHulk;                // 1: Hulk carries the host's HulkInfo (ADR-0117: the guest applies it as a HulkTransition)
        public HulkInfo Hulk;
    }

    /// <summary>Full-grid wire format: exact palette, instance order and brick cells, deflated. Same build on both ends, so block
    /// indices agree; the handshake's protocol version guards the layout.</summary>
    public static class GridCodec
    {
        const int SparseLimit = 1024;

        public static void WriteHeader(NetWriter w, in GridSpawnHeader h)
        {
            w.U64(h.NetId); w.U8(h.OwnerSlot); w.U8((byte)h.Kind); w.U8((byte)h.Motion); w.U8((byte)h.Role);
            w.U8(h.HasAllegiance); w.U8(h.Allegiance.Faction); w.U8(h.Allegiance.Team); w.U16((ushort)h.Flags);
            w.F32(h.CellSize); w.U64(h.Blueprint); w.F3(h.Position); w.Quat(h.Rotation); w.F3(h.Linear); w.F3(h.AngularWorld);
            w.Str(h.Pilot ?? "");
            WriteHulk(w, h.IsHulk != 0, h.Hulk);
        }

        /// <summary>A hulk flag and, when set, the HulkInfo a guest needs to apply the same transition (spawn headers, GridState).</summary>
        public static void WriteHulk(NetWriter w, bool hulk, in HulkInfo info)
        {
            w.Bool(hulk);
            if (!hulk) return;
            w.U8((byte)info.Cause); w.U8((byte)info.FormerKind); w.U64(info.OriginPersistentId); w.F3(info.CoreLocal);
        }

        public static bool ReadHulk(NetReader r, out HulkInfo info)
        {
            info = default;
            if (!r.Bool()) return false;
            info = new HulkInfo { Cause = (HulkCause)r.U8(), FormerKind = (GridKind)r.U8(), OriginPersistentId = r.U64(), CoreLocal = r.F3() };
            return true;
        }

        public static GridSpawnHeader ReadHeader(NetReader r)
        {
            var h = new GridSpawnHeader
            {
                NetId = r.U64(), OwnerSlot = r.U8(), Kind = (GridKind)r.U8(), Motion = (GridMotion)r.U8(), Role = (GridRoleHint)r.U8(),
                HasAllegiance = r.U8(), Allegiance = new Allegiance { Faction = r.U8(), Team = r.U8() }, Flags = (GridSpawnFlags)r.U16(),
                CellSize = r.F32(), Blueprint = r.U64(), Position = r.F3(), Rotation = r.Quat(), Linear = r.F3(), AngularWorld = r.F3(), Pilot = r.Str()
            };
            h.IsHulk = (byte)(ReadHulk(r, out h.Hulk) ? 1 : 0);
            return h;
        }

        public static void WritePaletteEntry(NetWriter w, PaletteEntry e) { w.U16(e.Block); w.U8(e.Orient); w.U8(e.Color); }
        public static PaletteEntry ReadPaletteEntry(NetReader r) => new PaletteEntry { Block = r.U16(), Orient = r.U8(), Color = r.U8() };
        public static void WriteCell(NetWriter w, Cell c) { w.U16(c.Ref); w.U8(c.Hp); w.U8(c.Flags); }
        public static Cell ReadCell(NetReader r) => new Cell { Ref = r.U16(), Hp = r.U8(), Flags = r.U8() };

        /// <summary>World angular velocity of a body (Unity Physics stores it in inertia space).</summary>
        public static float3 AngularWorld(EntityManager em, Entity e, quaternion rotation)
        {
            if (!em.HasComponent<PhysicsVelocity>(e) || !em.HasComponent<PhysicsMass>(e)) return float3.zero;
            var mass = em.GetComponentData<PhysicsMass>(e);
            return math.rotate(math.mul(rotation, mass.InertiaOrientation), em.GetComponentData<PhysicsVelocity>(e).Angular);
        }

        /// <summary>Host: the whole current grid. Caller completes jobs first.</summary>
        public static void WriteGrid(EntityManager em, Entity root, in GridSpawnHeader header, NetWriter body, NativeArray<Cell> scratch)
        {
            WriteHeader(body, header);
            var palette = em.GetBuffer<PaletteEntry>(root, true);
            body.U16((ushort)palette.Length);
            for (int i = 0; i < palette.Length; i++) WritePaletteEntry(body, palette[i]);
            var instances = em.GetBuffer<BlockInstance>(root, true);
            body.U16((ushort)instances.Length);
            for (int i = 0; i < instances.Length; i++)
            {
                var b = instances[i];
                body.U16(b.Block); body.U8(b.Orient); body.U8(b.Color); body.I3(b.Origin); body.F32(b.FunctionHp); body.U16(b.CellsAlive); body.U8((byte)b.Flags);
            }
            var slots = em.GetBuffer<BrickSlot>(root, true);
            int countAt = body.ReserveU32(); uint bricks = 0;
            for (int s = 0; s < slots.Length; s++)
            {
                Entity brick = slots[s].Brick;
                if (brick == Entity.Null || !em.Exists(brick)) continue;
                WriteBrick(body, em.GetComponentData<Brick>(brick).Coord, em, brick, scratch);
                bricks++;
            }
            body.PatchU32(countAt, bricks);
        }

        /// <summary>One brick as absolute content: dense when more than a quarter is solid, else sorted sparse entries.</summary>
        public static void WriteBrick(NetWriter w, int3 coord, EntityManager em, Entity brick, NativeArray<Cell> scratch)
        {
            ReplicaVoxelApi.ReadBrick(em, brick, scratch);
            WriteBrickCells(w, coord, scratch);
        }

        public static void WriteBrickCells(NetWriter w, int3 coord, NativeArray<Cell> cells)
        {
            int solid = 0;
            for (int i = 0; i < cells.Length; i++) if (!cells[i].IsAir) solid++;
            w.I3(coord); w.U16((ushort)solid);
            if (solid > SparseLimit) { for (int i = 0; i < cells.Length; i++) WriteCell(w, cells[i]); }
            else for (int i = 0; i < cells.Length; i++) if (!cells[i].IsAir) { w.U16((ushort)i); WriteCell(w, cells[i]); }
        }

        /// <summary>Reads one absolute brick into <paramref name="cells"/> (all 4096, air where absent).</summary>
        public static int3 ReadBrickCells(NetReader r, NativeArray<Cell> cells)
        {
            int3 coord = r.I3(); int solid = r.U16();
            for (int i = 0; i < cells.Length; i++) cells[i] = default;
            if (solid > SparseLimit) { for (int i = 0; i < cells.Length; i++) cells[i] = ReadCell(r); }
            else for (int n = 0; n < solid; n++) { int local = r.U16(); var cell = ReadCell(r); if (local < cells.Length) cells[local] = cell; }
            return coord;
        }

        /// <summary>Guest: the structure for GridSpawnRequest, with the host's palette and instance order preserved.</summary>
        public static VoxelStructure ReadStructure(NetReader r, float cellSize)
        {
            int paletteCount = r.U16();
            using var palette = new NativeArray<PaletteEntry>(paletteCount, Allocator.Temp);
            var p = palette;
            for (int i = 0; i < paletteCount; i++) p[i] = ReadPaletteEntry(r);
            int instanceCount = r.U16();
            using var instances = new NativeArray<BlockInstance>(instanceCount, Allocator.Temp);
            var inst = instances;
            for (int i = 0; i < instanceCount; i++)
                inst[i] = new BlockInstance { Block = r.U16(), Orient = r.U8(), Color = r.U8(), Origin = r.I3(), FunctionHp = r.F32(),
                    CellsAlive = r.U16(), Flags = (BlockInstanceFlags)r.U8() };
            var structure = ReplicaVoxelApi.CreateStructure(cellSize, palette, instances);
            uint bricks = r.U32();
            using var cells = new NativeArray<Cell>(BrickMath.CellsPerBrick, Allocator.Temp);
            for (uint b = 0; b < bricks; b++)
            {
                int3 coord = ReadBrickCells(r, cells);
                structure.SetBrickDense(coord, cells);
            }
            structure.OptimizeStorage();
            return structure;
        }
    }
}
