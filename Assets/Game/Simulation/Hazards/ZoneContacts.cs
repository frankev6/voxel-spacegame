using Unity.Collections;
using Unity.Entities;

namespace Game.Simulation.Hazards
{
    /// <summary>Singleton: this tick's gate and bounds contacts. Writer: ZoneSystem; read by deciders after it (SPEC-91 §4.3).</summary>
    public struct ZoneContacts : IComponentData
    {
        public NativeList<ZoneContact> Current;
    }
}
