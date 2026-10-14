using System;
using System.Collections.Generic;
using Game.ShipEditor.Data;
using Game.ShipEditor.Services;
using Game.ShipEditor.Validation;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.ShipEditor.Rendering
{
    /// <summary>SPEC-51 §5.13: versioned procedural overlays; owns GPU resources, borrows editor state.</summary>
    public sealed class OverlayRenderer : IDisposable
    {
        readonly EditSession session;
        readonly EditorContext context;
        readonly ShipEditorController controller;
        readonly Material material;
        readonly Mesh cube;
        readonly int limit;
        readonly OverlayCategory[] categories = new OverlayCategory[11];
        readonly Dictionary<BlockRecord, byte> damaged = new();
        readonly Dictionary<int, OverlayCategory> propGhosts = new();
        readonly IReadOnlyList<Mesh> propMeshes;
        readonly ProfilerMarker upload = new("OverlayRenderer.Upload");
        NativeList<BlockRecord> records;
        NativeList<byte> damage;
        NativeList<int3> disconnected;
        int previewVersion = -1, selectionVersion = -1, revision = -1, symmetryVersion = -1, slice = int.MinValue;
        double nextDisconnected;
        bool disconnectedDirty = true, damageDirty = true, disposed;
        public Matrix4x4 GridToWorld { get; set; } = Matrix4x4.identity;
        public int DrawCalls { get; private set; }
        public OverlayRenderer(EditSession session, EditorContext context, ShipEditorController controller, IReadOnlyList<Mesh> propMeshes = null)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            this.controller = controller ?? throw new ArgumentNullException(nameof(controller));
            material = context.Config.OverlayMaterial;
            if (!material) throw new ArgumentException("Editor overlay material is required", nameof(context));
            limit = Math.Clamp(context.Config.OverlayMaxInstances, 1, 65536);
            this.propMeshes = propMeshes ?? Array.Empty<Mesh>();
            records = new NativeList<BlockRecord>(Allocator.Persistent); damage = new NativeList<byte>(Allocator.Persistent); disconnected = new NativeList<int3>(Allocator.Persistent);
            cube = EdgeCubeMesh.Create();
            for (int i=0;i<categories.Length;i++) categories[i]=new OverlayCategory();
            try { RebuildDamage(); session.DraftApplied += Applied; session.Changed += Changed; }
            catch { Dispose(); throw; }
        }
        internal OverlayCategory Category(OverlayKind kind) => categories[(int)kind];
        void Clear(OverlayKind kind) => Category(kind).Clear();
        void Add(OverlayKind kind,float3 min,float3 size,Color color,uint flags=0) => Category(kind).Add(new OverlayInstance(min,size,color,flags),limit);
        void RecordCells(OverlayKind kind,BlockRecord record,Color color,bool liveOnly)
        {
            foreach(var offset in context.Footprints.Get(record.Block,record.Orient).Cells)
            {
                int3 cell=record.Origin+offset;
                if(liveOnly && !FullValidator.OwnsCell(session,record,cell))continue;
                Add(kind,cell,1,color);
            }
        }
        void RebuildDamage()
        {
            damaged.Clear();new DraftReader(session.Draft,context).Enumerate(records);
            foreach(var record in records) TrackDamage(record);
            damageDirty=true;
        }
        void TrackDamage(BlockRecord record)
        {
            record.DamageOffset=-1;damage.Clear();
            if(new DraftReader(session.Draft,context).CaptureDamage(record,damage)>=0)damaged[record]=0;
        }
        void Applied(DiffCommand command,bool forward)
        {
            // Read the completed draft, not intermediate remove/add states of a transformation.
            foreach(var op in command.Ops)
            {
                var record=op.Record;record.DamageOffset=-1;damaged.Remove(record);
                foreach(var cell in context.Footprints.Get(record.Block,record.Orient).Cells)
                    if(FullValidator.OwnsCell(session,record,record.Origin+cell)){TrackDamage(record);break;}
            }
            damageDirty=true;
        }
        void Changed(EditChange change)
        {
            if(change.Kind==EditChangeKind.Discarded)RebuildDamage();
            disconnectedDirty=true;
        }
        public void Update(double realtimeSeconds)
        {
            if(disposed)throw new ObjectDisposedException(nameof(OverlayRenderer));
            var cfg=context.Config;var preview=controller.Preview;
            if(previewVersion!=preview.Version)
            {
                previewVersion=preview.Version;
                Clear(OverlayKind.Ghost);Clear(OverlayKind.GhostInvalid);Clear(OverlayKind.MirrorGhost);Clear(OverlayKind.Footprint);Clear(OverlayKind.Clearance);Clear(OverlayKind.BoxOutline);
                foreach(var group in propGhosts.Values)group.Clear();
                bool invalid=preview.Error!=PlacementError.None;int index=0;
                foreach(var record in preview.Records)
                {
                    var kind=invalid?OverlayKind.GhostInvalid:index++==0 || !(controller.Symmetry.MirrorX || controller.Symmetry.MirrorY || controller.Symmetry.MirrorZ)?OverlayKind.Ghost:OverlayKind.MirrorGhost;
                    var color=invalid?cfg.InvalidColor:kind==OverlayKind.MirrorGhost?cfg.MirrorGhostColor:cfg.ValidColor;
                    RecordCells(kind,record,color,false);
                    var footprint=context.Footprints.Get(record.Block,record.Orient);
                    foreach(var cell in footprint.Clearance)Add(OverlayKind.Clearance,record.Origin+cell,1,cfg.ClearanceColor);
                    ref var def=ref context.Definitions.Value.Blocks[record.Block];
                    if(def.PropMeshIndex>=0 && def.PropMeshIndex<propMeshes.Count && propMeshes[def.PropMeshIndex])
                    {
                        // Prop mesh rotations are applied in the vertex shader using the oriented instance bucket below.
                        int key=def.PropMeshIndex*24+record.Orient;
                        if(!propGhosts.TryGetValue(key,out var group))propGhosts.Add(key,group=new OverlayCategory());
                        group.Add(new OverlayInstance((float3)record.Origin+.5f,1,color),limit);
                        RecordCells(OverlayKind.Footprint,record,color,false);
                    }
                }
                if(preview.HasBounds)Add(OverlayKind.BoxOutline,preview.BoundsMin,(float3)preview.BoundsMax-preview.BoundsMin+1,invalid?cfg.InvalidColor:cfg.ValidColor,1);
            }
            if(selectionVersion!=controller.Selection.Version || revision!=session.Revision)
            {
                selectionVersion=controller.Selection.Version;revision=session.Revision;
                Clear(OverlayKind.Selection);controller.Selection.CopyRecords(records);
                foreach(var record in records)RecordCells(OverlayKind.Selection,record,cfg.SelectionColor,true);
                symmetryVersion=-1;
            }
            if(damageDirty)
            {
                Clear(OverlayKind.Damaged);
                foreach(var record in damaged.Keys)RecordCells(OverlayKind.Damaged,record,cfg.DamagedColor,true);
                damageDirty=false;
            }
            if(disconnectedDirty && realtimeSeconds>=nextDisconnected)
            {
                int3 coreCell=new(int.MinValue);
                if(session.Placement.Core is BlockRecord core)
                    foreach(var cell in context.Footprints.Get(core.Block,core.Orient).Cells)
                        if(FullValidator.OwnsCell(session,core,core.Origin+cell)){coreCell=core.Origin+cell;break;}
                disconnected.Clear();session.Connectivity.DisconnectedCells(coreCell,disconnected,limit);
                Clear(OverlayKind.Disconnected);foreach(var cell in disconnected)Add(OverlayKind.Disconnected,cell,1,cfg.DisconnectedColor);
                disconnectedDirty=false;nextDisconnected=realtimeSeconds+.25;
            }
            int level=controller.SliceEnabled?controller.SliceLevel:0;
            if(symmetryVersion!=controller.Symmetry.Version || slice!=level)
            {
                symmetryVersion=controller.Symmetry.Version;slice=level;Clear(OverlayKind.MirrorPlane);Clear(OverlayKind.Grid);
                session.Placement.TryGetBounds(out int3 lo,out int3 hi);lo=math.min(lo,new int3(-8));hi=math.max(hi,new int3(8));
                var sym=controller.Symmetry;
                for(int axis=0;axis<3;axis++)
                {
                    bool enabled=axis==0?sym.MirrorX:axis==1?sym.MirrorY:sym.MirrorZ;
                    if(!enabled)continue;
                    float3 min=lo,size=(float3)hi-lo+1;min[axis]=(axis==0?sym.PlaneX2:axis==1?sym.PlaneY2:sym.PlaneZ2)*.5f;size[axis]=.015f;
                    Add(OverlayKind.MirrorPlane,min,size,cfg.MirrorPlaneColor);
                }
                for(int x=lo.x;x<=hi.x+1 && Category(OverlayKind.Grid).Instances.Length<limit;x++)Add(OverlayKind.Grid,new float3(x,level,lo.z),new float3(.012f,.012f,hi.z-lo.z+1),cfg.GridColor,1);
                for(int z=lo.z;z<=hi.z+1 && Category(OverlayKind.Grid).Instances.Length<limit;z++)Add(OverlayKind.Grid,new float3(lo.x,level,z),new float3(hi.x-lo.x+1,.012f,.012f),cfg.GridColor,1);
            }
        }
        public void Render(Camera camera=null)
        {
            if(disposed)throw new ObjectDisposedException(nameof(OverlayRenderer));
            DrawCalls=0;
            foreach(var category in categories)Draw(category,cube,camera,Matrix4x4.identity);
            foreach(var pair in propGhosts)
            {
                var q=context.Orientations.Value.Rotations[pair.Key%24].value;
                Draw(pair.Value,propMeshes[pair.Key/24],camera,Matrix4x4.Rotate(new Quaternion(q.x,q.y,q.z,q.w)));
            }
        }
        void Draw(OverlayCategory category,Mesh mesh,Camera camera,Matrix4x4 rotation)
        {
            using(var marker=upload.Auto())category.Upload(limit);
            if(category.Instances.Length==0)return;
            category.Properties.SetMatrix("_GridToWorld",GridToWorld);category.Properties.SetMatrix("_MeshRotation",rotation);
            category.Properties.SetFloat("_CellSize",session.Draft.CellSize);
            var parameters=new RenderParams(material){camera=camera,matProps=category.Properties,shadowCastingMode=ShadowCastingMode.Off,receiveShadows=false,
                worldBounds=new Bounds(GridToWorld.MultiplyPoint3x4(Vector3.zero),Vector3.one*100000)};
            Graphics.RenderMeshPrimitives(parameters,mesh,0,category.Instances.Length);DrawCalls++;
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;session.DraftApplied-=Applied;session.Changed-=Changed;
            foreach(var category in categories)category?.Dispose();foreach(var category in propGhosts.Values)category.Dispose();
            records.Dispose();damage.Dispose();disconnected.Dispose();
            if(Application.isPlaying)UnityEngine.Object.Destroy(cube);else UnityEngine.Object.DestroyImmediate(cube);
        }
    }
}
