using System;
using System.Collections.Generic;
using Engine;
using Game;
namespace SCIEPort.Features.Artifacts
{
    internal sealed class ArtifactSkillHitTarget:ClickableWidget
    {
        public override bool HitTest(Vector2 point)
        {
            Vector2 p=ScreenToWidget(point);float radius=Math.Min(ActualSize.X,ActualSize.Y)*.48f;
            return radius>0 && (p-ActualSize*.5f).LengthSquared()<=radius*radius;
        }
    }
    internal static class ArtifactSkillHud
    {
        private static ArtifactSkillLayout.Rect Bounds(ContainerWidget root,Widget widget)
        {
            if(widget==null||!widget.IsVisibleGlobal)return default(ArtifactSkillLayout.Rect);
            Vector2 a=root.ScreenToWidget(widget.GlobalBounds.Min),b=root.ScreenToWidget(widget.GlobalBounds.Max);
            return new ArtifactSkillLayout.Rect{X=Math.Min(a.X,b.X),Y=Math.Min(a.Y,b.Y),W=Math.Abs(a.X-b.X),H=Math.Abs(a.Y-b.Y)};
        }
        internal static bool Arrange(ComponentGui gui,CanvasWidget root,params CanvasWidget[] buttons)
        {
            return ArrangeCore(gui,root,null,Vector2.Zero,buttons);
        }
        internal static bool ArrangeWithStatus(ComponentGui gui,CanvasWidget root,LabelWidget status,params CanvasWidget[] buttons)
        {
            return ArrangeCore(gui,root,status,status.Size,buttons);
        }
        internal static bool ArrangeWithStatus(ComponentGui gui,CanvasWidget root,CanvasWidget status,params CanvasWidget[] buttons)
        {
            return ArrangeCore(gui,root,status,status.Size,buttons);
        }
        private static bool ArrangeCore(ComponentGui gui,CanvasWidget root,Widget status,Vector2 statusSize,CanvasWidget[] buttons)
        {
            var controls=gui.ControlsContainerWidget;root.Size=controls.ActualSize;
            var obstacles=new List<ArtifactSkillLayout.Rect>();
            foreach(Widget widget in controls.AllChildren)
            {
                if(widget==root||widget.IsChildWidgetOf(root)||!widget.IsVisibleGlobal||!widget.IsHitTestVisible)continue;
                if(!(widget is ButtonWidget)&&!(widget is ClickableWidget)&&!(widget is TouchInputWidget)&&!(widget is MoveRoseWidget)&&!(widget is InventorySlotWidget))continue;
                if(obstacles.Count>=254){root.IsVisible=false;return false;}
                obstacles.Add(Bounds(controls,widget));
            }
            obstacles.Add(Bounds(controls,gui.ShortInventoryWidget));obstacles.Add(Bounds(controls,gui.MoveWidget));
            var sizes=new float[buttons.Length];for(int i=0;i<buttons.Length;i++)sizes[i]=buttons[i].Size.X;
            ArtifactSkillLayout.Rect[] places;
            bool safe=ArtifactSkillLayout.Place(root.Size.X,root.Size.Y,Bounds(controls,gui.LookWidget),obstacles,sizes,SettingsManager.LeftHandedLayout,out places);
            root.IsVisible=safe;if(!safe)return false;
            for(int i=0;i<buttons.Length;i++)CanvasWidget.SetPosition(buttons[i],new Vector2(places[i].X,places[i].Y));
            if(status!=null)
            {
                float left=places[0].X,top=places[0].Y;
                foreach(var place in places){left=Math.Min(left,place.X);top=Math.Min(top,place.Y);}
                var hint=new ArtifactSkillLayout.Rect{X=Math.Max(8,Math.Min(root.Size.X-statusSize.X-8,left)),Y=top-statusSize.Y-4,W=statusSize.X,H=statusSize.Y};
                status.IsVisible=hint.Y>=8&&!hint.Overlaps(Bounds(controls,gui.LookWidget));
                foreach(var obstacle in obstacles)if(hint.Overlaps(obstacle))status.IsVisible=false;
                CanvasWidget.SetPosition(status,new Vector2(hint.X,hint.Y));
            }
            return true;
        }
    }
}
