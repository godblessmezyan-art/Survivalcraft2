using System;
using Engine;
using Game;
namespace SCIEPort.Features.AbyssReaper
{
    internal sealed class DeathArmyDialog:Dialog
    {
        private readonly ComponentPlayer player;private readonly DeathAuthorityRuntime runtime;private readonly CanvasWidget panel=new CanvasWidget{Size=new Vector2(540,550),HorizontalAlignment=WidgetAlignment.Center,VerticalAlignment=WidgetAlignment.Center,IsHitTestVisible=false};
        private readonly BevelledButtonWidget[] rows=new BevelledButtonWidget[DeathAuthorityRules.OwnerLimit];private readonly LabelWidget hint;private readonly BevelledButtonWidget summon,recall,dismiss,close,kind,stow,manifest;private ushort selected,armed,summonKind;
        internal DeathArmyDialog(ComponentPlayer player,DeathAuthorityRuntime runtime)
        {
            this.player=player;this.runtime=runtime;Children.Add(panel);
            var background=new RectangleWidget{Size=panel.Size,FillColor=new Color(15,8,24,245),OutlineColor=new Color(110,65,150),OutlineThickness=2,IsHitTestVisible=false};panel.Children.Add(background);
            var title=new LabelWidget{Text="冥界军势 · 最多七只",Size=new Vector2(380,30),FontScale=.7f};panel.Children.Add(title);panel.SetWidgetPosition(title,new Vector2(16,12));
            kind=Button("召唤类型：冥界猎犬",16,50,508,36);
            for(int i=0;i<rows.Length;i++){rows[i]=Button("空位",16,96+i*39,508,35);}
            hint=new LabelWidget{Text="解除契约后生物直接消失",Size=new Vector2(508,44),FontScale=.48f,WordWrap=true};panel.Children.Add(hint);panel.SetWidgetPosition(hint,new Vector2(16,375));
            summon=Button("召唤",16,428,152,44);recall=Button("召回友军",192,428,152,44);dismiss=Button("解除所选",368,428,152,44);close=Button("关闭",414,12,108,34);
            stow=Button("收回次位面",16,488,244,44);manifest=Button("召回现世",280,488,244,44);
            runtime.Request(player,DeathAuthorityRules.Sync,Vector3.UnitZ);
        }
        private BevelledButtonWidget Button(string text,float x,float y,float w,float h){var b=new BevelledButtonWidget{Text=text,Size=new Vector2(w,h),FontScale=.55f};panel.Children.Add(b);panel.SetWidgetPosition(b,new Vector2(x,y));return b;}
        public override void Update()
        {
            if(!player.IsAddedToProject||player.GameWidget==null){DialogsManager.HideDialog(this);return;}
            var available=player.GuiWidget.ActualSize;float scale=Math.Min(1,Math.Min((available.X-16)/540,(available.Y-16)/550));if(scale>0)panel.LayoutTransform=Matrix.CreateScale(scale);
            var list=runtime.Friends(player.PlayerGuid);list.Sort((a,b)=>a.Id.CompareTo(b.Id));
            bool exists=false;for(int i=0;i<rows.Length;i++){rows[i].IsEnabled=i<list.Count;rows[i].Text=i<list.Count?(selected==list[i].Id?"● ":"")+(list[i].Hidden?"[次位面] ":list[i].Pending?"[待加载] ":"")+list[i].Name:"— 空位 —";if(i<list.Count&&list[i].Id==selected)exists=true;if(rows[i].IsClicked&&i<list.Count){selected=list[i].Id;armed=0;exists=true;}}if(!exists)selected=armed=0;
            hint.Text=runtime.Feedback(player)??"解除后直接消失；次位面保留契约并占用七只名额";dismiss.Text=armed==selected&&selected!=0?"确认解除":"解除所选";dismiss.IsEnabled=selected!=0;
            if(kind.IsClicked)summonKind=(ushort)(summonKind==0?1:0);kind.Text=summonKind==0?"召唤类型：冥界猎犬":"召唤类型：夜魇战马 · 速度170%";
            if(summon.IsClicked)runtime.Request(player,DeathAuthorityRules.Summon,Vector3.UnitZ,summonKind);
            if(recall.IsClicked)runtime.Request(player,DeathAuthorityRules.Recall,Vector3.UnitZ);
            if(stow.IsClicked)runtime.Request(player,DeathAuthorityRules.Stow,Vector3.UnitZ);
            if(manifest.IsClicked)runtime.Request(player,DeathAuthorityRules.Manifest,Vector3.UnitZ);
            if(dismiss.IsClicked){if(armed==selected){runtime.Request(player,DeathAuthorityRules.Dismiss,Vector3.UnitZ,selected);selected=armed=0;}else armed=selected;}
            if(close.IsClicked||Input.Cancel||!runtime.IsDeath(player)||player.ComponentHealth.Health<=0||Terrain.ExtractContents(player.ComponentMiner.ActiveBlockValue)!=952)DialogsManager.HideDialog(this);
        }
    }
}
