using Game.Core;
using Unity.Collections;
using Unity.Entities;

namespace Game.Presentation.Components
{
    /// <summary>HUD snapshot singleton written once per frame by HudSnapshotSystem (SPEC-61 §4.5).</summary>
    public struct HudSnapshot : IComponentData
    {
        public HudFlags Flags;
        public float Hull01;
        public float ShieldHp, ShieldMax;
        public byte ShieldStatus;
        public float Energy, EnergyMax;
        public float SpeedMps;
        public FixedList64Bytes<HudWeaponGroupState> WeaponGroups;
        public FixedList128Bytes<HudAbilityState> Abilities;
        public ContentHash TargetNameHash;
        public FixedString32Bytes TargetPilot;   // co-op: the target's pilot name, shown in place of the blueprint name
        public byte TargetFaction;
        public byte TargetRelation;
        public float TargetHull01, TargetShield01, TargetDistanceM;
        public byte InteractionKind;
        public float InteractionDistanceM;
        public byte InteractionInRange;
        public ulong InteractionBodyKey;
        public ulong InteractionGridPersistentId;
        public uint StarSystemIdValue;
        public uint RadarVersion;
        public uint FrameIndex;
    }
}
