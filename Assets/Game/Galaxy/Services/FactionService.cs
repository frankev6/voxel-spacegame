using Game.Core.Flow;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Simulation;
using Game.Simulation.Factions;
using Unity.Entities;

namespace Game.Galaxy.Services
{
    /// <summary>SPEC-33 §4.5/5.3 managed faction facade; ENC-004 owns queued relations and new-game behavior.</summary>
    public sealed class FactionService : INewGameInitializer, System.IDisposable
    {
        // TODO(ENC-004): bind the ENC-003 singleton and preserve pending request semantics.
        public FactionService(World world, ContentCatalog catalog) => throw new System.NotImplementedException("ENC-004");
        public int NewGameOrder => 30;
        public FactionIndexMap Indices => throw new System.NotImplementedException("ENC-004");
        public FactionDefinition PlayerFaction => throw new System.NotImplementedException("ENC-004");
        public int GetRelation(FactionDefinition faction) => throw new System.NotImplementedException("ENC-004");
        public FactionStance GetPlayerStance(FactionDefinition faction) => throw new System.NotImplementedException("ENC-004");
        public void AdjustRelation(FactionDefinition faction, int delta) => throw new System.NotImplementedException("ENC-004");
        public void SetRelation(FactionDefinition faction, int value) => throw new System.NotImplementedException("ENC-004");
        public FactionStance GetStance(byte factionA, byte factionB) => throw new System.NotImplementedException("ENC-004");
        public void InitializeNewGame(in NewGameArgs args) => throw new System.NotImplementedException("ENC-004");
        public void Dispose() => throw new System.NotImplementedException("ENC-004");
    }
}
