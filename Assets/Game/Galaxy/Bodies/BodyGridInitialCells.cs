using Unity.Entities;
using Game.Galaxy.Data;
using Game.Data.Definitions;

namespace Game.Galaxy.Bodies
{
    /// <summary>Implements SPEC-31 §5.6. BodyGridInitialCells contract. Written by StarSystemLoader at spawn.</summary>
    public struct BodyGridInitialCells : IComponentData
    {
        public uint Cells;
    }
}
