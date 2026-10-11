using Unity.Entities;

namespace Game.Voxel.Damage
{
    /// <summary>On every brick. Writer: IntegritySystem (and SplitSystem when it creates bricks: Unknown).</summary>
    public struct BrickIntegrity : IComponentData
    {
        public BrickConnectivity State;   // Connected = all solid cells of the brick form one component (instance links included)
        public byte HasInstanceCells;     // 1 if any cell has Ref bit 15 set
        public ushort SolidCountAtCheck;  // Brick.SolidCount when State was computed; mismatch => treat as Unknown
    }
}
