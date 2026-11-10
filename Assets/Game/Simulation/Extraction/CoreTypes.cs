using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Extraction
{
    /// <summary>SPEC-93 decision 5: a station core, a dynamic grid held at a pose on its station (the Holder) while any of its tethers is
    /// whole. Writers: the place setup (spawn), CoreTetherSystem (Tethers, Freed, FreedTick), CoreSocketSystem (Socket).
    /// The pose is holder-local so origin rebases do not touch it.</summary>
    public struct StationCore : IComponentData
    {
        public Entity Holder;            // the station grid root that holds it, or Null (held where it is)
        public float3 HoldLocal;
        public quaternion HoldRotation;  // holder-local
        public ulong Station;            // the station's PersistentId (the alarm's owner)
        public byte Tethers;             // whole tethers left
        public byte Freed;
        public uint FreedTick;
        public float MassT;
        public Entity Socket;            // the socket it powers once installed (CoreSocketSystem)
    }

    /// <summary>One tether, its own entity. It is cut by a cut request (the cut key, the console) or when its post grid is gone or has
    /// lost half its cells. Cutting darkens its district (decision 5). Writer: CoreTetherSystem.</summary>
    public struct CoreTether : IComponentData
    {
        public Entity Core;
        public Entity Holder;            // anchors are holder-local (the station)
        public float3 Anchor;            // holder-local
        public float3 CoreAnchor;        // core-local
        public ushort District;
        public Entity Post;              // the post grid that carries it, or Null
        public int PostCells;            // the post's solid cells at the first look (0: not counted yet)
        public byte Cut;
        public uint CutTick;
    }

    /// <summary>A district of a place: lamps, watchers, doors and turrets with its Id go dark when Powered is 0. Writer: CoreTetherSystem
    /// (blackouts); the place setup creates them.</summary>
    public struct District : IComponentData
    {
        public ushort Id;
        public byte Powered;
        public uint ChangedTick;
    }

    /// <summary>On things that belong to a district (watchers, lamps, turrets, doors).</summary>
    public struct DistrictMember : IComponentData { public ushort District; }

    /// <summary>Singleton: tethers asked to be cut this tick (main thread: the cut key, the console, scenarios). CoreTetherSystem empties it.</summary>
    public struct TetherCutRequests : IComponentData { public NativeList<Entity> Pending; }

    public static class Districts
    {
        /// <summary>Whether the district `id` has power (a district with no entity is powered).</summary>
        public static bool Powered(EntityManager em, EntityQuery districts, ushort id)
        {
            if (id == 0) return true;
            using var all = districts.ToComponentDataArray<District>(Allocator.Temp);
            foreach (var d in all) if (d.Id == id) return d.Powered != 0;
            return true;
        }
    }
}
