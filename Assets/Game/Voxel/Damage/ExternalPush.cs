using Unity.Entities;
using Unity.Mathematics;

namespace Game.Voxel.Damage
{
    /// <summary>Velocity change that impulses applied to this root in one tick: damage knockback, requested impulses, recoil and
    /// zone pushes alike. Writer: ImpulseApplySystem. PropulsionSystem folds it into FlightState carry (SPEC-91 §4.2, ADR-0118).</summary>
    public struct ExternalPush : IComponentData
    {
        public float3 LinearDv;   // world m/s applied by impulses this tick (sum)
        public float YawDv;       // rad/s about +Y applied this tick
        public uint Tick;         // tick it was applied in
    }
}
