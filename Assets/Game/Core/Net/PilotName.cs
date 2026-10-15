using Unity.Collections;
using Unity.Entities;

namespace Game.Core.Net
{
    /// <summary>A player's name on the ship they fly in a co-op session (SPEC-90 §6). Written by Game.App on the host and by the
    /// replicator on guests; the HUD shows it in place of the blueprint name.</summary>
    public struct PilotName : IComponentData
    {
        public FixedString32Bytes Value;
        public static PilotName From(string name)
        {
            var value = new FixedString32Bytes();
            if (!string.IsNullOrEmpty(name)) value.CopyFromTruncated(name);
            return new PilotName { Value = value };
        }
    }
}
