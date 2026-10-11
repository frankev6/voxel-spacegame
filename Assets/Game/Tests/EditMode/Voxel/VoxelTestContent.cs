using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Tests.EditMode.Data;
using Game.Voxel.Model;
using UnityEngine;
using static Game.Tests.EditMode.Data.TestContentFactory;

namespace Game.Tests.EditMode.Voxel
{
    /// <summary>SPEC-10 test content built through the real SPEC-04 catalog/blob service.</summary>
    public sealed class VoxelTestContent : IDisposable
    {
        public ContentService Service { get; }
        public VoxelContent Content => new VoxelContent { Blocks = Service.BlockDefs, Orientations = Service.Orientations };
        public ushort Index(string id) => Service.Catalog.GetBlockIndex(Service.Catalog.Get<BlockDefinition>(new ContentId(id)));
        public VoxelTestContent(bool includeExplosive = false)
        {
            var defs = new List<ContentDefinition>
            {
                Block("block_missing", BlockKind.Structural), Item("item_missing"),
                Block("block_hull", BlockKind.Structural),
                Block("block_hull_angled", BlockKind.Structural, edit: so => { Set(so, "shape", (int)BlockShape.Wedge); Set(so, "cellHp", 50f); Set(so, "cellMass", 15f); }),
                Block("block_thruster_medium", BlockKind.Functional, BlockDefsBlobBuilderTests.ThrusterCross,
                    new BlockModule[] { new ThrusterModule { Params = new ThrusterParams { ThrustNewtons = 40000 } } },
                    so => { Set(so, "allowedOrientations.Bits", OrientationMask.HorizontalForwardBits); Set(so, "attachFaces", 47); Set(so, "functionHp", 1000f); Set(so, "cellMass", 30f); Set(so, "blockCategory", (int)BlockCategory.Propulsion); }),
                Block("block_core_cross", BlockKind.Functional,
                    new[] { new Vector3Int(0,0,0), new Vector3Int(0,1,0), new Vector3Int(0,2,0), new Vector3Int(1,1,0), new Vector3Int(-1,1,0), new Vector3Int(0,1,1), new Vector3Int(0,1,-1) },
                    new BlockModule[] { new CoreModule { Params = new CoreParams { MaxCells = 8000, MaxExtentCells = 150 } }, new PowerSourceModule { Params = new PowerSourceParams { Generation = 40 } }, new PowerStorageModule { Params = new PowerStorageParams { Storage = 200 } } },
                    so => { Set(so, "cellHp", 500f); Set(so, "functionHp", 3500f); Set(so, "cellMass", 60f); Set(so, "blockCategory", (int)BlockCategory.Core); }),
                Block("block_capacitor_small", BlockKind.Functional, BlockDefsBlobBuilderTests.Capacitor,
                    new BlockModule[] { new PowerStorageModule { Params = new PowerStorageParams { Storage = 40 } } },
                    so => { Set(so, "functionHp", 400f); Set(so, "cellMass", 35f); })
            };
            if (includeExplosive) defs.Add(Block("block_test_reactor", BlockKind.Functional, modules: new BlockModule[]
            {
                new PowerSourceModule { Params = new PowerSourceParams { Generation = 20 } },
                new ExplosiveModule { Params = new ExplosiveParams { BlastRadiusCells = 4, BlastDamage = 50 } }
            }));
            try { Service = ContentService.CreateForTests(defs); }
            catch { DestroyAll(); throw; }
        }
        public void Dispose() { Service.Dispose(); DestroyAll(); }
    }
}
