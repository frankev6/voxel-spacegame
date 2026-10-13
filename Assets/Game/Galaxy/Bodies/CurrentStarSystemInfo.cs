using Unity.Entities;
using Game.Galaxy.Data;
using Game.Data.Definitions;

namespace Game.Galaxy.Bodies
{
    /// <summary>Implements SPEC-31 §4.2. CurrentStarSystemInfo contract. Only writer: StarSystemLoader.</summary>
    public struct CurrentStarSystemInfo : IComponentData
    {
        public StarSystemId Id;
        public byte Loaded;
        public byte Changing;
        public ushort BodyCount;
    }
}
