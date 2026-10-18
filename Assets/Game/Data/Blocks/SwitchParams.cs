namespace Game.Data.Blocks
{
    /// <summary>ADR-0119 decision 2: a block that fires its mechanism group's switch trigger when it takes damage. Unmanaged; Burst reads it.</summary>
    [System.Serializable] public struct SwitchParams
    {
        public float TriggerDamage;     // damage in one tick that throws the switch (> 0)
        public ushort Group;            // mechanism group it triggers; 0 = the group of the grid it is placed on
    }
}
