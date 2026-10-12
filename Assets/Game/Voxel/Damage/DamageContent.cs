using System;
using Game.Data.Definitions;
using Game.Data.Combat;
using Game.Data.Services;
using Unity.Collections;
using Unity.Entities;

namespace Game.Voxel.Damage
{
    /// <summary>SPEC-13 section 4.7: Game.App builds/owns this table and sets DamageConfig at boot.</summary>
    public static class DamageContent
    {
        public static BlobAssetReference<DamageTableBlob> BuildTable(ContentCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var types = catalog.All<DamageTypeDefinition>();
            var armors = catalog.All<ArmorClassDefinition>();
            if (types.Count < Enum.GetValues(typeof(DamageType)).Length || armors.Count == 0)
                throw new ArgumentException("Every declared damage type and at least one armor class need definitions.", nameof(catalog));
            // Validate shape before allocating native memory, even for callers outside the normal validated boot path.
            var seenTypes = new bool[types.Count]; var seenArmors = new bool[armors.Count];
            foreach (var armor in armors)
            {
                if (armor.Index >= seenArmors.Length || seenArmors[armor.Index]) throw new ArgumentException("Armor indices must be unique and contiguous.", nameof(catalog));
                seenArmors[armor.Index] = true;
            }
            foreach (var type in types)
            {
                int index = (byte)type.Type;
                if (index >= seenTypes.Length || seenTypes[index] || type.ArmorMultipliers == null || type.ArmorMultipliers.Count != armors.Count)
                    throw new ArgumentException("Damage indices must be unique and contiguous, with one multiplier per armor class.", nameof(catalog));
                seenTypes[index] = true;
            }
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var table = ref builder.ConstructRoot<DamageTableBlob>();
            table.ArmorClassCount = armors.Count;
            var rows = builder.Allocate(ref table.Types, types.Count);
            var multipliers = builder.Allocate(ref table.ArmorMultipliers, types.Count * armors.Count);
            foreach (var type in types)
            {
                int index = (byte)type.Type, offset = index * armors.Count;
                rows[index] = new DamageTypeRow { ArmorOffset = offset, KickMin = type.KickMin, KickMax = type.KickMax,
                    ShieldMultiplier = type.ShieldMultiplier, BypassShields = (byte)(type.BypassShields ? 1 : 0) };
                for (int armor = 0; armor < armors.Count; armor++) multipliers[offset + armor] = type.ArmorMultipliers[armor];
            }
            return builder.CreateBlobAssetReference<DamageTableBlob>(Allocator.Persistent);
        }
    }
}
