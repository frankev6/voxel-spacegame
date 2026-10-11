using System.Collections.Generic;
using Game.Data.Blocks;
using Game.Data.Definitions;
using UnityEngine;
using static Game.Tests.EditMode.Data.TestContentFactory;

namespace Game.Tests.EditMode.Data
{
    /// <summary>Seeded synthetic content for ContentService tests (SPEC-04 DATA-007): valid placeholders, structural blocks, prop and functional
    /// boxes of at most 3×3×3 cells (X-symmetric, so no partners are needed), and plain items. Call TestContentFactory.DestroyAll afterwards.</summary>
    public static class SyntheticContent
    {
        public static List<ContentDefinition> Make(int definitions, int blocks, uint seed)
        {
            var rng = new Unity.Mathematics.Random(seed);
            var icon = Icon();
            var mesh = TestContentFactory.Mesh();
            var list = new List<ContentDefinition> { Block("block_missing", BlockKind.Structural), Item("item_missing") };
            for (int i = 0; list.Count < blocks + 1; i++)   // + item_missing is not a block
            {
                int kind = rng.NextInt(3);
                if (kind == 0) { list.Add(Block($"block_s{i:0000}", BlockKind.Structural)); continue; }
                var cells = new List<Vector3Int>();
                int w = rng.NextInt(1, 4), h = rng.NextInt(1, 4), d = rng.NextInt(1, 4);
                for (int z = 0; z < d; z++) for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) cells.Add(new Vector3Int(x, y, z));
                if (kind == 1) list.Add(Block($"block_p{i:0000}", BlockKind.Prop, cells.ToArray(), edit: so => Set(so, "propMesh", mesh)));
                else list.Add(Block($"block_f{i:0000}", BlockKind.Functional, cells.ToArray(),
                    new BlockModule[] { new PowerStorageModule { Params = new PowerStorageParams { Storage = rng.NextFloat(1, 500) } } },
                    so => Set(so, "propMesh", mesh)));
            }
            for (int i = 0; list.Count < definitions; i++)
                list.Add(Create<ItemDefinition>($"item_i{i:0000}", so => { Set(so, "displayName", "Synthetic " + i); Set(so, "icon", icon); Set(so, "basePrice", (int)rng.NextUInt(1000)); }));
            return list;
        }
    }
}
