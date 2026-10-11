using System;
using Game.Core;
using Game.Core.Components;
using Game.Data.Definitions;
using Unity.Entities;

namespace Game.App.Session
{
    /// <summary>Campaign identity allocation (SPEC-01 §5.5, §5.8). FND-009 adds flow and save-context behaviour.</summary>
    public sealed partial class GameSession : IPersistentIdAllocator
    {
        readonly World world;
        internal readonly int PersistentIdBlockSize, PersistentIdRefillThreshold;
        public bool HasActiveSession { get; private set; }
        public ulong NextPersistentId { get; private set; } = 1;

        // FND-009's composition constructor will delegate to this allocation initializer.
        internal GameSession(World world, GameConfigDefinition config)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            if (config == null) throw new ArgumentNullException(nameof(config));
            PersistentIdBlockSize = config.PersistentIdBlockSize;
            PersistentIdRefillThreshold = config.PersistentIdRefillThreshold;
            if (PersistentIdBlockSize < 256 || PersistentIdBlockSize > 65536 ||
                PersistentIdRefillThreshold < 0 || PersistentIdRefillThreshold >= PersistentIdBlockSize)
                throw new ArgumentOutOfRangeException(nameof(config), "Invalid persistent-id allocation limits");
        }

        /// <summary>Returns the next ungranted id; zero and ulong.MaxValue are never issued.</summary>
        public ulong Allocate()
        {
            if (NextPersistentId == ulong.MaxValue) throw new InvalidOperationException("Persistent id counter exhausted");
            return NextPersistentId++;
        }

        /// <summary>Reserves an exclusive block, advancing the saved counter past every id Burst can issue.</summary>
        internal ulong GrantBlock(int count)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            if ((ulong)count > ulong.MaxValue - NextPersistentId) throw new InvalidOperationException("Persistent id counter exhausted");
            ulong start = NextPersistentId;
            NextPersistentId += (ulong)count;
            return start;
        }

        /// <summary>Session start/restore/end boundary: discard outstanding grants before changing the saved counter.</summary>
        internal void ResetPersistentIds(ulong next, bool active)
        {
            if (next == 0) throw new ArgumentOutOfRangeException(nameof(next));
            var em = world.EntityManager;
            em.CompleteDependencyBeforeRW<PersistentIdAllocator>();
            using var query = em.CreateEntityQuery(ComponentType.ReadWrite<PersistentIdAllocator>());
            if (!query.IsEmptyIgnoreFilter) query.SetSingleton(default(PersistentIdAllocator));
            NextPersistentId = next;
            HasActiveSession = active;
        }
    }
}
