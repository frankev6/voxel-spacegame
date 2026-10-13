using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data.Blocks;
using Game.Data.Combat;
using Game.Data.Definitions;
using Game.Editor.Migration.Dto;
using UnityEngine;

namespace Game.Editor.Migration.Converters
{
    /// <summary>SPEC-72 MIG-009: canonical legacy identity, SPEC-21 §4.9 tuning, and protected mount/sound links.</summary>
    public sealed class WeaponConverter : ILegacyConverter
    {
        public string Name => "Weapons";
        public int Order => 220;

        public void Convert(LegacyMigrationContext context)
        {
            foreach (var file in context.Index.OfType("WeaponBlockItem"))
            {
                var dto = LegacyJson.Read<LegacyWeaponBlockItemDto>(file, context.Report);
                if (dto == null) continue;
                if (!context.IdMap.TryMap(LegacyIdKind.Weapon, dto.RegistryKey, out string id) || id == null || !Rows.TryGetValue(id, out var values) ||
                    !context.IdMap.TryMap(LegacyIdKind.Block, dto.RegistryKey, out string blockId) || blockId == null)
                { context.Report.Error("MIG-E003", file.RelativePath, "RegistryKey", "Weapon key has no canonical weapon/block mapping and v2 tuning row: " + dto.RegistryKey); continue; }
                if (!context.Index.TryResolve(dto.ProjectileData, out var projectileFile) || projectileFile.Type != "Projectile")
                { context.Report.Error("MIG-E004", file.RelativePath, "ProjectileData", "Expected an exported Projectile reference: " + dto.ProjectileData.Path); continue; }
                var projectile = LegacyJson.Read<LegacyProjectileDto>(projectileFile, context.Report);
                if (projectile == null) continue;
                if (!TryDamageType(projectile.DamageType, out var damageType) ||
                    !Enum.TryParse(projectile.VisualType, false, out ProjectileVisual visual) || !Enum.IsDefined(typeof(ProjectileVisual), visual) || visual.ToString() != projectile.VisualType)
                { context.Report.Error("MIG-E021", projectileFile.RelativePath, "DamageType/VisualType", "Unknown projectile damage type or visual."); continue; }
                WeaponDelivery delivery = projectile.Speed == 0 && visual == ProjectileVisual.LaserBeam ? WeaponDelivery.Beam :
                    projectile.IsHoming ? WeaponDelivery.Missile : WeaponDelivery.Projectile;
                // SPEC-21 §4.9 explicitly retunes this canonical missile from legacy Kinetic to Explosive (Q20).
                if (id == "wpn_missile_launcher_small") damageType = DamageType.Explosive;
                if (context.Catalog == null || !context.Catalog.TryGet<BlockDefinition>(new ContentId(blockId), out var block))
                { MissingTarget(context, file, "Block", blockId); continue; }
                int mountIndex = -1, mounts = 0;
                for (int i = 0; i < (block.Modules?.Count ?? 0); i++)
                    if (block.Modules[i] is WeaponMountModule) { mountIndex = i; mounts++; }
                if (mounts != 1) { MissingTarget(context, file, "modules", "Exactly one WeaponMountModule is required on " + blockId); continue; }
                if (!context.IdMap.TryMap(LegacyIdKind.AudioCue, dto.MuzzleSoundID ?? "", out string cueId))
                { context.Report.Error("MIG-E003", file.RelativePath, "MuzzleSoundID", "Unknown fire sound key: " + dto.MuzzleSoundID); continue; }
                AudioCueDefinition cue = null;
                if (cueId != null && !context.Catalog.TryGet(new ContentId(cueId), out cue))
                { MissingTarget(context, file, "MuzzleSoundID", cueId); continue; }
                try
                {
                    var weapon = context.Writer.CreateOrUpdate<WeaponDefinition>("Weapons", id, so =>
                    {
                        var writer = context.Writer;
                        writer.Set(so, "displayName", p => p.stringValue = dto.ItemName);
                        writer.Set(so, "delivery", p => p.intValue = (int)delivery);
                        writer.Set(so, "damageType", p => p.intValue = (int)damageType);
                        writer.Set(so, "damage", p => p.floatValue = values.Damage);
                        writer.Set(so, "penetrationCells", p => p.floatValue = values.Penetration);
                        writer.Set(so, "explosionRadius", p => p.floatValue = values.ExplosionRadius);
                        writer.Set(so, "explosionDamage", p => p.floatValue = values.ExplosionDamage);
                        writer.Set(so, "explodeOnExpire", p => p.boolValue = values.Fuse);
                        writer.Set(so, "impulsePerHit", p => p.floatValue = values.Damage * 2);
                        writer.Set(so, "fireRate", p => p.floatValue = values.Rate);
                        writer.Set(so, "burstCount", p => p.intValue = values.Burst);
                        writer.Set(so, "burstInterval", p => p.floatValue = values.BurstInterval);
                        writer.Set(so, "pelletsPerRound", p => p.intValue = values.Pellets);
                        writer.Set(so, "spreadDeg", p => p.floatValue = values.Spread);
                        writer.Set(so, "projectileSpeed", p => p.floatValue = values.Speed);
                        writer.Set(so, "projectileRadius", p => p.floatValue = values.Radius);
                        writer.Set(so, "lifetimeMin", p => p.floatValue = values.LifeMin);
                        writer.Set(so, "lifetimeMax", p => p.floatValue = values.LifeMax);
                        writer.Set(so, "homingTurnRateDeg", p => p.floatValue = values.Homing);
                        writer.Set(so, "beamRange", p => p.floatValue = values.BeamRange);
                        writer.Set(so, "beamPulseSeconds", p => p.floatValue = values.BeamPulse);
                        writer.Set(so, "beamWidth", p => p.floatValue = values.BeamWidth);
                        writer.Set(so, "traverseHalfArcDeg", p => p.floatValue = values.Arc);
                        writer.Set(so, "traverseRateDeg", p => p.floatValue = values.Traverse);
                        writer.Set(so, "maxElevationDeg", p => p.floatValue = 20);
                        writer.Set(so, "fireConeDeg", p => p.floatValue = 3);
                        writer.Set(so, "powerDraw", p => p.floatValue = values.Draw);
                        writer.Set(so, "defaultFireGroups", p => p.intValue = (int)values.Groups);
                        writer.Set(so, "visual", p => p.intValue = (int)visual);
                        writer.Set(so, "tracerColor", p => p.colorValue = Color.white);
                        writer.Set(so, "fireCue", p => p.objectReferenceValue = cue);
                        writer.Set(so, "muzzleFx", p => p.objectReferenceValue = null);
                        writer.Set(so, "hitFx", p => p.objectReferenceValue = null);
                        writer.Set(so, "deliveryData", p => p.managedReferenceValue = null);
                    }, out _);
                    if (weapon == null) continue; // Writer already reports type collisions.
                    // A pre-existing tuned weapon can still receive its missing sound without adopting its other fields.
                    if (cue != null) context.Writer.SetLinkIfEmpty(weapon, "fireCue", cue);
                    context.Writer.SetLinkIfEmpty(block, $"modules.Array.data[{mountIndex}].weapon", weapon);
                    context.Report.Info("MIG-I091", file.RelativePath, "", "Legacy numeric weapon/projectile tuning replaced by SPEC-21 §4.9. Block geometry/item fields belong to MIG-006; disableInputTrigger and MuzzlePoint are dropped there.");
                }
                catch (Exception e) { context.Report.Error("MIG-E099", file.RelativePath, "", "Weapon conversion failed: " + e.Message); }
            }
        }

