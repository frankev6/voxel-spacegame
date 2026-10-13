namespace Game.App.Spawning
{
    /// <summary>Arguments of ShipSpawnService.Spawn. Implements SPEC-01 §4.4. Requester values are supplied before grid profile conversion.</summary>
    public struct ShipSpawnArgs
    {
        public Game.Core.ContentId Blueprint;               // blueprint source; ignored when Structure != null
        public Game.Voxel.Model.VoxelStructure Structure;   // registered structure source (caller keeps ownership)
        public Unity.Mathematics.float3 Position;           // local frame
        public Unity.Mathematics.quaternion Rotation;       // yaw only (ADR-0003); other components are discarded
        public Unity.Mathematics.float3 LinearVelocity;
        public Game.Simulation.Allegiance Allegiance;
        public bool WithAi;                                 // adds SPEC-23 AI components
        public Game.Core.ContentId AiProfile;               // None = the faction default (SPEC-23)
        public bool IsPlayer;                               // PlayerShip tag, WriteBack, no StarSystemMember
        public ulong PersistentId;                          // 0 = allocate
    }
}
