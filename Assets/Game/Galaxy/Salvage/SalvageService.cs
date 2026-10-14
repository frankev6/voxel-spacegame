using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Audio;
using Game.Core.Components;
using Game.Core.Events;
using Game.Core.Flow;
using Game.Data.Definitions;
using Game.Data.Blocks;
using Game.Data.Economy;
using Game.Data.Services;
using Game.Galaxy.Bodies;
using Game.Galaxy.Interaction;
using Game.Simulation.Commands;
using Game.Voxel.Damage;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using Unity.Collections;
using Unity.Entities;

namespace Game.Galaxy.Salvage
{
    /// <summary>Deterministic first-touch yields retained across partial transfers; despawn remains grid-owner work.</summary>
    public sealed class SalvageService : IDisposable
    {
        readonly World world;
        readonly ContentCatalog catalog;
        readonly EconomyConfigDefinition config;
        readonly IAudioService audio;
        readonly SalvageLedger ledger = new();
        EntityQuery targets, hulks, players;
        bool disposed, transferring, leavingWorld;
        public SalvageService(World world, ContentCatalog catalog, EconomyConfigDefinition config, IAudioService audio)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.audio = audio ?? throw new ArgumentNullException(nameof(audio));
            targets = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<InteractionTarget>());
            hulks = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<Hulk>(), ComponentType.ReadOnly<PersistentId>());
            players = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerShip>());
            EventBus<BodyInteracted>.Subscribe(Interacted); EventBus<SceneTransitionStarted>.Subscribe(Transition);
            EventBus<SceneTransitionFinished>.Subscribe(TransitionFinished); EventBus<SessionReset>.Subscribe(Reset);
        }
        public int PendingWrecks
        {
            get { if (disposed || !world.IsCreated) return 0; ledger.Prune(world.EntityManager); return ledger.Entries.Count; }
        }
        void Transition(SceneTransitionStarted value) => leavingWorld = value.From == GameScene.World;
        void TransitionFinished(SceneTransitionFinished value)
        { if (leavingWorld && value.Success) ledger.Clear(); leavingWorld = false; }
        void Reset(SessionReset _) { ledger.Clear(); leavingWorld = false; }
        void Interacted(BodyInteracted value)
        {
            if (disposed || !world.IsCreated || value.Kind != (byte)InteractionKind.Salvage) return;
            var em = world.EntityManager; em.CompleteDependencyBeforeRO<InteractionTarget>(); em.CompleteDependencyBeforeRO<PersistentId>();
            if (targets.TryGetSingleton<InteractionTarget>(out var target) && em.Exists(target.Target) &&
                em.HasComponent<PersistentId>(target.Target) && em.GetComponentData<PersistentId>(target.Target).Value == value.GridPersistentId)
            { TrySalvage(target.Target, out _); return; }
            using var roots = hulks.ToEntityArray(Allocator.Temp);
            foreach (var root in roots)
                if (em.GetComponentData<PersistentId>(root).Value == value.GridPersistentId) { TrySalvage(root, out _); return; }
        }
        static bool Refuse(string text, out string message)
        { message = text; EventBus<EconomyNotice>.Publish(new EconomyNotice(EconomyNoticeKind.SalvageRefused, text)); return false; }
        public bool TrySalvage(Entity root, out string message)
        {
            if (disposed || !world.IsCreated || transferring) return Refuse("Nothing to salvage.", out message);
            var em = world.EntityManager;
            em.CompleteDependencyBeforeRO<GridMass>(); em.CompleteDependencyBeforeRO<HulkInfo>(); em.CompleteDependencyBeforeRO<PersistentId>();
            if (!em.Exists(root) || !em.HasComponent<Hulk>(root) || em.HasComponent<PlayerShip>(root) || em.HasComponent<BodyGridOwner>(root) ||
                !em.HasComponent<GridMass>(root) || !em.HasComponent<HulkInfo>(root) || !em.HasComponent<PersistentId>(root) ||
                !em.HasBuffer<BlockInstance>(root) || !em.HasComponent<GridDespawnRequest>(root) || em.IsComponentEnabled<GridDespawnRequest>(root) || em.HasComponent<SalvageCollected>(root))
                return Refuse("Nothing to salvage.", out message);
            uint cells = em.GetComponentData<GridMass>(root).Data.CellCount;
            if (cells > config.MaxSalvageCells) return Refuse("Too large to salvage.", out message);
            if (!Game.Core.Services.TryGet<ICargo>(out var cargo)) return Refuse("No cargo hold.", out message);
            ledger.Prune(em); ulong id = em.GetComponentData<PersistentId>(root).Value;
            if (!ledger.Entries.TryGetValue(id, out var entry) || entry.Root != root)
            {
                em.CompleteDependencyBeforeRO<BlockInstance>(); entry = new SalvageLedger.Entry { Root = root };
                ComputeYields(em.GetBuffer<BlockInstance>(root, true), cells, StableHash.Combine(id, em.GetComponentData<HulkInfo>(root).Tick), catalog, config, entry.Yields);
                ledger.Entries[id] = entry;
            }
            int total = 0; bool remaining = false; transferring = true;
            try
            {
                for (int i = 0; i < entry.Yields.Count; i++)
                {
                    var value = entry.Yields[i]; if (value.Count == 0) continue;
                    int added = cargo.AddUpTo(value.ItemIndex, value.Count, AcquireSource.Salvage);
                    value.Count -= added; total += added; entry.Yields[i] = value; remaining |= value.Count != 0;
                }
                if (!remaining)
                {
                    // QA-001: the wreck streams into the player's ship (SalvagePullSystem, which despawns it after the last wave)
                    // instead of vanishing; without a collector it despawns at once.
                    if (players.CalculateEntityCount() == 1)
                    {
                        em.AddComponentData(root, new SalvageCollected { Collector = players.GetSingletonEntity() });
                        if (em.HasComponent<Interactable>(root)) em.RemoveComponent<Interactable>(root);
                    }
                    else GridSpawnApi.RequestDespawn(em, root);
                    ledger.Entries.Remove(id);
                    if (config.SalvageCue != null) audio.PlayCue(config.SalvageCue.Id);
                    message = "Salvaged " + total + " items.";
                    EventBus<EconomyNotice>.Publish(new EconomyNotice(EconomyNoticeKind.SalvageDone, message)); return true;
                }
                message = "Cargo full: salvage left on the wreck.";
                EventBus<EconomyNotice>.Publish(new EconomyNotice(EconomyNoticeKind.SalvagePartial, message)); return true;
            }
            finally { transferring = false; }
        }
        public static void ComputeYields(DynamicBuffer<BlockInstance> instances, uint solidCells, ulong seed,
            ContentCatalog catalog, EconomyConfigDefinition config, List<SalvageYield> into)
        {
            if (catalog == null || config == null || into == null) throw new ArgumentNullException();
            into.Clear(); var merged = new Dictionary<int, int>();
            void Add(int item, int count)
            {
                if (item == 0 || count <= 0) return;
                if (merged.TryGetValue(item, out int at)) { var value = into[at]; value.Count = checked(value.Count + count); into[at] = value; }
                else { merged.Add(item, into.Count); into.Add(new SalvageYield { ItemIndex = item, Count = count }); }
            }
            var rng = RngStreams.For(seed, RngPurpose.Salvage, 0); int structuralTaken = 0;
            foreach (var instance in instances)
            {
                if (instance.Block == 0) continue;
                var definition = catalog.GetBlock(instance.Block); float u = rng.NextFloat();
                if (definition == null || (instance.Flags & BlockInstanceFlags.Destroyed) != 0 || instance.CellsAlive != definition.Cells.Count) continue;
                bool functional = definition.Kind == BlockKind.Functional;
                float chance = functional ? config.SalvageFunctionalChance : config.SalvageStructuralChance;
                if (u >= chance || !functional && structuralTaken >= config.SalvageMaxStructuralItems) continue;
                Add(catalog.IndexOf<ItemDefinition>(definition), 1); if (!functional) structuralTaken++;
            }
            int scrap = (int)Math.Min(config.SalvageMaxScrap, Unity.Mathematics.math.floor((float)solidCells * config.ScrapPerCell));
            if (scrap > 0) Add(catalog.IndexOf(config.ScrapItem), scrap);
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            EventBus<BodyInteracted>.Unsubscribe(Interacted); EventBus<SceneTransitionStarted>.Unsubscribe(Transition); EventBus<SceneTransitionFinished>.Unsubscribe(TransitionFinished); EventBus<SessionReset>.Unsubscribe(Reset);
            ledger.Clear(); if (world.IsCreated) { targets.Dispose(); hulks.Dispose(); players.Dispose(); }
        }
    }
}
