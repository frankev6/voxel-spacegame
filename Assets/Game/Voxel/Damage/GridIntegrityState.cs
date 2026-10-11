using Unity.Entities;

namespace Game.Voxel.Damage
{
    /// <summary>Writer: IntegritySystem.</summary>
    public struct GridIntegrityState : IComponentData
    {
        public byte ConsecutiveDeferrals;
        public byte Pad0; public ushort Pad1;
    }
}
