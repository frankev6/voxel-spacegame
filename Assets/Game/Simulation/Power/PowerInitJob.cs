using Game.Data.Blocks;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Game.Simulation.Power
{
    /// <summary>SPEC-22 §5.2 step 2: one worker owns each root and all of its functional consumer bindings.</summary>
    [BurstCompile, WithNone(typeof(GridSpawnRequest))]
    internal partial struct PowerInitJob : IJobEntity
    {
        public BlobAssetReference<BlockModulesBlob> Modules;
        // Each functional belongs to exactly one grid; each grid is processed by one worker (SPEC-22 §5.2).
        [NativeDisableParallelForRestriction] public BufferLookup<PowerRequest> Requests;
        // Same disjoint ownership as Requests; no two root jobs write the same functional's link.
        [NativeDisableParallelForRestriction] public ComponentLookup<PowerLink> Links;
        void Execute(ref ShipPower power, DynamicBuffer<PowerConsumer> consumers, in DynamicBuffer<BlockInstance> instances,
            ref ShieldState shield, DynamicBuffer<ShieldRipple> ripples, ref ShieldControl control, ref ShipStats stats)
        {
            if ((power.Flags & ShipPowerFlags.Initialized) != 0) return;
            using var sorted = new NativeList<PowerConsumer>(Allocator.Temp);
            float capacity = 0, generation = 0;
            for (int i = 0; i < instances.Length; i++)
            {
                var instance = instances[i];
                if (instance.Block == 0) continue;
                if ((instance.Flags & BlockInstanceFlags.Destroyed) == 0)
                {
                    if (Modules.Value.TryGet(BlockModuleId.PowerStorage, instance.Block, out PowerStorageParams storage)) capacity += storage.Storage;
                    if (Modules.Value.TryGet(BlockModuleId.PowerSource, instance.Block, out PowerSourceParams source)) generation += source.Generation;
                }
                if (!Requests.HasBuffer(instance.Functional)) continue;
                var requests = Requests[instance.Functional];
                for (int k = 0; k < requests.Length; k++)
                {
                    var request = requests[k]; request.ConsumerIndex = -1; requests[k] = request;
                    sorted.Add(new PowerConsumer { Functional = instance.Functional, InstanceIndex = i, Module = request.Module,
                        Priority = request.Priority, RequestIndex = (byte)k, Draw = request.Draw });
                }
            }
            sorted.Sort(new ConsumerOrder()); consumers.Clear(); consumers.AddRange(sorted.AsArray());
            for (int i = 0; i < instances.Length; i++)
            {
                Entity functional = instances[i].Functional;
                if (instances[i].Block == 0 || !Links.HasComponent(functional)) continue;
                Links[functional] = new PowerLink { Bound = 1 };
            }
            for (int j = 0; j < consumers.Length; j++)
            {
                var consumer = consumers[j]; var requests = Requests[consumer.Functional];
                var request = requests[consumer.RequestIndex]; request.ConsumerIndex = (short)j; requests[consumer.RequestIndex] = request;
                if (Links.HasComponent(consumer.Functional))
                { var link = Links[consumer.Functional]; link.ConsumerCount++; Links[consumer.Functional] = link; }
            }
            power = new ShipPower { Stored = capacity, Capacity = capacity, Generation = generation, Flags = ShipPowerFlags.Initialized };
            shield.Flags = ShieldFlags.FillOnNextUpdate;
            if (ripples.Length == 0)
            {
                ripples.ResizeUninitialized(8);
                for (int i = 0; i < ripples.Length; i++) ripples[i] = default;
                control.RequestedOn = 1;
            }
            stats.Flags &= ~ShipStatsFlags.Initialized;
        }
        struct ConsumerOrder : System.Collections.Generic.IComparer<PowerConsumer>
        {
            public int Compare(PowerConsumer a, PowerConsumer b)
            {
                int c = b.Priority.CompareTo(a.Priority); if (c != 0) return c;
                c = a.InstanceIndex.CompareTo(b.InstanceIndex); return c != 0 ? c : ((byte)a.Module).CompareTo((byte)b.Module);
            }
        }
    }
}
