using System.Collections.Generic;
using Game.Data;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using UnityEditor;

namespace Game.Tests.EditMode.Content
{
    /// <summary>Core ladder (owner, 2026-10-18: "a bigger core allows for a bigger ship"): every shipped blueprint that flies (one core,
    /// at least one thruster) sits inside its core's range, from its floor (20% of MaxCells) to MaxCells, so none spawns too big or
    /// already below the line where its core malfunctions. Stations and turrets (no thrusters, or several cores) are exempt.</summary>
    public sealed class CoreFitContentTests
    {
        [Test] public void ShippedShips_FitTheirCores()
        {
            using var service = new ContentService(AssetDatabase.LoadAssetAtPath<ContentDatabase>("Assets/Game/Content/ContentDatabase.asset"));
            var catalog = service.Catalog; var misfits = new List<string>(); int checkedShips = 0;
            foreach (var blueprint in catalog.All<ShipBlueprintDefinition>())
            {
                Assert.IsTrue(VoxelStructureCodec.TryDecode(blueprint.Payload, catalog, out var structure, out string error), blueprint.IdString + ": " + error);
                using (structure)
                {
                    using var blocks = new NativeList<StructureBlock>(Allocator.Temp); structure.GetBlocks(blocks);
                    int cores = 0, thrusters = 0; CoreParams core = default; string coreId = null;
                    foreach (var block in blocks)
                    {
                        if (block.InstanceIndex < 0 || catalog.GetBlock(block.Placement.Block) is not { } definition) continue;
                        if (definition.TryGetModule<CoreModule>(out var module)) { cores++; core = module.Params; coreId = definition.IdString; }
                        if (definition.TryGetModule<ThrusterModule>(out _)) thrusters++;
                    }
                    if (cores != 1 || thrusters == 0) continue;
                    checkedShips++;
                    int cells = structure.SolidCellCount;
                    if (!core.Fits(cells)) misfits.Add($"{blueprint.IdString}: {cells} cells on {coreId} ({core.Floor}..{core.MaxCells})");
                }
            }
            Assert.Greater(checkedShips, 10, "the shipped ships were found");
            Assert.IsEmpty(misfits, "run Game > Content > Build Core Ladder:\n" + string.Join("\n", misfits));
        }
    }
}
