using Unity.Entities;
using Unity.Collections;

namespace Game.Simulation
{
    /// <summary>Implements SPEC-21 weapon contract: FactionRelations.</summary>
    public struct FactionRelations : IComponentData
    {
        public NativeArray<byte> Stance;     // Count × Count, [a * Count + b], FactionStance values; diagonal = Friendly
        public int Count;                    // ≤ 64
        public FactionStance Get(byte a, byte b) =>
            a >= Count || b >= Count ? FactionStance.Neutral : (FactionStance)Stance[a * Count + b];
        public bool IsHostile(in Allegiance a, in Allegiance b) =>
            a.Faction != 255 && b.Faction != 255 && a.Faction != b.Faction && Get(a.Faction, b.Faction) == FactionStance.Hostile;
        public static FactionRelations CreateDefault(Allocator allocator)
        {
            var result = new FactionRelations { Count = 3, Stance = new NativeArray<byte>(9, allocator) };
            for (int i = 0; i < result.Count; i++)
                result.Stance[i * result.Count + i] = (byte)FactionStance.Friendly;
            result.Stance[1] = result.Stance[3] = (byte)FactionStance.Hostile;
            return result;
        }   // 3×3: 0↔1 Hostile, 2 Neutral to all, diagonal Friendly
        public void Dispose()
        {
            if (Stance.IsCreated) Stance.Dispose();
            Stance = default;
            Count = 0;
        }
    }
}
