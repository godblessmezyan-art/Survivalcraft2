using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;
using Rect=SCIEPort.Features.Gungnir.GungnirHudLayout.Rect;
namespace SCIEPort.Features.Excalibur
{
    internal sealed class ExcaliburSkillHitTarget:ClickableWidget
    {
        public override bool HitTest(Vector2 point){Vector2 p=ScreenToWidget(point);return (p-ActualSize*.5f).LengthSquared() <= ActualSize.X*ActualSize.X*.2304f;}
    }
    /// <summary>Three imagegen skill medallions arranged around the native Look control.</summary>
    internal sealed class ExcaliburHud
    {
        private sealed class View
        {
            internal ComponentPlayer Player;internal CanvasWidget Root;
            internal readonly CanvasWidget[] Badges=new CanvasWidget[3];
            internal readonly ExcaliburSkillHitTarget[] Clicks=new ExcaliburSkillHitTarget[3];
            internal readonly LabelWidget[] Labels=new LabelWidget[3];
            internal readonly RectangleWidget[] Icons=new RectangleWidget[3];
            internal readonly LabelWidget[] Names=new LabelWidget[3];
            internal readonly List<Rect> Obstacles=new List<Rect>(32);
        }
        private readonly SubsystemExcalibur m_runtime;
        private readonly Dictionary<ComponentGui,View> m_views=new Dictionary<ComponentGui,View>();
        internal ExcaliburHud(SubsystemExcalibur runtime){m_runtime=runtime;}
        internal void Update(ComponentGui gui)
        {
            var p=gui?.m_componentPlayer;if(p==null||!p.PlayerData.IsMainPlayer||gui.ControlsContainerWidget==null)return;
            bool visible=SubsystemExcalibur.Equipped(p)&&gui.ControlsContainerWidget.IsVisibleGlobal&&p.ComponentHealth.Health>0&&p.ComponentSleep.SleepFactor<=0
                &&gui.ModalPanelWidget==null&&!DialogsManager.HasDialogs(p.GuiWidget)&&p.GameWidget!=null&&p.GameWidget.ActiveCamera.IsEntityControlEnabled;
            View v;if(!m_views.TryGetValue(gui,out v)){if(!visible)return;v=Create(gui,p);m_views.Add(gui,v);}
            v.Root.IsVisible=visible;if(!visible)return;
            if(!Artifacts.ArtifactSkillHud.Arrange(gui,v.Root,v.Badges))return;
            var state=m_runtime.Get(p);
            for(int i=0;i<3;i++)
            {
                float remaining=(float)Math.Max(0,state.Ready[i]-m_runtime.Now);
                bool enchant=i==0&&m_runtime.Enchanted(p),sanctuary=i==2&&state.Active?.Skill==3&&m_runtime.Fresh(state);
                bool active=enchant||sanctuary;
                v.Labels[i].Text=enchant?"附魔 "+Math.Ceiling(state.BlessingEnd-m_runtime.Now).ToString("0"):sanctuary?"理想乡":remaining>0?Math.Ceiling(remaining).ToString("0"):string.Empty;
                v.Icons[i].FillColor=remaining>0&&!active?new Color(120,144,168,220):Color.White;

                v.Clicks[i].IsEnabled=remaining<=0&&!m_runtime.Busy(p)&&p.PlayerData.IsReadyForPlaying;
                if(v.Clicks[i].IsClicked)m_runtime.Request(p,(byte)(i+1),p.GameWidget.ActiveCamera.ViewDirection);
            }
        }
        private View Create(ComponentGui gui,ComponentPlayer p)
        {
            var v=new View{Player=p,Root=new CanvasWidget{Name="SCIEPort.Excalibur.LookSkills",IsHitTestVisible=false,HorizontalAlignment=WidgetAlignment.Near,VerticalAlignment=WidgetAlignment.Near}};
            string[] assets={"ExcaliburLake","ExcaliburRelease","ExcaliburAvalon"},names={"湖光附魔","誓约胜利","遥远理想乡"};
            for(int i=0;i<3;i++)
            {
                float size=i==2?88:72;var badge=new CanvasWidget{Size=new Vector2(size),IsHitTestVisible=false};
                var icon=new RectangleWidget{Size=new Vector2(size),Subtexture=new Subtexture(ContentManager.Get<Texture2D>("Textures/"+assets[i]),Vector2.Zero,Vector2.One),FillColor=Color.White,OutlineThickness=0,OutlineColor=Color.Transparent,IsHitTestVisible=false};
                var label=new LabelWidget{Size=new Vector2(size),TextAnchor=TextAnchor.HorizontalCenter|TextAnchor.VerticalCenter,FontScale=i==2?.65f:.62f,DropShadow=true,Color=Color.White,IsHitTestVisible=false};
                var name=new LabelWidget{Size=new Vector2(size,16),Text=names[i],TextAnchor=TextAnchor.HorizontalCenter|TextAnchor.VerticalCenter,FontScale=.35f,DropShadow=true,Color=new Color(255,239,190),IsHitTestVisible=false};
                var click=new ExcaliburSkillHitTarget{HorizontalAlignment=WidgetAlignment.Stretch,VerticalAlignment=WidgetAlignment.Stretch,SoundName="Audio/UI/ButtonClick"};
                badge.Children.Add(icon);badge.Children.Add(label);badge.Children.Add(name);CanvasWidget.SetPosition(name,new Vector2(0,size*.75f));badge.Children.Add(click);
                v.Badges[i]=badge;v.Clicks[i]=click;v.Labels[i]=label;v.Icons[i]=icon;v.Names[i]=name;v.Root.Children.Add(badge);
            }
            gui.ControlsContainerWidget.Children.Add(v.Root);return v;
        }
        private static Rect Bounds(ContainerWidget root,Widget widget)
        {
            if(widget==null||!widget.IsVisibleGlobal)return default(Rect);var a=root.ScreenToWidget(widget.GlobalBounds.Min);var b=root.ScreenToWidget(widget.GlobalBounds.Max);
            return new Rect{X=Math.Min(a.X,b.X),Y=Math.Min(a.Y,b.Y),Width=Math.Abs(a.X-b.X),Height=Math.Abs(a.Y-b.Y)};
        }
        internal void Remove(ComponentPlayer p)
        {
            ComponentGui key=null;foreach(var pair in m_views)if(pair.Value.Player==p){key=pair.Key;break;}
            if(key!=null){var v=m_views[key];v.Root.ParentWidget?.Children.Remove(v.Root);m_views.Remove(key);}
        }
        internal void Dispose(){foreach(var v in m_views.Values)v.Root.ParentWidget?.Children.Remove(v.Root);m_views.Clear();}
    }
}
