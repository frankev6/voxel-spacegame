using Game.Data.Definitions;
using Unity.Mathematics;

namespace Game.Galaxy.Data
{
    /// <summary>A lane between two star systems. A.Value &lt; B.Value always. Implements SPEC-30 §4.</summary>
    public struct LaneDef
    {
        public StarSystemId A, B;
        public float LengthLy;
        public Game.Data.Definitions.LaneKind Kind;
        public LaneFlags Flags;
        public ushort GateIndex;           // 0 = ungated; else 1 + index into GalaxyDef.LaneGates
        public ushort GateTemplateA;       // body template index of the gate in A (GalaxyDef.BodyTemplates)
        public ushort GateTemplateB;       // body template index of the gate in B
    }
}
