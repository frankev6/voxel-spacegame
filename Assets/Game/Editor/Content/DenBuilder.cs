using System;
using Game.Data.Services;
using Game.Voxel.Data;
using Game.Voxel.Model;
using Unity.Mathematics;

namespace Game.Editor.Content
{
    /// <summary>SPEC-93 EXT-008, the Rock (owner, 2026-11-10: "starting out with a natural looking asteroid and building around it, the
    /// player should be able to dig themselves"): the den as the crew finds it, from the agreed sketch (ArtSource/Places/sketches.py
    /// den_rock2, day one). A solid lumpy asteroid of 4 m stone cells, so digging takes real cells out; a natural cave on its east face
    /// looking at its shard across a canyon; a crack down from the top; two ore veins. Solid, not a shell: every dug cell is stone.</summary>
    internal static class DenBuilder
    {
        public const string Rock = "ship_den_rock", Shard = "ship_den_shard";
        public const float RockRadius = 130, ShardRadius = 40, Cell = 4;
        /// <summary>Den-local: the cave's mouth (east), the cave's centre, the shard's centre.</summary>
        public static readonly float3 CaveMouth = new float3(118, 0, 6), CaveCentre = new float3(92, 0, 4), ShardCentre = new float3(240, 10, 30);

        internal static int Build(ContentService service, VoxelContent content, Func<string, ushort> block, LanternBuilder.Maker make)
        {
            ushort rock = block("block_rock");
            int cells = make(service, content, Rock, "The Rock", Cell, put => Stone(put, rock, RockRadius, .9f, 21, true));
            cells += make(service, content, Shard, "The Rock's shard", Cell, put => Stone(put, rock, ShardRadius, .85f, 5, false));
            return cells;
        }

        static float Field(float3 p, float r, float squash, int seed)
        {
            float3 q = p / new float3(r, r * squash, r);
            float bump = .16f * math.sin(p.x * .045f + seed * 1.7f) * math.cos(p.z * .038f - seed)
                       + .10f * math.sin(p.y * .07f + p.x * .025f + seed * 2.3f)
                       + .05f * math.sin(p.z * .11f + p.y * .09f + seed);
            return math.length(q) - 1 - bump;
        }

        static void Stone(LanternBuilder.Put put, ushort rock, float r, float squash, int seed, bool den)
        {
            int n = (int)math.ceil(r * 1.25f / Cell);
            for (int x = -n; x <= n; x++) for (int y = -n; y <= n; y++) for (int z = -n; z <= n; z++)
            {
                var c = (new float3(x, y, z) + .5f) * Cell;
                if (Field(c, r, squash, seed) > 0) continue;
                if (den && Carved(c)) continue;
                uint h = math.hash(new int4(x, y, z, seed));
                byte colour = (byte)(77 + h % 3);
                if (den && Ore(c)) colour = 80;
                put(rock, new int3(x, y, z), colour, 0);
            }
        }

        /// <summary>The natural cave (a lumpy hollow behind the east face, open to it) and the crack from the top.</summary>
        static bool Carved(float3 c)
        {
            float3 d = c - CaveCentre; d.y *= 1.4f;
            float wobble = 4 * math.sin(c.z * .2f) + 3 * math.cos(c.y * .25f);
            if (math.length(d) < 34 + wobble) return true;
            if (c.x > CaveCentre.x && math.abs(c.y) < 18 + wobble * .5f && math.abs(c.z - CaveCentre.z) < 22) return true;   // the mouth
            // the crack: a narrow slot from the crown down toward the cave
            float t = math.saturate((c.y - 10) / 110);
            float cx = math.lerp(70, 20, t), cz = math.lerp(0, -10, t);
            return c.y > 10 && math.abs(c.x - cx) < 5 && math.abs(c.z - cz) < 16;
        }

        static bool Ore(float3 c)
        {
            float a = math.abs(c.y * .6f + c.x * .3f - 20 + 8 * math.sin(c.z * .05f));
            float b = math.abs(c.z * .5f - c.x * .4f + 30 + 6 * math.cos(c.y * .06f));
            return a < 5 || b < 4;
        }
    }
}
