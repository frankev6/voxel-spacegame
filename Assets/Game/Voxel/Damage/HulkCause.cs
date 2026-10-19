namespace Game.Voxel.Damage
{
    public enum HulkCause : byte { CoreLost = 0, Split = 1, CoreMalfunction = 2 }   // CoreMalfunction: live cells below the core's floor (CoreParams.Floor)
}
