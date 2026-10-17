using System;
using Game.Core;
using Game.Data.Definitions;
using Game.Simulation.Hazards;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.Galaxy.Racing
{
    /// <summary>SPEC-92 §5: spawns a track's zones, gates and pieces at a local-frame origin under one group, and creates the race
    /// entity (RaceState, its gates and slots). Despawn removes all of it. Authority; guests get grids and zones by replication.</summary>
    public static class TrackSpawnService
    {
        /// <summary>Returns the race entity. Gate zone persistent ids are derived from the group so every machine agrees.</summary>
        public static Entity Spawn(EntityManager em, TrackDefinition track, float2 origin, ushort group, IPersistentIdAllocator ids,
            Func<ShipBlueprintDefinition, float2, float, ulong, Entity> spawnPiece = null, bool spawnMines = false)
        {
            if (track == null) throw new ArgumentException("TrackSpawnService.Spawn: no track");
            var errors = TrackValidator.Check(track);
            if (errors.Count > 0) throw new ArgumentException(track.IdString + ": " + string.Join("; ", errors));
            Despawn(em, group);
            ulong Id() => ids != null ? ids.Allocate() : 0;
            for (int i = 0; i < track.Zones.Length; i++)
            {
                var z = track.Zones[i];
                if (z.Zone == null) continue;
                Entity e;
                if (z.Zone.Shape == Game.Data.Hazards.ZoneShape.Path)
                {
                    var pts = new float2[z.Path.Length];
                    for (int k = 0; k < pts.Length; k++) pts[k] = origin + (float2)z.Path[k];
                    e = HazardSpawn.PathZone(em, z.Zone, pts, group, Id(), z.PathRadius > 0 ? z.PathRadius : z.Zone.PathRadius);
                }
                else
                {
                    e = HazardSpawn.Zone(em, z.Zone, origin + (float2)z.Position, z.Yaw, group, Id());
                    if (z.HalfExtents.x > 0) { var w = em.GetComponentData<WorldZone>(e); w.HalfExtents = z.HalfExtents; em.SetComponentData(e, w); }
                }
            }
            var race = em.CreateEntity(typeof(RaceState), typeof(LocalTransform));
            em.SetName(race, "Race " + track.IdString);
            em.SetComponentData(race, LocalTransform.FromPosition(new float3(origin.x, 0, origin.y)));
            em.SetComponentData(race, new RaceState { Phase = RacePhase.Lobby, Laps = (byte)math.clamp(track.DefaultLaps, 1, 99), TrackGroup = group,
                Track = track.Id.Hash, Origin = origin });
            em.AddBuffer<RacerProgress>(race);
            // Gate zones first: creating entities invalidates any buffer held across it.
            var rows = new RaceGate[track.Gates.Length];
            for (int g = 0; g < track.Gates.Length; g++)
            {
                var t = track.Gates[g];
                ulong mask = 0; foreach (int n in t.Next) mask |= 1UL << n;
                rows[g] = new RaceGate { Position = t.Position, Yaw = t.Yaw, Width = t.Width, Required = (byte)(t.Required ? 1 : 0), NextMask = mask,
                    RespawnPosition = t.RespawnPosition, RespawnYaw = t.RespawnYaw };
                if (track.GateZone != null)
                {
                    var zone = HazardSpawn.Zone(em, track.GateZone, origin + (float2)t.Position, t.Yaw, group, Id());
                    var w = em.GetComponentData<WorldZone>(zone); w.HalfExtents = new float2(t.Width / 2, 2); em.SetComponentData(zone, w);
                    HazardSpawn.SetEffectId(em, zone, (ushort)g);
                }
            }
            var gates = em.AddBuffer<RaceGate>(race);
            foreach (var row in rows) gates.Add(row);
            var slots = em.AddBuffer<RaceSlot>(race);
            foreach (var s in track.StartGrid) slots.Add(new RaceSlot { Position = s.Position, Yaw = s.Yaw });
            // Mines are grids: the authority spawns them and guests receive them by replication (spawnMines only there).
            if (spawnMines && track.MineFields != null)
                foreach (var m in track.MineFields)
                {
                    if (m.Field == null) continue;
                    var requests = new Entity[m.Field.Count];
                    HazardSpawn.MineField(em, m.Field, origin + (float2)m.Position, m.Yaw, m.Seed, group, ids, requests);
                }
            if (spawnPiece != null)
                foreach (var piece in track.Pieces)
                {
                    if (piece.Blueprint == null) continue;
                    var root = spawnPiece(piece.Blueprint, origin + (float2)piece.Position, piece.Yaw, Id());
                    if (root != Entity.Null) em.AddComponentData(root, new TrackPieceOf { Group = group });
                }
            return race;
        }

        /// <summary>Removes the group's zones, its race entity and its pieces (grid pieces through GridDespawnRequest).</summary>
        public static void Despawn(EntityManager em, ushort group)
        {
            HazardSpawn.DespawnGroup(em, group);
            using (var q = em.CreateEntityQuery(typeof(RaceState)))
            using (var races = q.ToEntityArray(Allocator.Temp))
                foreach (var e in races) if (em.GetComponentData<RaceState>(e).TrackGroup == group) em.DestroyEntity(e);
            using (var q = em.CreateEntityQuery(typeof(TrackPieceOf)))
            using (var pieces = q.ToEntityArray(Allocator.Temp))
                foreach (var e in pieces)
                {
                    if (em.GetComponentData<TrackPieceOf>(e).Group != group) continue;
                    if (em.HasComponent<Game.Voxel.Model.GridDespawnRequest>(e)) Game.Voxel.Grid.GridSpawnApi.RequestDespawn(em, e);
                    else em.DestroyEntity(e);
                }
        }
    }
}
