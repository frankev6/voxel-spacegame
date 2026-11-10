using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Extraction
{
    /// <summary>SPEC-93 §4: the magnet tow, on the towing ship's root. A one-sided spring: slack under Length, a spring and damper
    /// over it. Writer: TowApi (attach, release) and TowSystem (snap). Anchors are body-local, so origin rebases do not touch them.</summary>
    public struct TowLink : IComponentData
    {
        public Entity Load;             // a root with PhysicsVelocity + PhysicsMass, or Null
        public float3 ShipAnchor, LoadAnchor;
        public float Length;            // slack length (m)
        public float Frequency;         // Hz of the taut cable (0 = TowSystem.DefaultFrequency)
        public float DampingRatio;      // (0 = TowSystem.DefaultDamping)
        public float BreakForce;        // N; over it the cable snaps (0 = unbreakable)
        public uint SinceTick;
        public float Tension;           // N, last tick (for the HUD and the cable's look)
    }

    /// <summary>On a towed root: who tows it and the velocity it should have had after last tick's cable impulse (for knocks).</summary>
    public struct TowLoad : IComponentData
    {
        public Entity Tower;
        public float MassT;
        public float3 ExpectedVelocity;
        public uint ExpectedTick;
        public uint LastKnockTick;
    }

    /// <summary>A sound watchers can hear (SPEC-93 decision 3): a towed load knocking on something, a snapped cable, a cut tether.</summary>
    public struct NoiseEvent
    {
        public float3 Position;
        public float Radius;            // m it carries
        public Entity Source;           // the ship it gives away (the tower), or Null
        public byte Kind;               // NoiseKind
    }

    public enum NoiseKind : byte { Knock = 0, Snap = 1, Cut = 2, Blast = 3 }

    /// <summary>Singleton: this tick's noises. Cleared by TowSystem (the first writer each tick); later writers append; WatchSystem reads.
    /// Main thread only.</summary>
    public struct NoiseEvents : IComponentData
    {
        public NativeList<NoiseEvent> Current;
        public uint Tick;
    }
}
