using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;
namespace SCIEPort.Features.AbyssReaper
{
    internal sealed class VoidPortalDialog:Dialog
    {
        private SubsystemAbyssReaper runtime;
        private ComponentPlayer player;
        private Guid source,selected,created;
        private readonly CanvasWidget panel;
        private readonly LabelWidget subtitle,pages;
        private readonly TextBoxWidget name;
        private readonly BevelledButtonWidget[] rows=new BevelledButtonWidget[5];
        private readonly Guid[] ids=new Guid[5];
        private readonly BevelledButtonWidget previous,next,refresh,create,rename,delete,travel,close;
        private int page;
        private Guid deleteArmed;
        internal VoidPortalDialog(SubsystemAbyssReaper runtime,ComponentPlayer player,Guid source)
        {
            this.runtime=runtime;this.player=player;this.source=source;
            created=runtime.Gateways.LastCreated;
            panel=new CanvasWidget{Size=new Vector2(620,464),HorizontalAlignment=WidgetAlignment.Center,VerticalAlignment=WidgetAlignment.Center,IsHitTestVisible=false};Children.Add(panel);
            panel.Children.Add(new RectangleWidget{Size=panel.Size,FillColor=new Color(12,5,20,248),OutlineColor=new Color(136,76,184),OutlineThickness=2,IsHitTestVisible=false});
            Add(new LabelWidget{Text="裂界星门 · 世界共享",FontScale=.85f,Color=new Color(228,202,250),IsHitTestVisible=false},16,10);
            subtitle=new LabelWidget{Size=new Vector2(588,36),FontScale=.43f,WordWrap=true,Color=new Color(210,190,226),IsHitTestVisible=false};Add(subtitle,16,42);
            for(int i=0;i<5;i++)rows[i]=Button("",16,84+i*42,588,38);
            name=new TextBoxWidget{Size=new Vector2(430,38),MaximumLength=32,FontScale=.65f,Text="星门"};Add(name,16,302);
            rename=Button("保存名称",454,302,150,38);
            previous=Button("上一页",16,350,100,36);next=Button("下一页",224,350,100,36);refresh=Button("刷新列表",464,350,140,36);
            pages=new LabelWidget{Size=new Vector2(100,36),FontScale=.55f,TextAnchor=TextAnchor.HorizontalCenter|TextAnchor.VerticalCenter,IsHitTestVisible=false};Add(pages,120,350);
            create=Button("建立永久星门",16,408,164,42);delete=Button("删除所选",188,408,120,42);travel=Button("前往所选星门",316,408,170,42);close=Button("关闭",494,408,110,42);
        }
        private void Add(Widget w,float x,float y){panel.Children.Add(w);CanvasWidget.SetPosition(w,new Vector2(x,y));}
        private BevelledButtonWidget Button(string text,float x,float y,float width,float height)
        {var b=new BevelledButtonWidget{Text=text,Size=new Vector2(width,height),FontScale=.55f,CenterColor=new Color(27,12,38),BevelColor=new Color(131,88,158)};Add(b,x,y);return b;}
        public override void Update()
        {
            if(runtime==null||player==null)return;
            if(!player.IsAddedToProject||player.GameWidget==null){runtime.ClosePortalDialog(player,this);return;}
            Vector2 available=player.GuiWidget.ActualSize;float scale=MathUtils.Min(1,MathUtils.Min((available.X-16)/620,(available.Y-16)/464));
            if(scale>0)panel.LayoutTransform=Matrix.CreateScale(scale);
            var net=runtime.Gateways;var registry=runtime.Portals;
            if(net.LastCreated!=Guid.Empty&&created!=net.LastCreated){created=net.LastCreated;source=created;}
            var list=new List<VoidPortalRegistry.Portal>();if(registry!=null)foreach(var gate in registry.Records)if(gate.Active)list.Add(gate);
            list.Sort((a,b)=>{int n=string.CompareOrdinal(a.Name,b.Name);return n!=0?n:a.Id.CompareTo(b.Id);});
            if(previous.IsClicked)page--;if(next.IsClicked)page++;
            int count=Math.Max(1,(list.Count+4)/5);page=Math.Max(0,Math.Min(count-1,page));pages.Text=(page+1)+" / "+count;
            previous.IsEnabled=page>0;next.IsEnabled=page+1<count;
            VoidPortalRegistry.Portal chosen=null,origin=null;
            if(registry!=null){registry.TryGet(selected,out chosen);registry.TryGet(source,out origin);}
            if(source!=Guid.Empty&&origin==null)source=Guid.Empty;
            for(int i=0;i<5;i++)
            {
                int at=page*5+i;rows[i].IsVisible=at<list.Count;ids[i]=at<list.Count?list[at].Id:Guid.Empty;
                if(at>=list.Count)continue;var gate=list[at];
                float distance=Vector3.Distance(player.ComponentBody.Position,new Vector3(gate.X,gate.Y,gate.Z));
                rows[i].Text=(gate.Id==selected?"● ":"")+gate.Name+"  ("+gate.X.ToString("0.0")+", "+gate.Y.ToString("0")+", "+gate.Z.ToString("0.0")+")  "+distance.ToString("0")+"格";
                rows[i].CenterColor=gate.Id==selected?new Color(72,35,98):new Color(27,12,38);
                if(rows[i].IsClicked){selected=gate.Id;chosen=gate;name.Text=gate.Name;deleteArmed=Guid.Empty;}
            }
            subtitle.Text=(origin!=null?"出发："+origin.Name:"随处开门：前往时会在附近建立并保留一座永久星门")+"\n"+net.Feedback;
            rename.IsEnabled=delete.IsEnabled=chosen!=null;travel.IsEnabled=chosen!=null&&chosen.Id!=source;
            delete.Text=deleteArmed==selected&&selected!=Guid.Empty?"确认删除":"删除所选";
            if(refresh.IsClicked)net.Request(player,VoidPortalNetwork.Sync,Guid.Empty,Guid.Empty,"");
            if(create.IsClicked)net.Request(player,VoidPortalNetwork.Create,Guid.Empty,Guid.Empty,name.Text.Trim());
            if(rename.IsClicked&&chosen!=null)net.Request(player,VoidPortalNetwork.Rename,chosen.Id,Guid.Empty,name.Text.Trim());
            if(delete.IsClicked&&chosen!=null)
            {
                if(deleteArmed!=chosen.Id)deleteArmed=chosen.Id;
                else{net.Request(player,VoidPortalNetwork.Delete,chosen.Id,Guid.Empty,"");deleteArmed=Guid.Empty;}
            }
            if(travel.IsClicked&&chosen!=null)net.Request(player,VoidPortalNetwork.Choose,source,chosen.Id,"");
            if(close.IsClicked||Input.Back||Input.Cancel){Input.Clear();runtime.ClosePortalDialog(player,this);}
        }
        internal void Release(){runtime=null;player=null;}
    }
}
