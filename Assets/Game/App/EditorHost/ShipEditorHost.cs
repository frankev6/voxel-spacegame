using System;
using Game.ShipEditor.Data;
using Game.ShipEditor.Rendering;
using Game.ShipEditor.Services;
using Unity.Mathematics;
using UnityEngine;

namespace Game.App.EditorHost
{
    /// <summary>Scene-owned editor; persistent UI delegates are detached before native owners are disposed.</summary>
    public sealed class ShipEditorHost : MonoBehaviour
    {
        EditorHostServices services;
        EditorContext context;
        EditSession session;
        ShipEditorController controller;
        DraftRenderer draft;
        OverlayRenderer overlay;
        EditorInputSampler input;
        ShipEditorPresenter presenter;
        bool released;
        public EditSession Session=>session;
        public static ShipEditorHost Create(ShipEditorEntryArgs args,EditorHostServices services)
        {
            if(args==null)throw new ArgumentNullException(nameof(args));
            if(services==null)throw new ArgumentNullException(nameof(services));
            if(services.Presenter!=null)throw new InvalidOperationException("An editor host is already active.");
            var go=new GameObject("Ship editor host");var host=go.AddComponent<ShipEditorHost>();
            try {host.Initialize(args,services);return host;}
            catch {host.Release();Destroy(go);throw;}
        }
        void Initialize(ShipEditorEntryArgs args,EditorHostServices owners)
        {
            services=owners;
            context=new EditorContext(owners.Config,owners.Content.Catalog,owners.Content.BlockDefs,owners.Content.Orientations,16);
            session=new EditSession(args,context);controller=new ShipEditorController(session,context,()=>presenter?.RequestLeave());
            draft=new DraftRenderer(session,context,owners.Config.BrickMaterial,owners.Config.BrickXRayMaterial,owners.Content.PropMeshes,owners.PropMaterial);
            overlay=new OverlayRenderer(session,context,controller,owners.Content.PropMeshes);
            presenter=new ShipEditorPresenter(args,owners,session,context,controller);
            input=new EditorInputSampler(owners.Input,owners.Ui);controller.FocusRequested+=Focus;
            owners.Presenter=presenter;owners.Ui.Navigator.SetRoot(owners.View);
            session.Draft.TryGetCellBounds(out var min,out var max);float size=session.Draft.CellSize;
            owners.Camera.EnterEditorOrbit(((float3)min+max+1)*(.5f*size),math.max(1,math.length((float3)max-min+1)*size*.5f),true);
        }
        void Focus(float3 cells,float radius)=>services.Camera.FocusOn(cells*session.Draft.CellSize,math.max(1,radius*session.Draft.CellSize));
        void Update()
        {
            if(released || presenter==null)return;
            session.CompleteRelabel();EditorInputFrame frame=default;input.Sample(ref frame);
            var ray=services.Camera.ScreenPointToRay(frame.PointerScreen);float size=session.Draft.CellSize;
            controller.Tick(frame,new Ray(ray.origin/size,ray.direction));
            services.Camera.SetEditorSuppression(controller.ConsumesWheel,false);
            presenter.Tick(Time.unscaledDeltaTime,frame.PrimaryHeld);
            draft.SetSlice(controller.SliceEnabled,controller.SliceLevel);draft.XRay=controller.XRay;
            draft.CameraPivotCells=services.Camera.Focus/size;draft.UpdateMeshes();overlay.Update(Time.realtimeSinceStartupAsDouble);
        }
        void LateUpdate(){if(released || draft==null)return;draft.Render(services.Camera.Camera);overlay.Render(services.Camera.Camera);}
        void OnDestroy()=>Release();
        void Release()
        {
            if(released)return;released=true;
            if(services!=null && services.Presenter==presenter)services.Presenter=null;
            presenter?.Dispose();if(controller!=null)controller.FocusRequested-=Focus;
            overlay?.Dispose();draft?.Dispose();controller?.Dispose();session?.Dispose();context?.Dispose();
            services?.Camera.SetEditorSuppression(false,false);
        }
    }
}
