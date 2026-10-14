namespace Game.Data.Blocks
{
    /// <summary>Identity of a module type: one append-only list (ADR-0013), at most 63 values (bit index in BlockDef.ModuleMask).
    /// A spec that adds a module appends its value here in the task that creates the module class. Implements SPEC-04 §4.6.</summary>
    public enum BlockModuleId : byte
    { None = 0, Core = 1, PowerSource = 2, PowerStorage = 3, Thruster = 4, WeaponMount = 5, ShieldEmitter = 6, Cargo = 7, Explosive = 8, SalvageMagnet = 9 }   // SalvageMagnet: QA-001
}
