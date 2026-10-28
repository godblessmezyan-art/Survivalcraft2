using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;
using SCIEPort.Features.Gungnir;

namespace SCIEPort.Features.Poseidon
{
    /// <summary>Project-owned touch badges. Reuses only same-project proven native-control placement.</summary>
    internal sealed class PoseidonHud
    {
        private sealed class View
        {
            internal ComponentPlayer Player;
            internal CanvasWidget Root;
            internal readonly CanvasWidget[] Badges = new CanvasWidget[2];
            internal readonly ClickableWidget[] Clicks = new ClickableWidget[2];
            internal readonly LabelWidget[] Labels = new LabelWidget[2];
        }
        private readonly SubsystemPoseidon m_runtime;
        private readonly Dictionary<ComponentGui,View> m_views = new Dictionary<ComponentGui,View>();
        internal PoseidonHud(SubsystemPoseidon runtime) { m_runtime=runtime; }
        internal void Update(ComponentGui gui)
        {
            var player=gui?.m_componentPlayer;
            if(player==null || !player.PlayerData.IsMainPlayer || gui.ControlsContainerWidget==null)return;
            bool visible=SubsystemPoseidon.Equipped(player) && gui.ControlsContainerWidget.IsVisibleGlobal;
            View view;
            if(!m_views.TryGetValue(gui,out view))
            {
                if(!visible)return;
                view=Create(gui,player); m_views.Add(gui,view);
            }
            view.Root.IsVisible=visible;
            if(!visible)return;
            var controls=gui.ControlsContainerWidget;
            Vector2 size=controls.ActualSize;
            if(size.X<300 || size.Y<240){view.Root.IsVisible=false;return;}
            view.Root.Size=size;
            var placement=GungnirHudLayout.Place(size.X,size.Y,Bounds(controls,gui.LookWidget),Bounds(controls,gui.ShortInventoryWidget),SettingsManager.LeftHandedLayout);
            // Check all concrete native controls, not full-screen input routing containers.
            bool safe=false;
            for(int attempt=0;attempt<32;attempt++)
            {
                if(!Overlap(gui,view,placement.Blessing) && !Overlap(gui,view,placement.Judgment)){safe=true;break;}
                if(Math.Min(placement.Blessing.Y,placement.Judgment.Y)>=24){placement.Blessing.Y-=16;placement.Judgment.Y-=16;}
                else {float dx=placement.Mirrored?16:-16;placement.Blessing.X=MathUtils.Clamp(placement.Blessing.X+dx,8,size.X-80);placement.Judgment.X=MathUtils.Clamp(placement.Judgment.X+dx,8,size.X-96);}
            }
            if(!safe){view.Root.IsVisible=false;return;}
            CanvasWidget.SetPosition(view.Badges[0],new Vector2(placement.Blessing.X,placement.Blessing.Y));
            CanvasWidget.SetPosition(view.Badges[1],new Vector2(placement.Judgment.X,placement.Judgment.Y));
            var state=m_runtime.Get(player);
            for(int i=0;i<2;i++)
            {
                float remaining=(float)Math.Max(0,(i==0?state.DashReady:state.WaveReady)-m_runtime.Now);
                view.Labels[i].Text=remaining>0?Math.Ceiling(remaining).ToString("0"):i==0?"潮涌":"怒涛";
                view.Clicks[i].IsEnabled=remaining<=0;
                if(view.Clicks[i].IsClicked) m_runtime.Request(player,(byte)(i+1),player.GameWidget.ActiveCamera.ViewDirection);
            }
        }
        private View Create(ComponentGui gui,ComponentPlayer player)
        {
            var v=new View {Player=player,Root=new CanvasWidget {Name="SCIEPort.Poseidon.Skills",IsHitTestVisible=false,HorizontalAlignment=WidgetAlignment.Near,VerticalAlignment=WidgetAlignment.Near}};
            var texture=ContentManager.Get<Texture2D>("Textures/PoseidonSkills");
            for(int i=0;i<2;i++)
            {
                float size=i==0?72:88;
                var badge=new CanvasWidget {Size=new Vector2(size),IsHitTestVisible=false};
                badge.Children.Add(new RectangleWidget {Size=new Vector2(size),Subtexture=new Subtexture(texture,new Vector2(i*0.5f,0),new Vector2((i+1)*0.5f,1)),FillColor=Color.White,OutlineColor=Color.Transparent,OutlineThickness=0,IsHitTestVisible=false});
                var label=new LabelWidget {Size=new Vector2(size,25),TextAnchor=TextAnchor.HorizontalCenter|TextAnchor.VerticalCenter,FontScale=0.63f,DropShadow=true,Color=new Color(207,247,255),IsHitTestVisible=false};
                badge.Children.Add(label);CanvasWidget.SetPosition(label,new Vector2(0,size-24));
                var click=new ClickableWidget {HorizontalAlignment=WidgetAlignment.Stretch,VerticalAlignment=WidgetAlignment.Stretch,SoundName="Audio/UI/ButtonClick"};
                badge.Children.Add(click);
                v.Badges[i]=badge;v.Clicks[i]=click;v.Labels[i]=label;v.Root.Children.Add(badge);
            }
            gui.ControlsContainerWidget.Children.Add(v.Root);return v;
        }
        private static GungnirHudLayout.Rect Bounds(ContainerWidget root,Widget widget)
        {
            if(widget==null || !widget.IsVisibleGlobal)return default(GungnirHudLayout.Rect);
            Vector2 a=root.ScreenToWidget(widget.GlobalBounds.Min),b=root.ScreenToWidget(widget.GlobalBounds.Max);
            return new GungnirHudLayout.Rect {X=Math.Min(a.X,b.X),Y=Math.Min(a.Y,b.Y),Width=Math.Abs(b.X-a.X),Height=Math.Abs(b.Y-a.Y)};
        }
        private static bool Overlap(ComponentGui gui,View view,GungnirHudLayout.Rect rect)
        {
            foreach(Widget widget in gui.ControlsContainerWidget.AllChildren)
            {
                if(widget==view.Root || widget.IsChildWidgetOf(view.Root) || !widget.IsVisibleGlobal || !widget.IsHitTestVisible)continue;
                if(!(widget is ButtonWidget) && !(widget is ClickableWidget) && !(widget is TouchInputWidget) && !(widget is MoveRoseWidget) && !(widget is InventorySlotWidget))continue;
                var native=Bounds(gui.ControlsContainerWidget,widget);
                if(native.IsValid && rect.Intersects(native))return true;
            }
            return false;
        }
        internal void Remove(ComponentPlayer player)
        {
            ComponentGui key=null;
            foreach(var pair in m_views)if(pair.Value.Player==player){key=pair.Key;break;}
            if(key!=null){var v=m_views[key];v.Root.ParentWidget?.Children.Remove(v.Root);m_views.Remove(key);}
        }
        internal void Dispose()
        {
            foreach(var v in m_views.Values)v.Root.ParentWidget?.Children.Remove(v.Root);
            m_views.Clear();
        }
    }
}
