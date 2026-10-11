using Game.Data.Combat;

namespace Game.Voxel.Damage
{
    // DamageType is designed here but lives in Assets/Game/Data/Combat/DamageType.cs (namespace Game.Data.Combat, architecture §3.2)
    // so content definitions use it directly (no mirror enum):
    //   /// <summary>Binding (architecture §6.2). Add values at the end only.</summary>
    //   public enum DamageType : byte { Kinetic = 0, Energy = 1, Explosive = 2, Ram = 3 }

    // Who produced an event (part of the sort key, §3 decision 3) is Game.Data.Combat.ProducerKind (SPEC-02 §4.12), shared by every channel.
    [System.Flags] public enum DamageEventFlags : byte { None = 0, NoImpulse = 1, NoKick = 2 }
}
