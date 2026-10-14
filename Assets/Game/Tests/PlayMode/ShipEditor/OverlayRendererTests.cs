using System;
using System.Collections;
using Game.App.Boot;
using Game.Data.Blocks;
using Game.Data.Services;
using Game.ShipEditor.Data;
using Game.ShipEditor.Rendering;
using Game.ShipEditor.Services;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
namespace Game.Tests.PlayMode.ShipEditor
{
    public sealed class OverlayRendererTests
    {
        sealed class Fixture:IDisposable
        {
            readonly ContentService content;
            readonly ShipEditorConfig config;
            readonly EditorContext context;
            readonly VoxelStructure source=new();
            readonly Material material;
            internal readonly EditSession Session;
            internal readonly ShipEditorController Controller;
            internal readonly OverlayRenderer Renderer;
            internal readonly ushort Hull;
            internal Fixture()
            {
                content=new ContentService(Resources.Load<GameRoot>("GameRoot").Config.ContentDatabase);
                config=ScriptableObject.CreateInstance<ShipEditorConfig>();material=new Material(Shader.Find("Game/EditorOverlay")){enableInstancing=true};
                config.OverlayMaterial=material;config.ValidColor=Color.green;config.InvalidColor=Color.red;
                context=new EditorContext(config,content.Catalog,content.BlockDefs,content.Orientations,16);
                for(ushort i=1;i<content.BlockDefs.Value.Blocks.Length;i++)if(content.BlockDefs.Value.Blocks[i].Kind==BlockKind.Structural && content.BlockDefs.Value.Blocks[i].Cells.Length==1){Hull=i;break;}
                Session=new EditSession(new ShipEditorEntryArgs{Source=source,Options=new EditSessionOptions{Creative=true}},context);
                Controller=new ShipEditorController(Session,context);Renderer=new OverlayRenderer(Session,context,Controller,content.PropMeshes);
            }
            public void Dispose(){Renderer.Dispose();Controller.Dispose();Session.Dispose();context.Dispose();source.Dispose();content.Dispose();UnityEngine.Object.Destroy(material);UnityEngine.Object.Destroy(config);}
        }
        [Test] public void UnchangedCategory_NoUpload()
        {
            using var f=new Fixture();var category=f.Renderer.Category(OverlayKind.Selection);
            category.Clear();category.Add(new OverlayInstance(0,1,Color.cyan),65536);
            f.Renderer.Render();int before=category.UploadCount;f.Renderer.Render();Assert.AreEqual(before,category.UploadCount);
            category.Clear();category.Add(new OverlayInstance(1,1,Color.cyan),65536);f.Renderer.Render();Assert.AreEqual(before+1,category.UploadCount);
        }
        [UnityTest] public IEnumerator MaxInstances_OneDraw()
        {
            using var f=new Fixture();var category=f.Renderer.Category(OverlayKind.Selection);category.Clear();
            for(int i=0;i<65536;i++)category.Add(new OverlayInstance(new float3(i%256,0,i/256),1,Color.cyan),65536);
            f.Renderer.Render();Assert.AreEqual(65536,category.Instances.Length);Assert.AreEqual(65536,category.Buffer.count);Assert.AreEqual(1,f.Renderer.DrawCalls);
            yield return null;
        }
        [Test] public void GhostInvalid_UsesInvalidColor()
        {
            using var f=new Fixture();using var records=new NativeArray<BlockRecord>(1,Allocator.Temp);
            records[0]=new BlockRecord{Block=f.Hull,DamageOffset=-1};f.Controller.Preview.Set(records,PlacementError.Occupied);
            f.Renderer.Update(0);var invalid=f.Renderer.Category(OverlayKind.GhostInvalid);
            Assert.AreEqual(1,invalid.Instances.Length);Assert.AreEqual(new OverlayInstance(0,1,Color.red).ColorRGBA8,invalid.Instances[0].ColorRGBA8);
            Assert.AreEqual(0,f.Renderer.Category(OverlayKind.Ghost).Instances.Length);f.Renderer.Render();
        }
    }
}
