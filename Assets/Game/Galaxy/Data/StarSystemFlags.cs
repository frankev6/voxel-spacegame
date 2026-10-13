using Game.Data.Definitions;
using Unity.Mathematics;

namespace Game.Galaxy.Data
{
    /// <summary>Implements SPEC-30 §4.2.</summary>
    [System.Flags] public enum StarSystemFlags : ushort { None = 0, Authored = 1, Start = 2, Story = 4, HiddenUntilKnown = 8, NoEncounters = 16, Hub = 32 }
}
