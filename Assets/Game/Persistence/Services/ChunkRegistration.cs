namespace Game.Persistence.Services
{
    /// <summary>Implements SPEC-05 §4.5. One participant chunk of the registry (§4.8).</summary>
    public readonly struct ChunkRegistration
    {
        public readonly string Key; public readonly int Order; public readonly string OwnerSpec; public readonly string Description;
        public ChunkRegistration(string key, int order, string ownerSpec, string description) { Key = key; Order = order; OwnerSpec = ownerSpec; Description = description; }
    }
}
