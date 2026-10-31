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
        private readonly ComponentPlayer player;private readonly DeathAuthorityRuntime runtime;private readonly CanvasWidget panel=new CanvasWidget{Size=new Vector2(540,550),HorizontalAlignment=WidgetAlignment.Center,VerticalAlignment=WidgetAlignment.Center,IsHitTestVisible=false,ClampToBounds=true};
        private readonly RectangleWidget background;
        private readonly BevelledButtonWidget[] rows=new BevelledButtonWidget[DeathAuthorityRules.OwnerLimit];private readonly LabelWidget hint;private readonly BevelledButtonWidget summon,recall,dismiss,close,kind,stow,manifest;private ushort selected,armed,summonKind;
        private readonly LabelWidget[] health=new LabelWidget[DeathAuthorityRules.OwnerLimit];
        internal DeathArmyDialog(ComponentPlayer player,DeathAuthorityRuntime runtime)
        {
            this.player=player;this.runtime=runtime;Children.Add(panel);
            background=new RectangleWidget{Size=panel.Size,FillColor=new Color(15,8,24,245),OutlineColor=new Color(110,65,150),OutlineThickness=2,IsHitTestVisible=false};panel.Children.Add(background);
            Place(new LabelWidget{Text="冥界军势 · 最多七只",FontScale=.7f,IsHitTestVisible=false},16,12,380,30,.7f);
            kind=Button("召唤类型：冥界猎犬",16,50,508,36);
            for(int i=0;i<rows.Length;i++)
            {
                rows[i]=Button("空位",16,96+i*39,326,35);
                health[i]=new LabelWidget{FontScale=.44f,TextAnchor=TextAnchor.HorizontalCenter|TextAnchor.VerticalCenter,IsHitTestVisible=false};
                Place(health[i],350,96+i*39,174,35,.44f);
            }
            hint=new LabelWidget{Text="解除契约后生物直接消失",FontScale=.48f,WordWrap=true,IsHitTestVisible=false};Place(hint,16,375,508,44,.48f);
            summon=Button("召唤",16,428,152,44);recall=Button("召回友军",192,428,152,44);dismiss=Button("解除所选",368,428,152,44);close=Button("关闭",414,12,108,34);
            stow=Button("收回次位面",16,488,244,44);manifest=Button("召回现世",280,488,244,44);
            runtime.Request(player,DeathAuthorityRules.Sync,Vector3.UnitZ);
        }
        private void Place(Widget widget,float x,float y,float w,float h,float font)
        {panel.Children.Add(widget);placements.Add(new Placement{Widget=widget,Position=new Vector2(x,y),Size=new Vector2(w,h),Font=font});}
        private BevelledButtonWidget Button(string text,float x,float y,float w,float h){var b=new BevelledButtonWidget{Text=text,FontScale=.55f};Place(b,x,y,w,h,.55f);return b;}
        public override void MeasureOverride(Vector2 available)
        {
            // Target Widget.Arrange does not divide ActualSize by LayoutTransform's scale.
            // Scale coordinates/sizes/fonts together, leaving the actual widget transform at identity.
            float scale=Math.Min(1,Math.Min(Math.Max(0,available.X-24)/540,Math.Max(0,available.Y-24)/550));
            if(!WorldCutRules.Finite(scale)||scale<=0)scale=.1f;
            panel.LayoutTransform=Matrix.Identity;panel.Size=new Vector2(540,550)*scale;background.Size=panel.Size;
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
            double cooldown=runtime.Remaining(player,DeathAuthorityRules.Summon);summon.Text=cooldown>0?"召唤 · "+Math.Ceiling(cooldown).ToString("0")+"秒":"召唤";
            if(kind.IsClicked)summonKind=(ushort)((summonKind+1)%3);kind.Text=summonKind==0?"召唤类型：冥界猎犬":summonKind==1?"召唤类型：夜魇战马 · 移速10":"召唤类型：地狱三头犬·刻尔柏洛斯（唯一）";
            if(summon.IsClicked)runtime.Request(player,DeathAuthorityRules.Summon,Vector3.UnitZ,summonKind);
            if(recall.IsClicked)runtime.Request(player,DeathAuthorityRules.Recall,Vector3.UnitZ);
            if(stow.IsClicked)runtime.Request(player,DeathAuthorityRules.Stow,Vector3.UnitZ);
            if(manifest.IsClicked)runtime.Request(player,DeathAuthorityRules.Manifest,Vector3.UnitZ);
            if(dismiss.IsClicked){if(armed==selected){if(runtime.Request(player,DeathAuthorityRules.Dismiss,Vector3.UnitZ,selected))selected=armed=0;}else armed=selected;}
            hint.Text=runtime.Feedback(player)??(armed!=0?"再次点击确认解除：生物将直接消失":cooldown>0?"召唤冷却中；收纳、现身和召回仍可使用":"解除后直接消失；次位面保留契约并占用七只名额");
            if(close.IsClicked||Input.Back||Input.Cancel||!runtime.IsDeath(player)||player.ComponentHealth.Health<=0||Terrain.ExtractContents(player.ComponentMiner.ActiveBlockValue)!=952){Input.Clear();DialogsManager.HideDialog(this);}
        }
    }
}
