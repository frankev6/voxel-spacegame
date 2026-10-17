using Game.Data.Hazards;
using Unity.Entities;

namespace Game.Simulation.Hazards
{
    /// <summary>One effect of a zone (SPEC-91 §4.3).</summary>
    [InternalBufferCapacity(1)]
    public struct ZoneEffect : IBufferElementData
    {
        public ZoneEffectKind Kind;
        public float A, B, C;      // Accelerate: A = target speed m/s, B = accel m/s²
                                   // Drag: A = drag per second (fraction of speed), B = min speed m/s
                                   // Repel: A = stiffness 1/s², B = damping 1/s, C = contact HP per m/s of approach
                                   // Gate: A = direction sign (+1, −1 or 0 = both); Bounds: none (inside = in bounds)
        public ushort Id;          // Gate: gate id; Bounds: bounds id; otherwise 0
        public ZoneEffectFlags Flags;
    }
}
