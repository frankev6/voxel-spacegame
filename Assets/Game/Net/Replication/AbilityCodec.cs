using Game.Net.Protocol;
using Game.Simulation.Abilities;
using Game.Simulation.Power;
using Unity.Entities;

namespace Game.Net.Replication
{
    /// <summary>ADR-0117 NET-016: one ship's ability slots on the wire. The host sends them when a decision changed them (activation,
    /// resync, a power grant or denial); timers run on both machines through AbilityLifecycle. The guest only translates them into
    /// AbilityHostSlot for AbilitySlotApplySystem. Main thread.</summary>
    public static class AbilityCodec
    {
        /// <summary>Writes the slots of <paramref name="root"/> and returns a signature of their decided state (not their timers), or
        /// false when the ship has no slot buffer.</summary>
        public static bool Write(EntityManager em, Entity root, ulong id, uint tick, NetWriter w, out ulong signature)
        {
            signature = 0;
            if (!em.HasBuffer<AbilitySlot>(root)) return false;
            var slots = em.GetBuffer<AbilitySlot>(root, true);
            byte shield = em.HasComponent<ShieldControl>(root) ? em.GetComponentData<ShieldControl>(root).RequestedOn : (byte)2;
            ulong h = 1469598103934665603UL;
            void Mix(ulong v) { h = (h ^ v) * 1099511628211UL; }
            Mix(shield); Mix((ulong)slots.Length);
            w.U64(id); w.U8(shield); w.U8((byte)slots.Length);
            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                Mix(s.AbilityIndex); Mix((ulong)s.State); Mix(s.Charges); Mix(s.Active); Mix(s.ActivationCount);
                w.U64(s.Ability.Value); w.U16(s.AbilityIndex); w.U8((byte)s.State); w.U8(s.Charges); w.U8(s.Active); w.U8(s.Blocked);
                w.U8(s.ActivationCount); w.F32(s.CooldownRemainingTicks); w.F32(s.CooldownTicks); w.F32(s.ChargeRechargeRemainingTicks);
                w.U32(s.ActiveUntilTick > tick ? s.ActiveUntilTick - tick : 0); w.I32(s.FirstInstanceIndex);
            }
            signature = h | 1;
            return true;
        }

        public static ulong ReadId(NetReader r) => r.U64();

        /// <summary>Reads the rest of a message whose id <see cref="ReadId"/> took, into <paramref name="root"/>'s AbilityHostSlot for
        /// the next fixed tick.</summary>
        public static void Read(EntityManager em, Entity root, NetReader r)
        {
            byte shield = r.U8(); int count = r.U8();
            if (!em.HasBuffer<AbilityHostSlot>(root) || !em.HasComponent<AbilityHostState>(root)) return;   // AbilityGridSetup adds both
            var slots = em.GetBuffer<AbilityHostSlot>(root); slots.Clear();
            for (int i = 0; i < count; i++)
            {
                var s = new AbilitySlot
                {
                    Ability = new Game.Core.ContentHash(r.U64()), AbilityIndex = r.U16(), State = (AbilitySlotState)r.U8(), Charges = r.U8(),
                    Active = r.U8(), Blocked = r.U8(), ActivationCount = r.U8(), CooldownRemainingTicks = r.F32(), CooldownTicks = r.F32(),
                    ChargeRechargeRemainingTicks = r.F32(), ActiveUntilTick = r.U32(), FirstInstanceIndex = r.I32()
                };
                slots.Add(new AbilityHostSlot { Value = s });
            }
            em.SetComponentData(root, new AbilityHostState { Pending = 1, ShieldRequestedOn = shield });
        }
    }
}
