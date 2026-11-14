using System;
using SCIEPort.Features.Blueprints.Core;
using SCIEPort.Features.Blueprints.Core.Blueprints;
using SCIEPort.Features.Blueprints.Core.BuildEngine;
using SCIEPort.Features.Blueprints.Core.Safety;
using SCIEPort.Features.Blueprints.Core.Workbench;
using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.Blueprints
{
    internal sealed class BlueprintPlacementSession
    {
        private readonly WorldEditRuntime runtime;
        internal BlueprintPlacementSession(WorldEditRuntime runtime) { this.runtime=runtime; }
        private ComponentMiner owner;
        private object world;
        private Dialog dialog;
        private Blueprint original, selected;
        private bool selecting, clear, mirror;
        private int wait, retry, rotation, height;
        private CellPos origin, ground;
        private GameTerrainGateway gateway;
        private BlueprintPlacementPreview preview;
        private BlueprintPreviewControls controls;
        private PrimitivesRenderer3D renderer;

        public void Reset()
        {
            Cancel(); owner=null; world=null; original=null; renderer=null;
        }
        internal void OnWorldDisposed(object project) { if(object.ReferenceEquals(world,project))Reset(); }
        private void Close()
        {
            var manager=dialog as BlueprintToolDialog;
            if(manager!=null)manager.CloseChildren();
            if(dialog!=null)DialogsManager.HideDialog(dialog);dialog=null;
        }
        private void Cancel()
        {
            Close();
            if(controls!=null && controls.ParentWidget!=null) controls.ParentWidget.Children.Remove(controls);
            controls=null;selecting=false;wait=0;selected=null;original=null;gateway=null;preview=null;
        }
        private void Message(string text)
        {
            if(owner!=null && owner.ComponentPlayer!=null)owner.ComponentPlayer.ComponentGui.DisplaySmallMessage("[蓝图] "+text,Color.White,false,false);
        }
        public void Tick(ComponentGui gui)
        {
            if(owner==null) return;
            if(gui==null || gui.m_componentPlayer==null)return;
            if(!object.ReferenceEquals(world,gui.m_componentPlayer.Project)) { Reset();return; }
            if(gui.m_componentPlayer!=owner.ComponentPlayer)return;
            if(!owner.Entity.IsAddedToProject || owner.ComponentPlayer.ComponentHealth.Health<=0 ||
                Terrain.ExtractContents(owner.ActiveBlockValue)!=BlueprintToolBlock.Index)
            { Reset();return; }
            if(dialog!=null && dialog.ParentWidget!=null && !DialogsManager.Dialogs.Contains(dialog)) { Cancel();return; }
            if(gateway!=null && (!runtime.CanRequest() || runtime.IsBusy))
            { Cancel();Message("权限或施工状态已改变，预览已取消。");return; }
            if(controls!=null)controls.Arrange();
            if(wait>0 && --retry<=0)
            {
                wait--;retry=15;Prepare();
                if(wait==0 && preview!=null && !preview.Ready)UpdateStatus("区块等待结束，可重新点地选择位置。");
            }
        }
        public bool OnUse(ComponentMiner miner,Ray3 ray)
        {
            if(miner==null || miner.ComponentPlayer==null || miner.ComponentPlayer.GameWidget==null)return true;
            if(!object.ReferenceEquals(world,miner.Project) || (owner!=null && owner!=miner))Reset();
            owner=miner;world=miner.Project;
            if(dialog!=null)return true;
            if(!selecting && controls==null) { Open();return true; }
            var hit=miner.Raycast<TerrainRaycastResult>(ray,RaycastMode.Interaction);
            if(!hit.HasValue) { Cancel();Message("已取消放置。");return true; }
            if(miner.m_subsystemTerrain==null || !runtime.CanRequest())
            { Cancel();Message("当前无法请求建造。");return true; }
            ground=new CellPos(hit.Value.CellFace.X,hit.Value.CellFace.Y,hit.Value.CellFace.Z);
            gateway=new GameTerrainGateway(miner.m_subsystemTerrain);
            selecting=false;
            if(controls==null)
            {
                controls=new BlueprintPreviewControls(owner.ComponentPlayer,Adjust,Confirm,Cancel);
                owner.ComponentPlayer.GameWidget.GuiWidget.Children.Add(controls);
            }
            Reposition();return true;
        }
        internal void Open()
        {
            owner=runtime.Player.ComponentMiner;world=runtime.Player.Project;
            Cancel();
            dialog=new BlueprintToolDialog(runtime,owner.ComponentPlayer,Begin,Cancel);
            DialogsManager.ShowDialog(owner.ComponentPlayer.GameWidget.GuiWidget,dialog);
        }
        private void Begin(int direction,bool clearSetting,bool mirrored)
        {
            Blueprint bp=runtime.ClipboardBlueprint;
            string error=runtime.QuickValidate(bp);
            if(error!=null) { Message(error);return; }
            if(!runtime.CanRequest()) { Message("当前无法请求建造。");return; }
            if(runtime.IsBusy) { Message("正在施工或等待区块。");return; }
            original=bp;rotation=direction;height=0;clear=clearSetting;mirror=mirrored;
            selected=bp.Reorient(rotation,mirror);Close();selecting=true;
            Message("选择落点。");
        }
        private void Adjust(int delta)
        {
            if(dialog!=null || gateway==null)return;
            if(delta==2) { mirror=!mirror;selected=original.Reorient(rotation,mirror); }
            else if(delta==0) { rotation=(rotation+1)%4;selected=original.Reorient(rotation,mirror); }
            else height=MathUtils.Clamp(height+delta,-64,64);
            Reposition();
        }
        private void Reposition()
        {
            CellPos baseOrigin=runtime.WorkbenchOriginFor(selected,ground);
            height=MathUtils.Clamp(height,-baseOrigin.Y,WorldBounds.MaxYExclusive-BlueprintPlacementPreview.HeightOf(selected)-baseOrigin.Y);
            origin=baseOrigin.Add(new CellPos(0,height,0));
            wait=10;retry=15;preview=null;Prepare();
        }
        private void Prepare()
        {
            if(gateway==null || selected==null)return;
            preview=BlueprintPlacementPreview.Inspect(selected,origin,runtime.Palette,gateway,clear);
            if(!preview.Plan.AllEditabilityFailuresRecoverable)wait=0;
            UpdateStatus(null);
        }
        private void UpdateStatus(string extra)
        {
            if(controls==null || preview==null)return;
            BuildPlan plan=preview.Plan;
            string state=preview.Ready
                ? (plan.ClearedCount>0 ? "清障 "+plan.ClearedCount+" 格 · 放置 "+(plan.Changes.Count-plan.ClearedCount)+" 格"
                    : (preview.OccupiedCount>0 ? "将覆盖 "+preview.OccupiedCount+" 格，需确认清障" : "允许施工"))
                : "不可施工："+plan.FailureReason;
            controls.SetStatus(selected.Name+"  "+selected.SizeX+"×"+preview.EnvelopeHeight+"×"+selected.SizeZ+"  "+(rotation*90)+"°\n原点 "+origin+"；Y="+origin.Y+".."+(origin.Y+preview.EnvelopeHeight-1)+"\n"+(extra??state),preview.Ready);
        }
        private bool ValidateSnapshot()
        {
            if(owner==null || gateway==null || preview==null || !preview.Ready || runtime.IsBusy ||
                !owner.Entity.IsAddedToProject || owner.ComponentPlayer.ComponentHealth.Health<=0 ||
                Terrain.ExtractContents(owner.ActiveBlockValue)!=BlueprintToolBlock.Index ||
                !runtime.CanRequest())return false;
            if(!preview.StillMatches(gateway,gateway.ValuesEquivalent))
            { Prepare();Message("目标地形已变化，预览已更新，请再次确认。");return false; }
            return true;
        }
        private void Confirm()
        {
            if(!ValidateSnapshot())return;
            Start();
        }
        private void Start()
        {
            if(!ValidateSnapshot())return;
            BuildPlan plan=BuildPlanner.Plan(selected,origin,runtime.Palette,gateway,clear,false);
            if(!plan.Success) { Prepare();Message("重新校验失败："+plan.FailureReason);return; }
            var expected=preview.Snapshot;var g=gateway;var cleared=plan.ClearedCells;
            bool started=runtime.QuickStart(plan,selected,g,origin,clear,
                (pos,value) => { int old;return runtime.CanRequest() && (cleared.Contains(pos) || (expected.TryGetValue(pos,out old) && g.ValuesEquivalent(old,value))); });
            if(started)Cancel();
            if(!started)Message("无法开始施工。");
        }
        public void Draw(ComponentGui gui,Camera camera,int order)
        {
            if(order!=9 || controls==null || preview==null || owner==null || gui.m_componentPlayer!=owner.ComponentPlayer ||
                camera.GameWidget!=owner.ComponentPlayer.GameWidget || !object.ReferenceEquals(world,owner.Project))return;
            if(renderer==null)renderer=new PrimitivesRenderer3D();
            FlatBatch3D batch=renderer.FlatBatch(0,DepthStencilState.None);
            Vector3 min=new Vector3(origin.X,origin.Y,origin.Z);
            Vector3 max=min+new Vector3(selected.SizeX,preview.EnvelopeHeight,selected.SizeZ);
            Color color=preview.Ready?new Color(80,255,130):new Color(255,80,80);
            batch.QueueBoundingBox(new BoundingBox(min,max),color);
            Vector3 a=min+new Vector3(0,.04f,0),b=new Vector3(max.X,a.Y,max.Z);
            batch.QueueLine(a,new Vector3(b.X,a.Y,a.Z),color);batch.QueueLine(a,new Vector3(a.X,a.Y,b.Z),color);
            batch.QueueLine(b,new Vector3(b.X,a.Y,a.Z),color);batch.QueueLine(b,new Vector3(a.X,a.Y,b.Z),color);
            batch.QueueBoundingBox(new BoundingBox(min-new Vector3(.1f),min+new Vector3(.3f)),Color.Cyan);
            Vector3 forward=rotation==0?-Vector3.UnitZ:rotation==1?Vector3.UnitX:rotation==2?Vector3.UnitZ:-Vector3.UnitX;
            Vector3 center=new Vector3((min.X+max.X)/2,min.Y+.15f,(min.Z+max.Z)/2);
            Vector3 tip=center+forward*3f,side=new Vector3(forward.Z,0,-forward.X)*.5f;
            batch.QueueLine(center,tip,Color.Cyan);batch.QueueLine(tip,tip-forward+side,Color.Cyan);batch.QueueLine(tip,tip-forward-side,Color.Cyan);
            foreach(CellPos p in preview.Markers)
            {
                Vector3 v=new Vector3(p.X,p.Y,p.Z);
                batch.QueueBoundingBox(new BoundingBox(v+new Vector3(.02f),v+new Vector3(.98f)),preview.Ready?new Color(255,175,40):new Color(255,80,80));
            }
            renderer.Flush(camera.ViewProjectionMatrix);
        }
    }
}
