namespace Game.App.Flow
{
    /// <summary>Dev only: World scene without galaxy streaming. Implements SPEC-01 §4.3.</summary>
    public readonly struct SandboxArgs
    {
        public readonly bool SpawnPlayer; public readonly ulong Seed;
        public SandboxArgs(bool spawnPlayer, ulong seed) { SpawnPlayer = spawnPlayer; Seed = seed; }
    }
}
