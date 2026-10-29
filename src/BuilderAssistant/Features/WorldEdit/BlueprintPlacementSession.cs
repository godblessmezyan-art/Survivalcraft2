using System;
using BuilderAssistant.Core;
using BuilderAssistant.Core.Blueprints;
using BuilderAssistant.Core.BuildEngine;
using BuilderAssistant.Core.Safety;
using BuilderAssistant.Core.Workbench;
using Engine;
using Engine.Graphics;
using Game;

namespace BuilderAssistant.Features.WorldEdit
{
    internal static class BlueprintPlacementSession
    {
        private static ComponentMiner owner;
        private static object world;
        private static Dialog dialog;
        private static Blueprint original, selected;
        private static bool selecting, clear;
        private static int wait, retry, rotation, height;
        private static CellPos origin, ground;
        private static GameTerrainGateway gateway;
        private static BlueprintPlacementPreview preview;
        private static BlueprintPreviewControls controls;
        private static PrimitivesRenderer3D renderer;

        public static void Reset()
        {
            Cancel(); owner=null; world=null; original=null; renderer=null;
        }
        internal static void OnWorldDisposed(object project) { if(object.ReferenceEquals(world,project))Reset(); }
        private static void Close()
        {
            var manager=dialog as BlueprintToolDialog;
            if(manager!=null)manager.CloseChildren();
            if(dialog!=null)DialogsManager.HideDialog(dialog);dialog=null;
        }
        private static void Cancel()
        {
            Close();
            if(controls!=null && controls.ParentWidget!=null) controls.ParentWidget.Children.Remove(controls);
            controls=null;selecting=false;wait=0;selected=null;original=null;gateway=null;preview=null;
        }
        private static void Message(string text)
        {
            if(owner!=null && owner.ComponentPlayer!=null)owner.ComponentPlayer.ComponentGui.DisplaySmallMessage("[蓝图] "+text,Color.White,false,false);
        }
        public static void Tick(ComponentGui gui)
        {
            if(owner==null) return;
            if(gui==null || gui.m_componentPlayer==null)return;
            if(!object.ReferenceEquals(world,gui.m_componentPlayer.Project)) { Reset();return; }
            if(gui.m_componentPlayer!=owner.ComponentPlayer)return;
            if(!owner.Entity.IsAddedToProject || owner.ComponentPlayer.ComponentHealth.Health<=0 ||
                Terrain.ExtractContents(owner.ActiveBlockValue)!=BlueprintToolBlock.Index)
            { Reset();return; }
            if(dialog!=null && dialog.ParentWidget!=null && !DialogsManager.Dialogs.Contains(dialog)) { Cancel();return; }
            if(gateway!=null && (!TerrainAuthority.IsWritable(WorldEditRuntime.CurrentAuthority()) || WorldEditRuntime.IsBusy))
            { Cancel();Message("权限或施工状态已改变，预览已取消。");return; }
            if(wait>0 && --retry<=0)
            {
                wait--;retry=15;Prepare();
                if(wait==0 && preview!=null && !preview.Ready)UpdateStatus("区块等待结束，可重新点地选择位置。");
            }
        }
        public static bool OnUse(ComponentMiner miner,Ray3 ray)
        {
            if(miner==null || miner.ComponentPlayer==null || miner.ComponentPlayer.GameWidget==null)return true;
            if(!object.ReferenceEquals(world,miner.Project) || (owner!=null && owner!=miner))Reset();
            owner=miner;world=miner.Project;
            if(dialog!=null)return true;
            if(WorldEditRuntime.IsBusy) { Message("正在施工，请稍候。");return true; }
            if(!selecting && controls==null) { Open();return true; }
            var hit=miner.Raycast<TerrainRaycastResult>(ray,RaycastMode.Interaction);
            if(!hit.HasValue) { Cancel();Message("已取消放置。");return true; }
            if(miner.m_subsystemTerrain==null || !TerrainAuthority.IsWritable(WorldEditRuntime.CurrentAuthority()))
            { Cancel();Message("仅单人或房主可以建造。");return true; }
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
        private static void Open()
        {
            Cancel();
            dialog=new BlueprintToolDialog(owner.ComponentPlayer,Begin,Cancel);
            DialogsManager.ShowDialog(owner.ComponentPlayer.GameWidget.GuiWidget,dialog);
        }
        private static void Begin(int direction,bool clearSetting)
        {
            Blueprint bp=WorldEditRuntime.ClipboardBlueprint;
            string error=WorldEditRuntime.QuickValidate(bp);
            if(error!=null) { Message(error);return; }
            if(!TerrainAuthority.IsWritable(WorldEditRuntime.CurrentAuthority())) { Message("联机客户端只能浏览；仅房主可建造。");return; }
            if(WorldEditRuntime.IsBusy) { Message("正在施工或等待区块。");return; }
            original=bp;rotation=direction;height=0;clear=clearSetting;
            selected=bp.Reorient(rotation,false);Close();selecting=true;
            Message("点地选择落点并预览，之后可转向、升降、重新点地；点天空取消。");
        }
        private static void Adjust(int delta)
        {
            if(dialog!=null || gateway==null)return;
            if(delta==0) { rotation=(rotation+1)%4;selected=original.Reorient(rotation,false); }
            else height=MathUtils.Clamp(height+delta,-64,64);
            Reposition();
        }
        private static void Reposition()
        {
            CellPos baseOrigin=WorldEditRuntime.WorkbenchOriginFor(selected,ground);
            height=MathUtils.Clamp(height,-baseOrigin.Y,WorldBounds.MaxYExclusive-BlueprintPlacementPreview.HeightOf(selected)-baseOrigin.Y);
            origin=baseOrigin.Add(new CellPos(0,height,0));
            wait=10;retry=15;preview=null;Prepare();
        }
        private static void Prepare()
        {
            if(gateway==null || selected==null)return;
            preview=BlueprintPlacementPreview.Inspect(selected,origin,WorldEditRuntime.Palette,gateway,clear);
            if(!preview.Plan.AllEditabilityFailuresRecoverable)wait=0;
            UpdateStatus(null);
        }
        private static void UpdateStatus(string extra)
        {
            if(controls==null || preview==null)return;
            string state=preview.Ready ? (preview.OccupiedCount>0 ? "将覆盖 "+preview.OccupiedCount+" 格，需确认清障" : "允许施工") : "不可施工："+preview.Plan.FailureReason;
            controls.SetStatus(selected.Name+"  "+selected.SizeX+"×"+preview.EnvelopeHeight+"×"+selected.SizeZ+"  "+(rotation*90)+"°\n原点 "+origin+"；Y="+origin.Y+".."+(origin.Y+preview.EnvelopeHeight-1)+"\n"+(extra??state)+"\n绿=范围，蓝=原点/朝向，橙=占用，红=不可施工；点地重选",preview.Ready);
        }
        private static bool ValidateSnapshot()
        {
            if(owner==null || gateway==null || preview==null || !preview.Ready || WorldEditRuntime.IsBusy ||
                !owner.Entity.IsAddedToProject || owner.ComponentPlayer.ComponentHealth.Health<=0 ||
                Terrain.ExtractContents(owner.ActiveBlockValue)!=BlueprintToolBlock.Index ||
                !TerrainAuthority.IsWritable(WorldEditRuntime.CurrentAuthority()))return false;
            if(!preview.StillMatches(gateway,gateway.ValuesEquivalent))
            { Prepare();Message("目标地形已变化，预览已更新，请再次确认。");return false; }
            return true;
        }
        private static void Confirm()
        {
            if(!ValidateSnapshot())return;
            if(preview.OccupiedCount>0)
            {
                dialog=new BlueprintClearConfirmDialog(owner.ComponentPlayer,selected,origin,preview.OccupiedCount,
                    () => { Close();Start(); },() => { Close();Prepare(); });
                DialogsManager.ShowDialog(owner.ComponentPlayer.GameWidget.GuiWidget,dialog);
            }
            else Start();
        }
        private static void Start()
        {
            if(!ValidateSnapshot())return;
            BuildPlan plan=BuildPlanner.Plan(selected,origin,WorldEditRuntime.Palette,gateway,clear,false);
            if(!plan.Success) { Prepare();Message("重新校验失败："+plan.FailureReason);return; }
            var expected=preview.Snapshot;var g=gateway;
            bool started=WorldEditRuntime.QuickStart(plan,selected,g,origin,clear,
                (pos,value) => { int old;return TerrainAuthority.IsWritable(WorldEditRuntime.CurrentAuthority()) && expected.TryGetValue(pos,out old) && g.ValuesEquivalent(old,value); });
            if(started)Cancel();
            Message(started?"开始施工；可使用 902 撤销。":"无法开始施工，未修改地形。");
        }
        public static void Draw(ComponentGui gui,Camera camera,int order)
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
