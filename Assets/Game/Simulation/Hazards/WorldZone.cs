using Game.Data.Hazards;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Hazards
{
    /// <summary>An analytic planar zone in the local frame (SPEC-91 §4.3). Spawned by HazardSpawn; Position is shifted by OriginRebaseSystem.</summary>
    public struct WorldZone : IComponentData
    {
        public ZoneShape Shape;
        public float2 HalfExtents;  // Box: half width (local x) and half length (local z); Circle: x = radius
        public float PathRadius;    // Path: capsule radius
        public float Yaw;           // radians; zone forward = local +Z
        public float2 Position;     // local-frame XZ
        public ulong PersistentId;  // sort key and replication id
        public ushort Group;        // owner group (a track, an encounter); 0 = none
    }
}
