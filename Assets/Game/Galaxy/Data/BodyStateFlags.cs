using Game.Data.Definitions;
using Unity.Mathematics;

namespace Game.Galaxy.Data
{
    /// <summary>Implements SPEC-30 §4.2.</summary>
    [System.Flags] public enum BodyStateFlags : byte { None = 0, Destroyed = 1, Interacted = 2, Replaced = 4, StorySpawn = 8, HasStructure = 16 }
}
