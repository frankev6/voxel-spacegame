using Game.Data.Combat;
using Unity.Entities;

namespace Game.Voxel.Damage
{
    public struct DamageTableBlob
    {
        public BlobArray<DamageTypeRow> Types;   // index = (int)DamageType
        public BlobArray<float> ArmorMultipliers; // Types[t].ArmorOffset + armorClass
        public int ArmorClassCount;
    }
}
