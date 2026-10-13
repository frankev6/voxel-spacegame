using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Game.Simulation.Weapons
{
    /// <summary>SPEC-21 §5.5: stable chunk order, then within-chunk firing order.</summary>
    [BurstCompile] internal struct ShotFlattenJob : IJob
    {
        [ReadOnly] public NativeStream.Reader Reader;
        public NativeList<WeaponShot> Shots;
        public void Execute()
        {
            Shots.Clear();
            for (int i = 0; i < Reader.ForEachCount; i++)
            {
                int count = Reader.BeginForEachIndex(i);
                for (int n = 0; n < count; n++) Shots.Add(Reader.Read<WeaponShot>());
                Reader.EndForEachIndex();
            }
        }
    }
}
