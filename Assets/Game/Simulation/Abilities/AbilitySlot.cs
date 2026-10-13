using Game.Core;
using Unity.Entities;

namespace Game.Simulation.Abilities
{
    /// <summary>Implements SPEC-24 §4.3. One ability of the ship, at most 8; lifecycle writer: AbilitySystem.
    /// Effect systems own Blocked and duration-free Active. Field names fixed by SPEC-22 and SPEC-61 Q3.</summary>
    [InternalBufferCapacity(0)]
    public struct AbilitySlot : IBufferElementData // 48 bytes
    {
        public ContentHash Ability;                 // SPEC-61
        public ushort AbilityIndex;                 // into AbilityDefsBlob
        public AbilitySlotState State;              // SPEC-22: PendingPower
        public AbilitySlotFlags Flags;              // this tick only
        public byte Charges;                        // SPEC-61
        public byte Active;                         // SPEC-61: 1 while a boost runs or the toggled shield is requested on
        public byte Blocked;                        // 1 = activation not allowed now; written only by the effect system of this slot's kind (§5.2)
        public byte Reserved;
        public float CooldownRemainingTicks;        // SPEC-61
        public float CooldownTicks;                 // SPEC-61
        public float ChargeRechargeRemainingTicks;  // > 0 while a charge is recharging
        public float PendingCost;                   // SPEC-22: E requested this tick
        public uint PendingSinceTick;
        public uint ActiveUntilTick;                // StatBoost
        public int FirstInstanceIndex;              // lowest granting BlockInstance index (ordering)
    }
}
