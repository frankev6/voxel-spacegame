namespace Game.Voxel.Damage
{
    public struct DamageTickStats
    {
        public uint Tick;
        public int EventsIn, EventsDropped, GridsResolved, CellsDestroyed, CellsDetached, InstancesDestroyed;
        public int IntegrityGrids, IntegrityDeferred, IslandsFound;
        public int HulksCreated, ChunksCreated, ParticleIslands, SplitsDeferred, DebrisParticles, RamEvents;
        public int ImpulsesApplied, ImpulsesDropped, CellsHealed, CellsPlaced, RestoresDropped, RestoresDeferred;   // ADR-0016 channels
    }
}
