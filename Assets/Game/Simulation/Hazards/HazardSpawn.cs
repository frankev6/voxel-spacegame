using System;
using Game.Core;
using Game.Data.Definitions;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Hazards
{
    /// <summary>The one spawn API for zones and mine fields (SPEC-91 §4.5, ADR-0118 decision 8). Tracks, encounters, star-system
    /// locations and the playground all use it. HAZ-001 contract: implemented by HAZ-006 (zones) and HAZ-008 (mine fields).</summary>
    public static class HazardSpawn
    {
        public static Entity Zone(EntityManager em, ZoneDefinition def, float2 position, float yaw, ushort group, ulong persistentId)
            => throw new NotImplementedException("HAZ-006");

        public static Entity PathZone(EntityManager em, ZoneDefinition def, ReadOnlySpan<float2> points, ushort group, ulong persistentId)
            => throw new NotImplementedException("HAZ-006");

        /// <summary>Requests the field's mines as grids; returns how many were requested into <paramref name="outRequests"/>.</summary>
        public static int MineField(EntityManager em, MineFieldDefinition def, float2 position, float yaw, ulong seed, ushort group,
                                    IPersistentIdAllocator ids, Span<Entity> outRequests)
            => throw new NotImplementedException("HAZ-008");

        /// <summary>Destroys the zones and devices of a group.</summary>
        public static void DespawnGroup(EntityManager em, ushort group) => throw new NotImplementedException("HAZ-006");
    }
}
