using System;
using System.Collections;
using Game.App.Boot;
using Game.App.EditorHost;
using Game.App.Services;
using Game.Core;
using Game.Core.Events;
using Game.Data.Services;
using Game.Input;
using Game.Presentation.Input;
using Game.Presentation.Services;
using Game.ShipEditor.Data;
using Game.ShipEditor.Services;
using Game.Tests.PlayMode.UI;
using Game.UI.Data;
using Game.UI.Services;
using Game.UI.Views;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;
namespace Game.Tests.PlayMode.ShipEditor
{
    public sealed class EditorHostTests
    {
        [SetUp] public void Setup(){Services.Reset();EventHub.Reset();}
        [TearDown] public void Cleanup(){Services.Reset();EventHub.Reset();}
        sealed class Fixture:IDisposable
        {
            readonly UITestPanel panel=new(1280,720);
            readonly UIConfig uiConfig;
            readonly ContentService content;
            readonly InputService input=new(null);
            readonly InputServiceBridge bridge;
            readonly PauseService pause=new();
            readonly CameraRigService camera;
            readonly GameObject cameraParent=new("Editor test camera");
            readonly VoxelStructure source=new();
            internal readonly UIRoot UI;
            internal readonly EditorHostServices Owners;
            internal ShipEditorHost Host;
            internal int Leaves;
            internal Fixture()
            {
                var root=Resources.Load<GameRoot>("GameRoot");content=new ContentService(root.Config.ContentDatabase);
                uiConfig=Object.Instantiate(root.Config.UiConfig);uiConfig.PanelSettings=panel.Settings;
                bridge=new InputServiceBridge(input);UI=new UIRoot(panel.Document,uiConfig,pause,bridge,new NullUISoundSink(),new UnityFrameClock());bridge.BindBack(UI.HandleBackPressed);
                camera=new CameraRigService(root.Config.CameraConfig,cameraParent.transform,new InputCameraInput(input));
                Owners=new EditorHostServices(UI,input,camera,content,AssetDatabase.LoadAssetAtPath<ShipEditorConfig>("Assets/Game/ShipEditor/Data/ShipEditorConfig.asset"),null);
                Owners.Leave=()=>Leaves++;
                ushort hull=0;for(ushort i=1;i<content.BlockDefs.Value.Blocks.Length;i++)if(content.BlockDefs.Value.Blocks[i].Kind==Game.Data.Blocks.BlockKind.Structural){hull=i;break;}
                using var edits=new NativeList<VoxelEdit>(Allocator.Temp);edits.Add(new VoxelEdit{Kind=VoxelEditKind.PlaceBlock,Block=hull});edits.Add(new VoxelEdit{Kind=VoxelEditKind.PlaceBlock,Block=hull,Cell=new int3(1,0,0)});
                using var touched=new NativeList<int3>(Allocator.Temp);Assert.IsTrue(source.ApplyEdits(edits.AsArray(),new VoxelContent{Blocks=content.BlockDefs,Orientations=content.Orientations},touched));
                Host=ShipEditorHost.Create(new ShipEditorEntryArgs{Source=source,Options=new EditSessionOptions{Creative=true,Title="Host test"}},Owners);
                // Drive presenter time explicitly; the scene still owns and destroys the real host.
                Host.enabled=false;
            }
            internal void Edit()
            {
                Assert.IsTrue(Host.Session.Draft.TryGetBlockAt(new int3(1,0,0),out var block,out _));
                using var batch=new EditBatch();batch.Ops.Add(new DiffOp{Kind=DiffOpKind.Remove,Record=new BlockRecord{Block=block.Block,Origin=block.Origin,Orient=block.Orient,Color=block.Color,DamageOffset=-1}});
                Assert.IsTrue(Host.Session.TryApply(batch,AtomicityMode.AllOrNothing,0,ToolId.Remove,"Remove",new ApplyReport()));
            }
            internal void Escape()=>UI.Navigator.Back();
            public void Dispose()
            {
                if(Host)Object.DestroyImmediate(Host.gameObject);UI.Dispose();bridge.Dispose();input.Dispose();pause.Dispose();camera.Dispose();Object.DestroyImmediate(cameraParent);
                source.Dispose();content.Dispose();Object.DestroyImmediate(uiConfig);panel.Dispose();
            }
        }
        [UnityTest] public IEnumerator EscDirty_ShowsPrompt()
        {
            using var f=new Fixture();f.Edit();f.Escape();yield return null;
            Assert.IsTrue(f.UI.Confirm.IsOpen);var root=f.UI.Navigator.Get<ConfirmDialogView>().Root;
            Assert.IsNotNull(root.Q<Button>("confirm-commit"));Assert.IsNotNull(root.Q<Button>("confirm-discard"));Assert.IsNotNull(root.Q<Button>("confirm-keep-editing"));
        }
        [UnityTest] public IEnumerator PromptEsc_KeepsEditing()
        {
            using var f=new Fixture();f.Edit();f.Escape();yield return null;f.Escape();yield return null;
            Assert.IsFalse(f.UI.Confirm.IsOpen);Assert.IsTrue(f.Host.Session.IsDirty);Assert.AreEqual(0,f.Leaves);Assert.AreSame(f.Owners.View,f.UI.Navigator.Top);
        }
        [UnityTest] public IEnumerator CommitDisabled_ShowsReason()
        {
            using var f=new Fixture();f.Edit();f.Owners.Presenter.Tick(.11f,false);yield return null;
            Assert.IsFalse(f.Owners.Model.CanCommit);StringAssert.Contains("NoCore",f.Owners.Model.CommitBlockedReason);
            Assert.IsFalse(f.Owners.View.Root.Q<Button>("editor-commit").enabledSelf);
        }
        [UnityTest] public IEnumerator Discard_LeavesWithoutChanges()
        {
            using var f=new Fixture();int original=f.Host.Session.Original.SolidCellCount;f.Edit();f.Escape();yield return null;
            DialogTestContext.Activate(f.UI.Navigator.Get<ConfirmDialogView>().Root.Q<Button>("confirm-discard"));yield return null;
            Assert.AreEqual(1,f.Leaves);Assert.AreEqual(original,f.Host.Session.Draft.SolidCellCount);Assert.IsFalse(f.Host.Session.IsDirty);
        }
        [Test] public void PresenterThrottle()
        {
            using var f=new Fixture();var presenter=f.Owners.Presenter;int before=presenter.RefreshCount;f.Edit();
            presenter.Tick(.01f,true);presenter.Tick(.01f,true);Assert.AreEqual(before,presenter.RefreshCount);
            presenter.Tick(.081f,true);Assert.AreEqual(before+1,presenter.RefreshCount);
            f.Host.Session.Undo();presenter.Tick(.001f,true);Assert.AreEqual(before+1,presenter.RefreshCount);
            presenter.Tick(0,false);Assert.AreEqual(before+2,presenter.RefreshCount);presenter.Tick(.2f,false);Assert.AreEqual(before+2,presenter.RefreshCount);
        }
        [UnityTest] public IEnumerator SceneUnload_DestroysHost_NoLeaks()
        {
            var mode=NativeLeakDetection.Mode;NativeLeakDetection.Mode=NativeLeakDetectionMode.EnabledWithStackTrace;
            try
            {
                using(var warm=new Fixture()){}yield return null;
                Application.LogCallback baseline=(message,trace,kind)=>{if(message.Contains("Leak Detected"))LogAssert.Expect(kind,message);};
                Application.logMessageReceived+=baseline;try{UnsafeUtility.CheckForLeaks();}finally{Application.logMessageReceived-=baseline;}
                using(var f=new Fixture())
                {
                    var scene=SceneManager.CreateScene("Editor host lifetime");SceneManager.MoveGameObjectToScene(f.Host.gameObject,scene);
                    var operation=SceneManager.UnloadSceneAsync(scene);while(!operation.isDone)yield return null;
                    Assert.IsTrue(f.Host==null);Assert.IsNull(f.Owners.Presenter);
                }
                yield return null;Assert.AreEqual(0,UnsafeUtility.CheckForLeaks());
            }
            finally{NativeLeakDetection.Mode=mode;}
        }
    }
}
