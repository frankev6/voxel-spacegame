using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Flow;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Simulation;
using Game.Simulation.Factions;
using Unity.Entities;

namespace Game.Galaxy.Services
{
    /// <summary>Managed facade over the single fixed-step faction writer (SPEC-33 §5.3).</summary>
    public sealed class FactionService : INewGameInitializer, IDisposable
    {
        readonly World world;
        readonly Entity singleton;
        bool disposed;
        internal readonly SortedDictionary<string, int> Unresolved = new(StringComparer.Ordinal);
        public FactionService(World world, ContentCatalog catalog)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            if (!world.IsCreated) throw new ArgumentException("World must be created.", nameof(world));
            Indices = FactionIndexMap.Build(catalog);
            using var query = world.EntityManager.CreateEntityQuery(typeof(FactionState), typeof(FactionRelations), typeof(FactionStanding), typeof(FactionRelationRequest));
            if (query.CalculateEntityCount() != 1) throw new InvalidOperationException("Faction boot singleton is required.");
            singleton = query.GetSingletonEntity();
            if (world.EntityManager.GetComponentData<FactionState>(singleton).Count != Indices.Count)
                throw new InvalidOperationException("Faction catalog and boot matrix disagree.");
        }
        EntityManager Manager
        {
            get { if (disposed || !world.IsCreated) throw new ObjectDisposedException(nameof(FactionService)); return world.EntityManager; }
        }
        public int NewGameOrder => 30;
        public FactionIndexMap Indices { get; }
        public FactionDefinition PlayerFaction => Indices.ByIndex(0);
        internal static int Clamp(long value) => (int)(value < -100 ? -100 : value > 100 ? 100 : value);
        public int GetRelation(FactionDefinition faction)
        {
            var em = Manager; byte index = Indices.IndexOf(faction);
            if (index == 255) return 0;
            em.CompleteDependencyBeforeRO<FactionStanding>(); em.CompleteDependencyBeforeRO<FactionRelationRequest>();
            var standing = em.GetBuffer<FactionStanding>(singleton, true)[index];
            int value = standing.Relation;
            if (standing.Locked == 0)
                foreach (var request in em.GetBuffer<FactionRelationRequest>(singleton, true))
                    if (request.Faction == index) value = Clamp(request.Mode == 0 ? (long)value + request.Value : request.Value);
            return value;
        }
        public FactionStance GetPlayerStance(FactionDefinition faction)
        {
            if (Indices.IndexOf(faction) == 255) return FactionStance.Neutral;
            var em = Manager; em.CompleteDependencyBeforeRO<FactionState>();
            return Stance(GetRelation(faction), em.GetComponentData<FactionState>(singleton));
        }
        public void AdjustRelation(FactionDefinition faction, int delta) => Queue(faction, delta, 0);
        public void SetRelation(FactionDefinition faction, int value) => Queue(faction, value, 1);
        void Queue(FactionDefinition faction, int value, byte mode)
        {
            var em = Manager; byte index = Indices.IndexOf(faction);
            em.CompleteDependencyBeforeRO<FactionStanding>();
            if (index == 255 || em.GetBuffer<FactionStanding>(singleton, true)[index].Locked != 0)
            { GameLog.WarnOnce("Faction.RelationIgnored:" + (faction != null ? faction.IdString : "none"), "Factions", "Relation change ignored for unknown or locked faction."); return; }
            em.CompleteDependencyBeforeRW<FactionRelationRequest>();
            em.GetBuffer<FactionRelationRequest>(singleton).Add(new FactionRelationRequest { Faction = index, Value = value, Mode = mode });
        }
        public FactionStance GetStance(byte factionA, byte factionB)
        {
            var em = Manager; em.CompleteDependencyBeforeRO<FactionRelations>();
            return em.GetComponentData<FactionRelations>(singleton).Get(factionA, factionB);
        }
        public void InitializeNewGame(in NewGameArgs args) => Replace(null, null);
        internal void Replace(Dictionary<byte, int> values, SortedDictionary<string, int> unresolved)
        {
            var em = Manager;
            em.CompleteDependencyBeforeRW<FactionStanding>(); em.CompleteDependencyBeforeRW<FactionRelationRequest>();
            em.CompleteDependencyBeforeRW<FactionRelations>(); em.CompleteDependencyBeforeRW<FactionState>();
            var state = em.GetComponentData<FactionState>(singleton);
            var standings = em.GetBuffer<FactionStanding>(singleton);
            var matrix = em.GetComponentData<FactionRelations>(singleton);
            for (byte i = 0; i < Indices.Count; i++)
            {
                var def = Indices.ByIndex(i); var standing = standings[i];
                int relation = i == 0 ? 100 : Clamp(def.BasePlayerRelation);
                if (i != 0 && standing.Locked == 0 && values != null && values.TryGetValue(i, out int saved)) relation = Clamp(saved);
                standing.Relation = relation; standing.LastProvokedTick = 0; standing.PlayerStance = Stance(relation, state);
                standings[i] = standing;
                matrix.Stance[i] = matrix.Stance[i * matrix.Count] = (byte)standing.PlayerStance;
            }
            matrix.Stance[0] = (byte)FactionStance.Friendly;
            em.GetBuffer<FactionRelationRequest>(singleton).Clear();
            state.Revision++; em.SetComponentData(singleton, state);
            Unresolved.Clear();
            if (unresolved != null) foreach (var row in unresolved) Unresolved.Add(row.Key, row.Value);
        }
        static FactionStance Stance(int value, FactionState state) => value <= state.HostileAtOrBelow ? FactionStance.Hostile
            : value >= state.FriendlyAtOrAbove ? FactionStance.Friendly : FactionStance.Neutral;
        public void Dispose() { disposed = true; Unresolved.Clear(); }
    }
}
