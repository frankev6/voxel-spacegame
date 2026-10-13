namespace Game.Data.Definitions
{
    /// <summary>Power and shield tunables. Exactly one asset (`tuning_power`) in the database. Implements SPEC-22 §4.5.</summary>
    [ContentIdPrefix("tuning_")]
    [UnityEngine.CreateAssetMenu(menuName = "Game/Content/Tuning/Power", fileName = "tuning_power")]
    public sealed class PowerTuningDefinition : ContentDefinition
    {
        [UnityEngine.SerializeField] private byte priorityShieldEmitter = 200;
        [UnityEngine.SerializeField] private byte priorityThruster = 150;
        [UnityEngine.SerializeField] private byte priorityAbility = 120;
        [UnityEngine.SerializeField] private byte priorityWeapon = 100;
        [UnityEngine.SerializeField, UnityEngine.Min(1)] private int brownoutHoldTicks = 30;
        [UnityEngine.SerializeField, UnityEngine.Min(1f)] private float shieldPaddingScale = 1.2f;     // × half extents
        [UnityEngine.SerializeField, UnityEngine.Min(0f)] private float shieldPaddingMetres = 2f;      // + per axis
        [UnityEngine.SerializeField, UnityEngine.Min(0.5f)] private float shieldMinRadiusMetres = 3f;
        [UnityEngine.SerializeField, UnityEngine.Range(0f, 1f)] private float shieldRestartFraction = 0.25f;
        [UnityEngine.SerializeField] private float shieldMultKinetic = 0.8f, shieldMultEnergy = 1.25f, shieldMultExplosive = 1f, shieldMultRam = 1f;
        [UnityEngine.SerializeField, UnityEngine.Min(1f)] private float sustainWarningSeconds = 30f;

        public byte PriorityShieldEmitter => priorityShieldEmitter;
        public byte PriorityThruster => priorityThruster;
        public byte PriorityAbility => priorityAbility;
        public byte PriorityWeapon => priorityWeapon;
        public int BrownoutHoldTicks => brownoutHoldTicks;
        public float ShieldPaddingScale => shieldPaddingScale;
        public float ShieldPaddingMetres => shieldPaddingMetres;
        public float ShieldMinRadiusMetres => shieldMinRadiusMetres;
        public float ShieldRestartFraction => shieldRestartFraction;
        public float ShieldMultKinetic => shieldMultKinetic;
        public float ShieldMultEnergy => shieldMultEnergy;
        public float ShieldMultExplosive => shieldMultExplosive;
        public float ShieldMultRam => shieldMultRam;
        public float SustainWarningSeconds => sustainWarningSeconds;
    }
}
