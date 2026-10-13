using System;
using System.Collections.Generic;
using Game.Data.Blocks;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Editor.Migration.Converters
{
    /// <summary>Independent of the export: the binding SPEC-04 §4.10 rows. Module instances are fresh for each asset.</summary>
    internal sealed class LegacyBlockValues
    {
        internal string Id, Mesh;
        internal BlockKind Kind;
        internal BlockShape Shape;
        internal Vector3Int[] Cells, Clearance;
        internal float CellHp, FunctionHp, Mass;
        internal BlockCategory Category;
        internal uint Orientations;
        internal BlockFaceMask Attach;
        internal Func<BlockModule[]> MakeModules;
        internal static readonly IReadOnlyDictionary<string, LegacyBlockValues> All = Build();
        static IReadOnlyDictionary<string, LegacyBlockValues> Build()
        {
            var rows = new Dictionary<string, LegacyBlockValues>(StringComparer.Ordinal);
            void Add(string id, BlockKind kind, float hp, float mass, BlockCategory category, Vector3Int[] cells, Vector3Int[] clearance, string mesh, Func<BlockModule[]> modules = null, bool horizontal = false, BlockShape shape = BlockShape.Cube)
            {
                rows.Add(id, new LegacyBlockValues { Id = id, Kind = kind, Shape = shape, CellHp = hp, FunctionHp = kind == BlockKind.Functional ? hp * cells.Length : 0, Mass = mass,
                    Category = category, Cells = cells, Clearance = clearance ?? Array.Empty<Vector3Int>(), Mesh = mesh, MakeModules = modules ?? (() => Array.Empty<BlockModule>()),
                    Orientations = horizontal ? OrientationMask.HorizontalForwardBits : OrientationMask.AllBits, Attach = horizontal ? BlockFaceMask.All & ~BlockFaceMask.PosZ : BlockFaceMask.All });
            }
            Add("block_hull", BlockKind.Structural, 100, 25, BlockCategory.Structure, Box(1, 1, 1), null, null);
            Add("block_hull_angled", BlockKind.Structural, 50, 15, BlockCategory.Structure, Box(1, 1, 1), null, null, shape: BlockShape.Wedge);
            Add("block_core_small", BlockKind.Functional, 300, 60, BlockCategory.Core, Box(1, 1, 1), null, "shipVoxels/hull_1x1_Cube.008", () => Core(4000, 100));
            Add("block_core_cross", BlockKind.Functional, 500, 60, BlockCategory.Core,
                new[] { V(0, 0, 0), V(0, 1, 0), V(0, 2, 0), V(1, 1, 0), V(-1, 1, 0), V(0, 1, 1), V(0, 1, -1) }, null,
                "hulls/cross_3x3_hull_cross_3x3_hull", () => Core(8000, 150));
            Add("block_capacitor_small", BlockKind.Functional, 100, 35, BlockCategory.Power, Box(2, 2, 1), null,
                "shipVoxels/capacitor_2x1_base_1x1_hull.002", () => new BlockModule[] { new PowerStorageModule { Params = new PowerStorageParams { Storage = 40 } } });
            Add("block_shield_small", BlockKind.Functional, 100, 40, BlockCategory.Shield, Box(1, 1, 1), null,
                "hulls/base_1x1_hull_base_1x1_hull", () => new BlockModule[] { new ShieldEmitterModule { Params = new ShieldEmitterParams { Capacity = 3000, RegenPerSecond = 120, RechargeDelaySeconds = 4, PowerDraw = 8 } } });
            Add("block_thruster_small", BlockKind.Functional, 100, 30, BlockCategory.Propulsion, Box(1, 1, 2), Exhaust(1, 2, 4),
                "shipVoxels/thruster1x2_Cube.004", () => Thruster(20000), true);
            Add("block_thruster_medium", BlockKind.Functional, 100, 30, BlockCategory.Propulsion,
                new[] { V(0, 0, 0), V(1, 0, 0), V(2, 0, 0), V(1, 1, 0), V(1, -1, 0), V(0, 0, 1), V(1, 0, 1), V(2, 0, 1), V(1, 1, 1), V(1, -1, 1) }, null,
                "shipVoxels/thruster3x3x2.001_Cube.007", () => Thruster(40000), true);
            Add("block_thruster_large", BlockKind.Functional, 100, 30, BlockCategory.Propulsion, Box(2, 1, 2), Exhaust(2, 2, 4),
                "hulls/base_2x2_thruster_base_2x2_thruster", () => Thruster(40000), true);
            Add("block_portal_small", BlockKind.Prop, 150, 30, BlockCategory.Detail, Box(1, 1, 1), new[] { V(0, 0, 2) }, "hulls/base_1x1_hull_base_1x1_hull", horizontal: true);
            foreach (string id in new[] { "block_prismatic_mirror_small", "block_prismatic_mirror_fixed_small" })
                Add(id, BlockKind.Prop, 100, 30, BlockCategory.Detail, Box(1, 1, 1), new[] { V(0, 0, 1) }, "hulls/base_1x1_hull_base_1x1_hull", horizontal: true);
            void Weapon(string id, float hp, int x, int y, int z, Vector3Int[] clearance, string mesh)
                => Add(id, BlockKind.Functional, hp, 30, BlockCategory.Weapon, Box(x, y, z), clearance, "shipVoxels/" + mesh,
                    () => new BlockModule[] { new WeaponMountModule { MuzzleOffsetCells = new float3((x - 1) * .5f, (y - 1) * .5f, z - .5f) } }, true);
            Weapon("weapon_plasma_turret_small", 100, 1, 1, 2, Exhaust(1, 2, 4), "gun_turret_1x2_base_1x2_gun.001");
            Weapon("weapon_plasma_turret_small_fixed", 150, 1, 1, 2, Exhaust(1, 2, 4), "gun_turret_1x2_base_1x2_gun.001");
            Weapon("weapon_plasma_turret_medium", 100, 2, 1, 2, Exhaust(2, 3, 4), "gun_turret_2x1x3_Cube.005");
            Weapon("weapon_small_beam_turret", 100, 1, 1, 2, Exhaust(1, 2, 4), "beam_turret_1x2_Cube.002");
            Weapon("weapon_fixed_beam_turret_small", 200, 1, 1, 2, Exhaust(1, 2, 3), "beam_turret_1x2_Cube.002");
            Weapon("weapon_railgun_medium", 200, 1, 1, 2, Exhaust(1, 2, 4), "beam_turret_1x2_Cube.002");
            Weapon("weapon_ark_lance_medium", 200, 1, 2, 3, Exhaust(1, 3, 4), "gun_turret_2x1x3_Cube.005");
            Weapon("weapon_flak_cannon_medium", 200, 3, 1, 3, Exhaust(3, 4, 4), "flak_cannon_3x3x3_Cube");
            Weapon("weapon_missile_launcher_small", 200, 2, 1, 2, Exhaust(2, 3, 4), "missile_launcher_2x1_Cube.006");
            return rows;
        }
        static BlockModule[] Core(int cells, int extent) => new BlockModule[] {
            new CoreModule { Params = new CoreParams { MaxCells = cells, MaxExtentCells = extent } },
            new PowerSourceModule { Params = new PowerSourceParams { Generation = 40 } },
            new PowerStorageModule { Params = new PowerStorageParams { Storage = 200 } } };
        static BlockModule[] Thruster(float force) => new BlockModule[] { new ThrusterModule { Params = new ThrusterParams { ThrustNewtons = force, PowerDraw = 0 } } };
        static Vector3Int V(int x, int y, int z) => new(x, y, z);
        static Vector3Int[] Box(int sx, int sy, int sz)
        {
            var cells = new List<Vector3Int>();
            for (int z = 0; z < sz; z++) for (int y = 0; y < sy; y++) for (int x = 0; x < sx; x++) cells.Add(V(x, y, z));
            return cells.ToArray();
        }
        static Vector3Int[] Exhaust(int sx, int startZ, int endZ)
        {
            var cells = new List<Vector3Int>();
            for (int z = startZ; z <= endZ; z++) for (int x = 0; x < sx; x++) cells.Add(V(x, 0, z));
            return cells.ToArray();
        }
    }
}
