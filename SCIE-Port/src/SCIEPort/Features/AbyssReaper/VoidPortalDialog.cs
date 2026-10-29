using System;
using System.Collections.Generic;
using Engine;
using Game;
namespace SCIEPort.Features.AbyssReaper
{
    // Local authority management. Client request/replication and travel remain separate work.
    internal sealed class VoidPortalDialog : Dialog
    {
        private SubsystemAbyssReaper runtime;
        private ComponentPlayer player;
        private readonly Guid source;
        private readonly CanvasWidget panel;
        private readonly TextBoxWidget name;
        private readonly LabelWidget info;
        private readonly BevelledButtonWidget previous,next,link,unlink,rename,delete,close;
        private Guid selected;
        private bool deleteArmed;
        internal VoidPortalDialog(SubsystemAbyssReaper runtime,ComponentPlayer player,Guid source)
        {
            this.runtime=runtime;this.player=player;this.source=source;
            panel=new CanvasWidget { Size=new Vector2(550,360),HorizontalAlignment=WidgetAlignment.Center,VerticalAlignment=WidgetAlignment.Center };Children.Add(panel);
            panel.Children.Add(new RectangleWidget { FillColor=new Color(12,5,20,245),OutlineColor=new Color(136,76,184),OutlineThickness=2 });
            Add(new LabelWidget { Text="裂界星门 · 管理",Color=new Color(222,188,250),FontScale=.85f },16,12);
            name=new TextBoxWidget { Size=new Vector2(310,42),FontScale=.75f,MaximumLength=32 };Add(name,16,52);
            VoidPortalRegistry.Portal current;if(runtime.Portals.TryGet(source,out current))name.Text=current.Name;
            rename=Button("重命名",336,52,95);delete=Button("删除",440,52,94);
            info=new LabelWidget { Size=new Vector2(510,125),Color=new Color(210,177,230),FontScale=.7f,WordWrap=true };Add(info,20,108);
            previous=Button("上一座",16,244,115);next=Button("下一座",140,244,115);
            link=Button("设置目标",264,244,130);unlink=Button("取消目标",402,244,132);
            close=Button("关闭",374,305,160);
            Select(1);
        }
        private void Add(Widget w,float x,float y){panel.Children.Add(w);panel.SetWidgetPosition(w,new Vector2(x,y));}
        private BevelledButtonWidget Button(string text,float x,float y,float width)
        { var b=new BevelledButtonWidget { Text=text,Size=new Vector2(width,42),FontScale=.65f,CenterColor=new Color(27,12,38),BevelColor=new Color(131,88,158) };Add(b,x,y);return b; }
        private List<VoidPortalRegistry.Portal> Destinations()
        {
            var result=new List<VoidPortalRegistry.Portal>();
            if(runtime?.Portals!=null)foreach(var p in runtime.Portals.Records)if(p.Active && p.Id!=source)result.Add(p);
            result.Sort((a,b)=>string.CompareOrdinal(a.Name,b.Name));return result;
        }
        private void Select(int step)
        {
            var list=Destinations();if(list.Count==0){selected=Guid.Empty;return;}
            int i=list.FindIndex(p=>p.Id==selected);if(i<0)i=step>0?-1:0;
            selected=list[(i+step+list.Count)%list.Count].Id;
        }
        public override void Update()
        {
            if(runtime==null || player==null)return;
            if(!player.IsAddedToProject || player.GameWidget==null){runtime.ClosePortalDialog(player,this);return;}
            Vector2 available=player.GameWidget.GuiWidget.ActualSize;
            float scale=MathUtils.Min(1,MathUtils.Min((available.X-16)/550,(available.Y-16)/360));if(scale>0)panel.LayoutTransform=Matrix.CreateScale(scale);
            VoidPortalRegistry.Portal current,destination;
            if(runtime.Portals==null || !runtime.Portals.TryGet(source,out current)){runtime.ClosePortalDialog(player,this);return;}
            if(previous.IsClicked)Select(-1);if(next.IsClicked)Select(1);
            string result="";
            if(rename.IsClicked)result=runtime.PortalOperations.Rename(player,source,name.Text)?"已重命名":"重命名失败：请检查名称、距离和所有权";
            if(link.IsClicked)result=selected!=Guid.Empty && runtime.PortalOperations.Link(player,source,selected)?"已设置目标":"设置失败：请检查目标、距离和所有权";
            if(unlink.IsClicked)result=runtime.PortalOperations.Link(player,source,Guid.Empty)?"已取消目标":"取消失败：请检查距离和所有权";
            if(delete.IsClicked)
            {
                if(!deleteArmed){deleteArmed=true;delete.Text="再次删除";result="再次点击确认删除";}
                else if(runtime.PortalOperations.Delete(player,source)){runtime.ClosePortalDialog(player,this);return;}
                else result="删除失败：请检查距离和所有权";
            }
            string target=runtime.Portals.TryGet(current.Target,out destination)?destination.Name:"未设置";
            string option=runtime.Portals.TryGet(selected,out destination)?destination.Name+" ("+destination.X.ToString("0")+", "+destination.Y.ToString("0")+", "+destination.Z.ToString("0")+")":"没有其他星门";
            info.Text="当前："+current.Name+"  目标："+target+"\n可选目的地："+option+"\n单人/本地主机仅传送到已加载且安全的目的地。\n"+result;
            if(close.IsClicked||Input.Back||Input.Cancel){Input.Clear();runtime.ClosePortalDialog(player,this);}
        }
        internal void Release(){runtime=null;player=null;}
    }
}
