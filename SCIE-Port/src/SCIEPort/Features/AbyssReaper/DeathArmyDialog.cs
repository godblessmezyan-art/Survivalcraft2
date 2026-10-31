using System;
using Engine;
using Game;
namespace SCIEPort.Features.AbyssReaper
{
    internal sealed class DeathArmyDialog:Dialog
    {
        private readonly ComponentPlayer player;private readonly DeathAuthorityRuntime runtime;private readonly CanvasWidget panel=new CanvasWidget{Size=new Vector2(540,450),HorizontalAlignment=WidgetAlignment.Center,VerticalAlignment=WidgetAlignment.Center,IsHitTestVisible=false};
        private readonly BevelledButtonWidget[] rows=new BevelledButtonWidget[6];private readonly LabelWidget hint;private readonly BevelledButtonWidget summon,recall,dismiss,close;private ushort selected,armed;
        internal DeathArmyDialog(ComponentPlayer player,DeathAuthorityRuntime runtime)
        {
            this.player=player;this.runtime=runtime;Children.Add(panel);
            var background=new RectangleWidget{Size=panel.Size,FillColor=new Color(15,8,24,245),OutlineColor=new Color(110,65,150),OutlineThickness=2,IsHitTestVisible=false};panel.Children.Add(background);
            var title=new LabelWidget{Text="冥界军势 · 每人最多6个友军",Size=new Vector2(510,30),FontScale=.7f};panel.Children.Add(title);panel.SetWidgetPosition(title,new Vector2(16,12));
            for(int i=0;i<6;i++){rows[i]=Button("空位",16,55+i*42,508,38);}
            hint=new LabelWidget{Text="解除普通契约会恢复野生行为",Size=new Vector2(508,44),FontScale=.48f,WordWrap=true};panel.Children.Add(hint);panel.SetWidgetPosition(hint,new Vector2(16,312));
            summon=Button("召唤猎犬",16,366,152,44);recall=Button("召回友军",192,366,152,44);dismiss=Button("解除所选",368,366,152,44);close=Button("关闭",414,12,108,34);
            runtime.Request(player,DeathAuthorityRules.Sync,Vector3.UnitZ);
        }
        private BevelledButtonWidget Button(string text,float x,float y,float w,float h){var b=new BevelledButtonWidget{Text=text,Size=new Vector2(w,h),FontScale=.55f};panel.Children.Add(b);panel.SetWidgetPosition(b,new Vector2(x,y));return b;}
        public override void Update()
        {
            if(!player.IsAddedToProject||player.GameWidget==null){DialogsManager.HideDialog(this);return;}
            var available=player.GuiWidget.ActualSize;float scale=Math.Min(1,Math.Min((available.X-16)/540,(available.Y-16)/450));if(scale>0)panel.LayoutTransform=Matrix.CreateScale(scale);
            var list=runtime.Friends(player.PlayerGuid);list.Sort((a,b)=>a.Id.CompareTo(b.Id));
            for(int i=0;i<6;i++){rows[i].IsEnabled=i<list.Count;rows[i].Text=i<list.Count?(selected==list[i].Id?"● ":"")+list[i].Name:"— 空位 —";if(rows[i].IsClicked&&i<list.Count){selected=list[i].Id;armed=0;}}
            hint.Text=runtime.Feedback(player)??"解除普通契约会恢复野生；专属猎犬解散后消失";dismiss.Text=armed==selected&&selected!=0?"确认解除":"解除所选";dismiss.IsEnabled=selected!=0;
            if(summon.IsClicked)runtime.Request(player,DeathAuthorityRules.Summon,Vector3.UnitZ);
            if(recall.IsClicked)runtime.Request(player,DeathAuthorityRules.Recall,Vector3.UnitZ);
            if(dismiss.IsClicked){if(armed==selected){runtime.Request(player,DeathAuthorityRules.Dismiss,Vector3.UnitZ,selected);selected=armed=0;}else armed=selected;}
            if(close.IsClicked||Input.Cancel||!runtime.IsDeath(player)||player.ComponentHealth.Health<=0||Terrain.ExtractContents(player.ComponentMiner.ActiveBlockValue)!=952)DialogsManager.HideDialog(this);
        }
    }
}
