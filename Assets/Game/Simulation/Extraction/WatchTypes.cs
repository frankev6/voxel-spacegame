using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Extraction
{
    /// <summary>SPEC-93 decision 6: what a crew ship broadcasts. Off, it is dark: any watcher that sees it notices it. Writer: TransponderApi.</summary>
    public enum AccessCode : byte { None = 0, Trader = 1, Work = 2, Garrison = 3 }

    public struct Transponder : IComponentData { public byte On; public AccessCode Code; }

    /// <summary>0..1 per crew ship. Writer: WatchSystem (authority). Seen: a watcher saw it misfit this tick (HUD); Faction: who last raised it.</summary>
    public struct Suspicion : IComponentData
    {
        public float Value;
        public uint LastSeenTick;
        public byte Seen;
        public byte Faction;
        public byte Heard;      // a watcher heard it this tick
    }

    /// <summary>A ship identified by these factions (bit per faction index): their AI treats it as hostile whatever the stance (decision 7).
    /// Writer: WatchSystem (adds bits), HeistDirector (reset).</summary>
    public struct Wanted : IComponentData { public ulong FactionMask; }

    public enum WatcherKind : byte { Light = 0, Sensor = 1, Ship = 2, Crowd = 3 }

    /// <summary>Something that looks: on its own entity at its LocalTransform (lights, sensors, crowds), or on an AI ship's root.
    /// Light and Sensor look along Facing (local) within an arc, swept by SweepRadians over SweepSeconds; Ship and Crowd look all round.
    /// A Light or Sensor in a dark district sees nothing.</summary>
    public struct Watcher : IComponentData
    {
        public WatcherKind Kind;
        public byte Faction;
        public float3 Facing;
        public float Range, CosHalfArc;
        public float Rate;              // suspicion per second at close range
        public float SweepRadians, SweepSeconds, SweepPhase;
        public ushort District;
    }

    /// <summary>A box (LocalTransform centre and rotation, HalfExtents) where only ships broadcasting Needs or a higher code fit
    /// (Trader &lt; Work &lt; Garrison). Outside every zone a ship with its transponder on is traffic.</summary>
    public struct AccessZone : IComponentData
    {
        public float3 HalfExtents;
        public AccessCode Needs;
        public ushort District;
    }

    public static class WatchRules
    {
        public const float DecayPerSecond = .08f, DecayDelaySeconds = 4f, HeardJump = .34f;

        public static bool Fits(AccessCode code, AccessCode needs) => needs == AccessCode.None || code >= needs;

        /// <summary>1 near, falling to .35 at the edge of range: far ships are noticed slowly.</summary>
        public static float Closeness(float distance, float range) => math.lerp(1f, .35f, math.saturate((distance / math.max(1, range) - .3f) / .7f));

        public static float BaseRate(WatcherKind kind) => kind switch { WatcherKind.Light => .9f, WatcherKind.Sensor => .45f, WatcherKind.Ship => .6f, _ => .3f };
    }
}
