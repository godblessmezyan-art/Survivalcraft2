using Engine;
using Engine.Graphics;
using Game;
using SCIEPort.Features.Artifacts;

namespace SCIEPort.Features.Blueprints
{
    /// <summary>One circular skill target, arranged by the shared native-controls avoidance layout.</summary>
    internal sealed class BlueprintHud
    {
        private readonly WorldEditRuntime runtime;
        private CanvasWidget root, button;
        private ClickableWidget click;
        internal BlueprintHud(WorldEditRuntime runtime) { this.runtime=runtime; }
        internal void Tick(ComponentGui gui)
        {
            bool visible=runtime.CanRequest() && gui.ControlsContainerWidget.IsVisible && !DialogsManager.HasDialogs(runtime.Player.GuiWidget);
            if(!visible) { if(root!=null)root.IsVisible=false;return; }
            if(root==null)
            {
                root=new CanvasWidget { IsHitTestVisible=false,Name="SCIEPort.TiangongSkill",HorizontalAlignment=WidgetAlignment.Near,VerticalAlignment=WidgetAlignment.Near };
                button=new CanvasWidget { Size=new Vector2(72),IsHitTestVisible=false };
                var texture=ContentManager.Get<Texture2D>("Textures/TiangongBlueprintSkill");
                button.Children.Add(new RectangleWidget { Size=new Vector2(72),Subtexture=new Subtexture(texture,Vector2.Zero,Vector2.One),FillColor=Color.White,OutlineThickness=0,IsHitTestVisible=false });
                click=new ArtifactSkillHitTarget { HorizontalAlignment=WidgetAlignment.Stretch,VerticalAlignment=WidgetAlignment.Stretch,SoundName="Audio/UI/ButtonClick" };
                button.Children.Add(click);root.Children.Add(button);gui.ControlsContainerWidget.Children.Add(root);
            }
            root.IsVisible=true;
            if(ArtifactSkillHud.Arrange(gui,root,button) && click.IsClicked) runtime.OpenManager();
        }
        internal void Dispose() { if(root!=null && root.ParentWidget!=null)root.ParentWidget.Children.Remove(root);root=null;button=null;click=null; }
    }
}
