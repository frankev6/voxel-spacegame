namespace Game.Core.Net
{
    /// <summary>Who owns the simulation on this machine (ADR-0116, SPEC-90 §4). Solo and Host run the full simulation; a Guest replicates the host.</summary>
    public enum SimRoleKind : byte { Solo = 0, Host = 1, Guest = 2 }

    /// <summary>Boot singleton created by Game.App; absent counts as Solo. Only writer: the co-op session in Game.App.</summary>
    public struct SimRole : Unity.Entities.IComponentData
    {
        public SimRoleKind Value;
        public bool IsGuest => Value == SimRoleKind.Guest;
        public bool IsHost => Value == SimRoleKind.Host;
    }
}
