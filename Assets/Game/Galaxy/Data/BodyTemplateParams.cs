using Game.Data.Definitions;
using Unity.Mathematics;

namespace Game.Galaxy.Data
{
    /// <summary>Implements SPEC-30 §4.4.</summary>
    public struct BodyTemplateParams
    {
        public Game.Data.Definitions.BodyRole Role;
        public float RadiusMinM, RadiusMaxM, BeltWidthM;
        public byte CanHaveMoons;              // 0/1
    }
}
