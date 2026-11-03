using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;
namespace SCIEPort.Features.AbyssReaper
{
    internal sealed class DeathArmyDialog:Dialog
    {
        private sealed class Placement{internal Widget Widget;internal Vector2 Position,Size;internal float Font;}
        private readonly List<Placement> placements=new List<Placement>();
        private readonly ComponentPlayer player;private readonly DeathAuthorityRuntime runtime;private readonly CanvasWidget panel=new CanvasWidget{Size=new Vector2(540,586),HorizontalAlignment=WidgetAlignment.Center,VerticalAlignment=WidgetAlignment.Center,IsHitTestVisible=false,ClampToBounds=true};
        private readonly RectangleWidget background;
        private readonly BevelledButtonWidget[] rows=new BevelledButtonWidget[DeathAuthorityRules.OwnerLimit];private readonly LabelWidget hint;private readonly BevelledButtonWidget summon,recall,dismiss,close,stow,manifest;private ushort selected,armed;
        private readonly LabelWidget[] health=new LabelWidget[DeathAuthorityRules.OwnerLimit];
        private readonly BevelledButtonWidget ridingMode,selectedAction,behaviorMode;
        internal DeathArmyDialog(ComponentPlayer player,DeathAuthorityRuntime runtime)
        {
            this.player=player;this.runtime=runtime;Children.Add(panel);
            background=new RectangleWidget{Size=panel.Size,FillColor=new Color(15,8,24,245),OutlineColor=new Color(110,65,150),OutlineThickness=2,IsHitTestVisible=false};panel.Children.Add(background);
            Place(new LabelWidget{Text="冥界军势",FontScale=.7f,IsHitTestVisible=false},16,12,380,30,.7f);
            for(int i=0;i<rows.Length;i++)
            {
                rows[i]=Button("空位",16,50+i*39,326,35);
                health[i]=new LabelWidget{FontScale=.44f,TextAnchor=TextAnchor.HorizontalCenter|TextAnchor.VerticalCenter,IsHitTestVisible=false};
                Place(health[i],350,50+i*39,174,35,.44f);
            }
            behaviorMode=Button("行动模式",16,329,508,40);
            hint=new LabelWidget{Text="",FontScale=.48f,WordWrap=true,IsHitTestVisible=false,IsVisible=false};Place(hint,16,374,508,38,.48f);
            summon=Button("召唤",16,422,152,44);recall=Button("召回友军",192,422,152,44);dismiss=Button("解除所选",368,422,152,44);close=Button("关闭",414,12,108,34);
            stow=Button("全部收回",16,478,244,44);manifest=Button("全部召出",280,478,244,44);
            ridingMode=Button("骑乘追击",16,534,244,40);selectedAction=Button("单只操作",280,534,244,40);
            runtime.Request(player,DeathAuthorityRules.Sync,Vector3.UnitZ);
        }
        private void Place(Widget widget,float x,float y,float w,float h,float font)
        {panel.Children.Add(widget);placements.Add(new Placement{Widget=widget,Position=new Vector2(x,y),Size=new Vector2(w,h),Font=font});}
        private BevelledButtonWidget Button(string text,float x,float y,float w,float h){var b=new BevelledButtonWidget{Text=text,FontScale=.55f};Place(b,x,y,w,h,.55f);return b;}
        public override void MeasureOverride(Vector2 available)
        {
            // Target Widget.Arrange does not divide ActualSize by LayoutTransform's scale.
            // Scale coordinates/sizes/fonts together, leaving the actual widget transform at identity.
            float scale=Math.Min(1,Math.Min(Math.Max(0,available.X-24)/540,Math.Max(0,available.Y-24)/586));
            if(!WorldCutRules.Finite(scale)||scale<=0)scale=.1f;
            panel.LayoutTransform=Matrix.Identity;panel.Size=new Vector2(540,586)*scale;background.Size=panel.Size;
            foreach(var item in placements)
            {
                panel.SetWidgetPosition(item.Widget,item.Position*scale);
                var canvas=item.Widget as CanvasWidget;if(canvas!=null)canvas.Size=item.Size*scale;
                var label=item.Widget as LabelWidget;if(label!=null){label.Size=item.Size*scale;label.FontScale=item.Font*scale;}
                var button=item.Widget as BevelledButtonWidget;if(button!=null)button.FontScale=item.Font*scale;
            }
            base.MeasureOverride(available);
        }
        public override void Update()
        {
            if(!player.IsAddedToProject||player.GameWidget==null){DialogsManager.HideDialog(this);return;}
            var list=runtime.Friends(player.PlayerGuid);list.Sort((a,b)=>a.Id.CompareTo(b.Id));
            bool exists=false;
            for(int i=0;i<rows.Length;i++)
            {
                rows[i].IsEnabled=i<list.Count;health[i].IsVisible=i<list.Count;
                if(i<list.Count)
                {
                    var row=list[i];string name=row.Name.Length>16?row.Name.Substring(0,15)+"…":row.Name;
                    rows[i].Text=(selected==row.Id?"● ":"")+(row.Hidden?"[次位面] ":row.Pending?"[待加载] ":"")+name;
                    health[i].Text=DeathAuthorityRuntime.HealthText(row);
                    health[i].Color=!row.HealthKnown||row.Pending?new Color(175,175,190):row.HealthFraction<=.25f?new Color(255,110,115):row.HealthFraction<=.5f?new Color(255,210,105):new Color(145,235,175);
                    if(row.Id==selected)exists=true;
                    if(rows[i].IsClicked){selected=row.Id;armed=0;exists=true;}
                }
                else rows[i].Text="— 空位 —";
            }
            if(!exists)selected=armed=0;
            dismiss.Text=armed==selected&&selected!=0?"确认解除":"解除所选";dismiss.IsEnabled=selected!=0;
            summon.Text="召唤";summon.IsEnabled=runtime.Remaining(player,DeathAuthorityRules.Summon)<=0;
            if(summon.IsClicked)runtime.Request(player,DeathAuthorityRules.Summon,Vector3.UnitZ);
            if(recall.IsClicked)runtime.Request(player,DeathAuthorityRules.Recall,Vector3.UnitZ);
            if(stow.IsClicked)runtime.Request(player,DeathAuthorityRules.Stow,Vector3.UnitZ);
            if(manifest.IsClicked)runtime.Request(player,DeathAuthorityRules.Manifest,Vector3.UnitZ);
            var mount=runtime.RiddenFriend(player);ridingMode.IsEnabled=mount!=null;
            ridingMode.Text=mount==null?"骑乘追击":"骑乘追击："+(mount.RidingAuto?"开启":"关闭");
            if(ridingMode.IsClicked&&mount!=null)runtime.Request(player,DeathAuthorityRules.RidingMode,Vector3.UnitZ,mount.Id);
            var chosen=list.Find(row=>row.Id==selected);selectedAction.IsEnabled=chosen!=null&&!chosen.Pending;
            behaviorMode.IsEnabled=chosen!=null;
            behaviorMode.Text=chosen==null?"行动模式":"模式："+DeathAuthorityRuntime.ModeText(chosen.Mode);
            if(behaviorMode.IsClicked&&chosen!=null)runtime.Request(player,(byte)(DeathAuthorityRules.GuardMode+((int)chosen.Mode+1)%3),Vector3.UnitZ,chosen.Id);
            selectedAction.Text=chosen==null?"单只操作":chosen.Pending?"待加载":chosen.Hidden?"召出所选":"收回所选";
            if(selectedAction.IsClicked&&chosen!=null&&!chosen.Pending)runtime.Request(player,chosen.Hidden?DeathAuthorityRules.ManifestOne:DeathAuthorityRules.StowOne,Vector3.UnitZ,chosen.Id);
            if(dismiss.IsClicked){if(armed==selected){if(runtime.Request(player,DeathAuthorityRules.Dismiss,Vector3.UnitZ,selected))selected=armed=0;}else armed=selected;}
            hint.Text=armed!=0?"解除后生物将消失":runtime.Feedback(player)??"";hint.IsVisible=hint.Text.Length>0;
            if(close.IsClicked||Input.Back||Input.Cancel||!runtime.IsDeath(player)||player.ComponentHealth.Health<=0||Terrain.ExtractContents(player.ComponentMiner.ActiveBlockValue)!=952){Input.Clear();DialogsManager.HideDialog(this);}
        }
    }
}
