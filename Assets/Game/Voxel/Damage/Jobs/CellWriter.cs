using System;
using Game.Core;
using Game.Data.Blocks;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

namespace Game.Voxel.Damage.Jobs
{
    /// <summary>SPEC-13 §5.3.5–6: one authoritative cell write/removal path, owned by one grid job.</summary>
    internal partial struct CellWriter : IDisposable
    {
        public GridCellAccess Access;
        public DamageConfig Config;
        public DamageEvent Event;
        public DamageTypeRow Row;
        public LocalTransform Transform;
        public PhysicsVelocity Velocity;
        public PhysicsMass Mass;
        public uint Tick;
        public int Ordinal;
        public float3 IslandCentroidWorld;
        public GridDamageStats Stats;
        public bool Changed;
        public NativeList<CellRemoved> Removed;
        public NativeList<TouchedBrick> Touched;
        public NativeList<Entity> DestroyList;
        NativeHashSet<Entity> touchedSet;

        public void Allocate(Allocator allocator)
        {
            Removed = new NativeList<CellRemoved>(allocator);
            Touched = new NativeList<TouchedBrick>(allocator);
            DestroyList = new NativeList<Entity>(allocator);
            touchedSet = new NativeHashSet<Entity>(16, allocator);
        }
        public void Dispose()
        {
            Removed.Dispose(); Touched.Dispose(); DestroyList.Dispose(); touchedSet.Dispose();
        }
        void Touch(Entity brick, int slot, BrickDirtyFlags flags)
        {
            var dirty = Access.Dirty[brick]; dirty.Flags |= flags; Access.Dirty[brick] = dirty;
            if (touchedSet.Add(brick)) Touched.Add(new TouchedBrick { Grid = Access.Grid, SlotIndex = slot, Brick = brick });
            // Enable bits are applied serially by the resolver's Flatten phase, never by parallel ranges.
        }
        public void ApplyCellDamage(int3 c, float effective)
        {
            if (effective <= 0f || !Access.TryGetCell(c, out var cell, out var brick, out int local)) return;
            float remaining = DamageMath.HpUnits(cell.Hp, Access.CellHp(cell));
            if (effective >= remaining) { RemoveCell(c, CellRemovedReason.Destroyed); return; }
            byte hp = DamageMath.QuantizeHp(remaining - effective, Access.CellHp(cell), DamageMath.Dither01(Tick, Access.Grid.Index, c, Ordinal++));
            if (hp == 0) { RemoveCell(c, CellRemovedReason.Destroyed); return; }
            bool sameBin = (cell.Hp >> 6) == (hp >> 6); cell.Hp = hp;
            Changed |= Access.Write(brick, local, cell, sameBin);
            if (!sameBin) Touch(brick, Access.Slot(c), BrickDirtyFlags.Mesh);
            Stats.DamageTaken += effective;
            if (cell.IsInstance)
            {
                var instance = Access.Instances[cell.InstanceIndex]; instance.FunctionHp -= effective;
                Access.Instances[cell.InstanceIndex] = instance;
            }
            // Point/blast callers evaluate instance collapse after this write (FND-020).
        }
        public bool RemoveCell(int3 c, CellRemovedReason reason)
        {
            if (!Access.TryGetCell(c, out var cell, out var brick, out int local)) return false;
            var appearance = Access.Appearance(cell);
            float hp = DamageMath.HpUnits(cell.Hp, Access.CellHp(cell));
            Access.Write(brick, local, default); Changed = true;
            BrickDirtyFlags flags = BrickDirtyFlags.Mesh | BrickDirtyFlags.Collider;
            // BrickDirtyScheduleSystem routes runtime collider changes to urgent ColliderPriority.Damage.
            int slot = Access.Slot(c); Touch(brick, slot, flags);
            int3 localCell = BrickMath.LocalOf(c);
            for (int axis = 0; axis < 3; axis++)
            {
                if (localCell[axis] != 0 && localCell[axis] != 15) continue;
                int3 neighbour = BrickMath.BrickOf(c); neighbour[axis] += localCell[axis] == 0 ? -1 : 1;
                int neighbourSlot = BrickMath.SlotIndex(neighbour, Access.Definition.BrickMin, Access.Definition.BrickDims);
                if (neighbourSlot < 0) continue;
                var other = Access.Slots[neighbourSlot].Brick;
                if (other != Entity.Null) Touch(other, neighbourSlot, BrickDirtyFlags.Mesh);
            }
            if (Access.Bricks[brick].SolidCount == 0)
            {
                DestroyList.Add(brick); Access.Slots[slot] = default;
            }
            float3 world = Transform.TransformPoint(((float3)c + .5f) * Access.Definition.CellSize);
            Removed.Add(new CellRemoved
            {
                Grid = Access.Grid, Cell = c, Block = appearance.Block, Color = appearance.Color, Orient = appearance.Orient,
                InstanceIndex = cell.IsInstance ? cell.InstanceIndex : -1, Reason = reason, WorldPos = world,
                WorldVel = DamageMath.VelocityAtPoint(Velocity, Mass, Transform, world), Kick = Kick(c, world, reason)
            });
            if (reason == CellRemovedReason.Destroyed)
            {
                Stats.CellsDestroyed++; Stats.DamageTaken += hp;
                if (cell.IsInstance)
                {
                    var instance = Access.Instances[cell.InstanceIndex]; instance.FunctionHp -= hp;
                    instance.CellsAlive--; Access.Instances[cell.InstanceIndex] = instance;
                }
            }
            else if (reason == CellRemovedReason.DetachedBody || reason == CellRemovedReason.DetachedParticle) Stats.CellsDetached++;
            // Integrity derives solid face-neighbour seeds from Removed; it owns the retained seed buffer.
            return true;
        }
        float3 Kick(int3 cell, float3 world, CellRemovedReason reason)
        {
            if (reason == CellRemovedReason.DetachedBody || (Event.Flags & DamageEventFlags.NoKick) != 0) return 0;
            var random = Random.CreateFromIndex((uint)StableHash.Combine(StableHash.Combine(Tick, (ulong)Access.Grid.Index), DamageMath.Packed(cell)));
            float3 unit = random.NextFloat3Direction(); float r = random.NextFloat();
            float k = Config.KickRandomFraction;
            float3 com = Transform.TransformPoint(Mass.CenterOfMass);
            float3 direction; float speed;
            if (reason == CellRemovedReason.DetachedParticle)
            {
                direction = IslandCentroidWorld - com; direction.y = 0; speed = Config.KickDetach;
            }
            else if (reason == CellRemovedReason.Dissolved) { direction = world - com; speed = Config.KickDissolve; }
            else if (Event.Radius > 0)
            {
                direction = world - Transform.TransformPoint(Event.LocalPoint);
                speed = math.lerp(Row.KickMin, Row.KickMax, math.saturate(1f - math.length(direction) / Event.Radius));
            }
            else
            {
                direction = -math.rotate(Transform.Rotation, math.normalizesafe(Event.LocalDir));
                speed = Row.KickMin * (.6f + .8f * r);
            }
            return math.normalizesafe(direction * (1f - k) + unit * k, unit) * speed;
        }
    }
}
