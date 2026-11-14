using Engine;using Game;
namespace SCIEPort.Features.Codex
{
    internal sealed class CodexDialog:Dialog
    {
        private readonly ComponentPlayer player;
        private readonly CanvasWidget panel=new CanvasWidget{Size=new Vector2(660,460),HorizontalAlignment=WidgetAlignment.Center,VerticalAlignment=WidgetAlignment.Center};
        private readonly LabelWidget title,info;private readonly BlockIconWidget icon;private readonly BevelledButtonWidget close;
        private readonly BevelledButtonWidget[] entries=new BevelledButtonWidget[CodexRules.Items.Length];private int selected;
        internal CodexDialog(ComponentPlayer p)
        {
            player=p;Children.Add(panel);
            Add(new RectangleWidget{Size=panel.Size,FillColor=new Color(232,222,191,252),OutlineColor=new Color(149,111,48),OutlineThickness=3},0,0);
            Add(new RectangleWidget{Size=new Vector2(232,460),FillColor=new Color(24,31,59),OutlineThickness=0},0,0);
            Add(new LabelWidget{Text="神器图谱",Color=new Color(247,221,149),FontScale=1},24,18);
            var listFrame=new CanvasWidget{Size=new Vector2(216,374)};Add(listFrame,8,66);
            var listScroll=new ScrollPanelWidget{Direction=LayoutDirection.Vertical,HorizontalAlignment=WidgetAlignment.Stretch,VerticalAlignment=WidgetAlignment.Stretch};listFrame.Children.Add(listScroll);
            var list=new CanvasWidget{Size=new Vector2(208,entries.Length*52),HorizontalAlignment=WidgetAlignment.Near,VerticalAlignment=WidgetAlignment.Near};listScroll.Children.Add(list);
            for(int i=0;i<entries.Length;i++){var b=Button(CodexRules.Names[i],0,0,200);panel.Children.Remove(b);b.Size=new Vector2(200,44);list.Children.Add(b);list.SetWidgetPosition(b,new Vector2(4,i*52));entries[i]=b;}
            title=new LabelWidget{Size=new Vector2(388,38),FontScale=.9f,Color=new Color(53,39,30)};Add(title,252,20);
            icon=new BlockIconWidget{Size=new Vector2(116,116)};icon.DrawBlockEnvironmentData.SubsystemTerrain=p.Project.FindSubsystem<SubsystemTerrain>(true);Add(icon,382,65);
            var scrollFrame=new CanvasWidget{Size=new Vector2(376,142)};Add(scrollFrame,254,190);
            var scroll=new ScrollPanelWidget{HorizontalAlignment=WidgetAlignment.Stretch,VerticalAlignment=WidgetAlignment.Stretch};scrollFrame.Children.Add(scroll);
            info=new LabelWidget{Size=new Vector2(360,float.PositiveInfinity),FontScale=.65f,Color=new Color(55,47,40),WordWrap=true};scroll.Children.Add(info);
            close=Button("关闭",486,392,150);
        }
        private void Add(Widget w,float x,float y){panel.Children.Add(w);panel.SetWidgetPosition(w,new Vector2(x,y));}
        private BevelledButtonWidget Button(string text,float x,float y,float width){var b=new BevelledButtonWidget{Text=text,Size=new Vector2(width,38),FontScale=.65f,CenterColor=new Color(39,45,70),BevelColor=new Color(177,142,78)};Add(b,x,y);return b;}
        /// <inheritdoc/>
        public override void Update()
        {
            if(!SubsystemArtifactCodex.Held(player,CodexRules.Book)||close.IsClicked||Input.Cancel||Input.Back){Input.Clear();DialogsManager.HideDialog(this);return;}
            var size=player.GuiWidget.ActualSize;float scale=MathUtils.Min(1,MathUtils.Min((size.X-16)/660,(size.Y-16)/460));if(scale>0)panel.LayoutTransform=Matrix.CreateScale(scale);
            for(int i=0;i<entries.Length;i++){if(entries[i].IsClicked)selected=i;entries[i].Text=CodexRules.Names[i];}
            int id=CodexRules.Items[selected];title.Text=CodexRules.Names[selected];icon.Value=Terrain.MakeBlockValue(id,15,0);
            info.Text=CodexRules.Information[selected];
        }
    }
}