        static void MissingTarget(LegacyMigrationContext context, LegacyFile file, string field, string target)
            => context.Report.Error("MIG-E062", file.RelativePath, field, "Required v2 weapon migration target is unavailable: " + target);
        static bool TryDamageType(string name, out DamageType type)
        {
            type = name switch { "Kinetic" => DamageType.Kinetic, "Energy" => DamageType.Energy, "HighExplosive" => DamageType.Explosive, _ => DamageType.Ram };
            return type != DamageType.Ram;
        }

        // Exact SPEC-21 §4.9 values. Unused beam speed/life/range fields are explicitly zero (Q21).
        sealed class Values
        {
            public float Damage, Penetration = 1, ExplosionRadius, ExplosionDamage;
            public bool Fuse;
            public float Rate, BurstInterval, Spread, Speed, Radius = .1f, LifeMin, LifeMax, Homing;
            public int Burst = 1, Pellets = 1;
            public float BeamRange, BeamPulse = .5f, BeamWidth = .3f, Arc, Traverse, Draw;
            public WeaponFireGroups Groups = WeaponFireGroups.Group1;
        }
        static readonly IReadOnlyDictionary<string, Values> Rows = new Dictionary<string, Values>(StringComparer.Ordinal)
        {
            ["wpn_plasma_turret_small"] = new Values { Damage = 60, Rate = 2, Spread = 2, Speed = 400, LifeMin = 1.5f, LifeMax = 1.5f, Arc = 5, Traverse = 360, Draw = 1.5f },
            ["wpn_plasma_turret_small_fixed"] = new Values { Damage = 60, Rate = 2.5f, Spread = .5f, Speed = 400, LifeMin = 1.5f, LifeMax = 1.5f, Draw = 1.5f },
            ["wpn_plasma_turret_medium"] = new Values { Damage = 70, Penetration = 2, Rate = 1.5f, Burst = 3, BurstInterval = .05f, Spread = .5f, Speed = 450, LifeMin = 1.6f, LifeMax = 1.6f, Arc = 90, Traverse = 180, Draw = 4 },
            ["wpn_small_beam_turret"] = new Values { Damage = 150, Rate = .6f, BeamRange = 450, Arc = 90, Traverse = 180, Draw = 4, Groups = WeaponFireGroups.Group2 },
            ["wpn_fixed_beam_turret_small"] = new Values { Damage = 180, Rate = .6f, BeamRange = 450, Draw = 4, Groups = WeaponFireGroups.Group2 },
            ["wpn_railgun_medium"] = new Values { Damage = 250, Penetration = 6, Rate = .3f, Burst = 4, BurstInterval = .1f, Spread = .5f, Speed = 1200, LifeMin = 1, LifeMax = 1, Arc = 30, Traverse = 90, Draw = 8 },
            ["wpn_ark_lance_medium"] = new Values { Damage = 900, Penetration = 2, Rate = .1f, BeamRange = 900, BeamPulse = 1, BeamWidth = .6f, Arc = 35, Traverse = 60, Draw = 10, Groups = WeaponFireGroups.Group2 },
            ["wpn_flak_cannon_medium"] = new Values { ExplosionRadius = 2.5f, ExplosionDamage = 60, Fuse = true, Rate = 1, Pellets = 8, Spread = 6, Speed = 300, Radius = .2f, LifeMin = .5f, LifeMax = 1.5f, Draw = 5, Groups = WeaponFireGroups.Group3 },
            ["wpn_missile_launcher_small"] = new Values { ExplosionRadius = 3, ExplosionDamage = 200, Fuse = true, Rate = .3f, Burst = 6, BurstInterval = .05f, Spread = 2, Speed = 180, Radius = .3f, LifeMin = 5, LifeMax = 5, Homing = 90, Draw = 2, Groups = WeaponFireGroups.Group3 },
        };
    }
}
