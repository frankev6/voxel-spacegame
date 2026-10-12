using Game.Data.Blocks;
using Game.Data.Combat;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Voxel.Damage.Jobs
{
    internal partial struct CellWriter
    {
        public BlobAssetReference<OrientationTablesBlob> Orientations;
        public BlobAssetReference<BlockModulesBlob> Modules;
        public NativeStream.Writer Explosions;
        public bool CoreLost;
        public float3 CoreWorldPos;
        void CheckInstance(Cell cell)
        {
            if (!cell.IsInstance) return;
            int index = cell.InstanceIndex; var instance = Access.Instances[index];
            if ((instance.Flags & BlockInstanceFlags.Destroyed) != 0) return;
            ref var def = ref Access.Blocks.Value.Blocks[instance.Block];
            int count = def.Cells.Length;
            bool lost = instance.CellsAlive == 0 || (def.FunctionHp > 0 && instance.FunctionHp <= 0) ||
                (count >= 2 && count - instance.CellsAlive >= (int)math.ceil(Config.InstanceDestroyCellFraction * count));
            if (!lost) return;
            // Mark first, so collapsing the rest of a footprint cannot recursively collapse it again.
            instance.Flags |= BlockInstanceFlags.Destroyed; Access.Instances[index] = instance;
            for (int i = 0; i < count; i++)
            {
                int3 c = instance.Origin + Orientations.Value.Rotate(def.Cells[i], instance.Orient);
                if (Access.TryGetCell(c, out var other, out _, out _) && other.Ref == cell.Ref) RemoveCell(c, CellRemovedReason.Destroyed);
            }
            instance = Access.Instances[index]; instance.FunctionHp = 0; instance.CellsAlive = 0; Access.Instances[index] = instance;
            Stats.InstancesDestroyed++; Changed = true;
            if (instance.Functional != Entity.Null) DestroyList.Add(instance.Functional);
            if (Modules.Value.TryGet(BlockModuleId.Explosive, instance.Block, out ExplosiveParams explosive) && explosive.BlastDamage > 0)
                Explosions.Write(new ExplosionRequest
                {
                    WorldPos = Transform.TransformPoint(((float3)instance.Origin + .5f) * Access.Definition.CellSize),
                    Radius = explosive.BlastRadiusCells * Access.Definition.CellSize, Amount = explosive.BlastDamage,
                    Type = DamageType.Explosive, Source = Access.Grid, Instigator = Event.Instigator == Entity.Null ? Event.Source : Event.Instigator,
                    Producer = ProducerKind.Explosion, Sequence = (uint)index
                });
            if (Access.Definition.AnchorInstance == index)
            {
                CoreLost = true; CoreWorldPos = Transform.TransformPoint(((float3)instance.Origin + .5f) * Access.Definition.CellSize);
                var definition = Access.Definition; definition.AnchorInstance = -1; Access.Definition = definition;
            }
        }
    }
}
