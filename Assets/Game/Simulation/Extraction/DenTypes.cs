using Unity.Entities;

namespace Game.Simulation.Extraction
{
    /// <summary>SPEC-93 decision 10: the den's socket, at its LocalTransform. A free core that comes within Radius installs: it is held
    /// at the socket and powers every DenPowered module of the same Den. Writer: CoreSocketSystem (authority).</summary>
    public struct CoreSocket : IComponentData
    {
        public float Radius;
        public ushort Den;
        public Entity Core;
        public byte Powered;
        public uint InstalledTick;
    }

    /// <summary>On a den module (gun, door, lamp): it works only while On. Writers: CoreSocketSystem (on install), the den builder (new
    /// modules start with the socket's state).</summary>
    public struct DenPowered : IComponentData { public ushort Den; public byte On; }
}
