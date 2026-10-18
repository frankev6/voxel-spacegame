using System;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Voxel.Data;
using Game.Voxel.Model;
using Unity.Mathematics;
using UnityEditor;

namespace Game.Editor.Content
{
    /// <summary>The development playground's "ARK Proving Grounds" space hub as voxel blueprints (TrackStructureBuilder's pattern):
    /// hull for the structure, Track Frame for gold and light-grey trim. Flight plane is local y = 0: what ships fly over stays below
    /// −2 m, what blocks them spans it. +z is north, +x east. A cell i spans [i, i + 1) × cell from the grid origin.</summary>
    public static class HubStructureBuilder
    {
        public const string Core = "ship_hub_core", Pylon = "ship_hub_pylon", Kiosk = "ship_hub_kiosk", Pier = "ship_hub_pier",
            Plinth = "ship_hub_plinth", Wall = "ship_hub_wall";
        // palette: 1 graphite, 5 gold, 11 soot, 13 steel blue, 14 bare metal, 16 light grey
        const byte Graphite = 1, Gold = 5, Soot = 11, Steel = 13, Bare = 14, Light = 16;

        /// <summary>Builds every hub blueprint. Origins: Core and Plinth are centred on the grid origin (cell centres at i + .5);
        /// Pylon is the column x, z ∈ [−3, 3] (centre at (+.5, +.5), like ship_track_post); Kiosk, Pier and Wall have their front
        /// face at z = 0 (the −z side) and their centre column at x = 0 (centre at x + .5), y = 0 being the flight plane.</summary>
        [MenuItem("Game/Content/Build Playground Hub")]
        public static void Build()
        {
            TrackStructureBuilder.EnsureBlock(); AssetDatabase.SaveAssets();
            using (var service = new ContentService(ContentDatabaseBuilder.Rebuild()))
            {
                var catalog = service.Catalog;
                var content = new VoxelContent { Blocks = service.BlockDefs, Orientations = service.Orientations };
                ushort frame = catalog.GetBlockIndex(catalog.Get<BlockDefinition>(new ContentId("block_track_frame")));
                ushort hull = catalog.GetBlockIndex(catalog.Get<BlockDefinition>(new ContentId("block_hull")));
                // gold and light grey are trim (Track Frame); everything else is hull
                Action<Action<ushort, int, int, int, byte>, int, int, int, byte> P = (put, x, y, z, c) => put(c == Gold || c == Light ? frame : hull, x, y, z, c);

                // Core (2 m cells, 188 m across): a checkered hub disc with a gold-banded spire; an outer ring at 80–94 m radius with four
                // 16 m gates (N, E, S, W) and lit windows; four diagonal spokes below the flight plane (y −6..−2 m) so ships fly over them.
                TrackStructureBuilder.Make(service, content, Core, "ARK Proving Grounds Core", 2, (put) =>
                {
                    for (int x = -48; x < 48; x++) for (int z = -48; z < 48; z++)
                    {
                        float px = x + .5f, pz = z + .5f, r = math.length(new float2(px, pz));
                        if (r <= 11)
                        {
                            for (int y = -3; y < 3; y++) P(put, x, y, z, y == 2 ? Bare : ((x ^ z) & 4) == 0 ? Steel : Graphite);
                            if (r <= 3) for (int y = 3; y <= 7; y++) P(put, x, y, z, y == 7 ? Light : (y & 1) == 0 && r > 2 ? Gold : Steel);
                        }
                        else if (r >= 40 && r <= 47)
                        {
                            if (math.abs(px) < 4 || math.abs(pz) < 4) continue;   // the four gates
                            int step = (int)math.floor((math.atan2(pz, px) + math.PI) * 47);
                            for (int y = -2; y <= 1; y++)
                            {
                                byte c = y == -2 ? Soot : y == 1 && (r < 41 || r > 46) ? Gold : y == 0 && r > 46 && step % 3 == 0 ? Light : Graphite;
                                P(put, x, y, z, c);
                            }
                        }
                        if (r > 10 && r < 41)
                        {
                            // spokes on the diagonals: 3 cells wide, gold every 4th cell along
                            float along = (math.abs(px) + math.abs(pz)) * .70710678f, across = math.abs(math.abs(px) - math.abs(pz)) * .70710678f;
                            if (across <= 1.5f) for (int y = -3; y <= -2; y++) P(put, x, y, z, ((int)along & 3) == 0 ? Gold : Steel);
                        }
                    }
                });

                // Pylon (1 m cells): a 7 m chamfered steel column from 8 m below the plane to 16 m above, gold band at the plane, lit face strips.
                TrackStructureBuilder.Make(service, content, Pylon, "District Pylon", 1, (put) =>
                {
                    for (int y = -8; y <= 15; y++) for (int x = -3; x <= 3; x++) for (int z = -3; z <= 3; z++)
                    {
                        if (math.abs(x) == 3 && math.abs(z) == 3) continue;
                        bool strip = (math.abs(x) == 3 && z == 0 || math.abs(z) == 3 && x == 0) && y >= 2 && y <= 12;
                        byte c = y < -6 ? Soot : y >= 14 ? Light : y >= -1 && y <= 0 ? Gold : strip ? Light : Steel;
                        P(put, x, y, z, c);
                    }
                });

                // Kiosk (1 m cells): a 9 × 3 × 11 m graphite slab; its −z face (z = 0) carries a 7 × 5 m lit screen in a steel frame from the plane up.
                TrackStructureBuilder.Make(service, content, Kiosk, "Terminal Kiosk", 1, (put) =>
                {
                    for (int x = -4; x <= 4; x++) for (int y = -4; y <= 6; y++) for (int z = 0; z <= 2; z++)
                    {
                        byte c = y <= -3 ? Soot : Graphite;
                        if (z == 0)
                        {
                            if (x >= -3 && x <= 3 && y >= 0 && y <= 4) c = Light;
                            else if (y >= -1 && y <= 5) c = Steel;
                            else if (y == 6) c = Gold;
                        }
                        P(put, x, y, z, c);
                    }
                });

                // Pier (1 m cells): a 12 × 80 m deck 3–5 m below the plane, planked every 4 m, gold edges; 2 × 2 bollards every 10 m on both
                // edges up to 3 m above the plane (the moored ship sits beside the pier).
                TrackStructureBuilder.Make(service, content, Pier, "Dock Pier", 1, (put) =>
                {
                    for (int x = -6; x <= 5; x++) for (int z = 0; z <= 79; z++)
                    {
                        for (int y = -5; y <= -3; y++)
                            P(put, x, y, z, y == -3 && (x == -6 || x == 5) ? Gold : y == -3 && (z & 3) == 0 ? Graphite : Steel);
                        if ((x <= -5 || x >= 4) && z % 10 >= 4 && z % 10 <= 5)
                            for (int y = -2; y <= 2; y++) P(put, x, y, z, y == 2 ? Light : Graphite);
                    }
                });

                // Plinth (1 m cells): a 28 m steel disc 6–9 m below the plane with a gold rim, centred on the grid origin.
                TrackStructureBuilder.Make(service, content, Plinth, "Display Plinth", 1, (put) =>
                {
                    for (int x = -14; x < 14; x++) for (int z = -14; z < 14; z++)
                    {
                        float r = math.length(new float2(x + .5f, z + .5f));
                        if (r > 14) continue;
                        for (int y = -9; y <= -6; y++) P(put, x, y, z, y == -9 ? Soot : y == -6 && r > 13 ? Gold : Steel);
                    }
                });

                // Wall (1 m cells): a 40 × 3 × 10 m graphite barrier across the plane, gold/soot chevrons on both faces, light-grey top.
                TrackStructureBuilder.Make(service, content, Wall, "Barrier Wall", 1, (put) =>
                {
                    for (int x = -20; x <= 19; x++) for (int y = -4; y <= 5; y++) for (int z = 0; z <= 2; z++)
                    {
                        int u = ((x % 8) + 8) % 8, v = u < 4 ? u : 7 - u;
                        byte c = y == 5 ? Light : z != 1 && y >= -1 && y <= 2 ? (((v + y + 8) & 2) == 0 ? Gold : Soot) : Graphite;
                        P(put, x, y, z, c);
                    }
                });
            }
            AssetDatabase.SaveAssets(); ContentDatabaseBuilder.Rebuild();
            GameLog.Info("Content", "Built the playground hub (ARK Proving Grounds).");
        }
    }
}
