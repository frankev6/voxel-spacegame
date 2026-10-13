using Game.Data.Definitions;
using Unity.Mathematics;

namespace Game.Galaxy.Data
{
    /// <summary>Implements SPEC-30 §4.6.</summary>
    public struct ReplacementRecord { public ulong BodyKey; public Game.Core.ContentId Template; }
}
