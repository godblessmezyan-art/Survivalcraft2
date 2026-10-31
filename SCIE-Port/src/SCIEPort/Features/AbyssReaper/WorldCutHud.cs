using System;
using Engine;
using Engine.Graphics;
using Engine.Input;
using Game;
namespace SCIEPort.Features.AbyssReaper
{
    // Leave transparent sprite corners available to world input.
    internal sealed class AbyssSkillHitTarget : ClickableWidget
    {
        public override bool HitTest(Vector2 point)
        {
            Vector2 p=ScreenToWidget(point);
            return WorldCutHudLayout.Circle(p.X,p.Y,ActualSize.X,ActualSize.Y);
        }
    }
    internal sealed class WorldCutHud : IDisposable
    {
        private sealed class Button
        {
            internal CanvasWidget Root;
            internal RectangleWidget Icon,Glow;
            internal LabelWidget Cooldown,Name;
            internal AbyssSkillHitTarget Hit;
        }
        private readonly ComponentGui gui;
        private readonly CanvasWidget panel;
        private readonly Button[] buttons=new Button[5];
        private readonly CanvasWidget[] roots=new CanvasWidget[5];
        private readonly LabelWidget status;
        private readonly Subtexture[] voidIcons=new Subtexture[5],deathIcons=new Subtexture[5];
        private int capturedTouch=-1,capturedSlot;
        internal bool Visible => panel.IsVisible;
        internal WorldCutHud(ComponentGui owner)
        {
            gui=owner;
            panel=new CanvasWidget { Name="SCIEPort.Abyss.LookSkills",HorizontalAlignment=WidgetAlignment.Near,VerticalAlignment=WidgetAlignment.Near,IsHitTestVisible=false,IsVisible=false };
            Texture2D atlas=ContentManager.Get<Texture2D>("Textures/AbyssSkillsV1");
            string[] names={"断界斩","无界折跃","万象破碎","虚夜湮灭","裂界星门"};
            string[] icons={"Textures/AbyssWorldCutV2","Textures/AbyssBlinkV2","Textures/AbyssShatterV2"};
            for(int i=0;i<5;i++)
            {
                float size=i==3?88:i==4?64:72;
                var button=new Button();buttons[i]=button;
                button.Root=new CanvasWidget { Size=new Vector2(size),IsHitTestVisible=false };
                Vector2 uv=i==3?new Vector2(.5f):Vector2.Zero;
                Subtexture texture=i<3?new Subtexture(ContentManager.Get<Texture2D>(icons[i]),Vector2.Zero,Vector2.One):new Subtexture(atlas,uv,uv+new Vector2(.5f));
                voidIcons[i]=texture;
                button.Icon=new RectangleWidget { Size=new Vector2(size),Subtexture=texture,FillColor=Color.White,OutlineThickness=0,OutlineColor=Color.Transparent,IsHitTestVisible=false };
                button.Glow=new RectangleWidget { Size=new Vector2(size),Subtexture=button.Icon.Subtexture,FillColor=Color.Transparent,OutlineThickness=0,OutlineColor=Color.Transparent,IsHitTestVisible=false };
                button.Cooldown=new LabelWidget { Size=new Vector2(size),TextAnchor=TextAnchor.HorizontalCenter|TextAnchor.VerticalCenter,FontScale=i==3?.92f:.8f,DropShadow=true,Color=Color.White,IsHitTestVisible=false };
                var label=new LabelWidget { Size=new Vector2(size,16),Text=names[i],TextAnchor=TextAnchor.HorizontalCenter|TextAnchor.VerticalCenter,FontScale=.38f,DropShadow=true,Color=new Color(237,218,255),IsHitTestVisible=false };
                button.Name=label;
                button.Hit=new AbyssSkillHitTarget { HorizontalAlignment=WidgetAlignment.Stretch,VerticalAlignment=WidgetAlignment.Stretch,IsHitTestVisible=true };
                button.Root.Children.Add(button.Icon);button.Root.Children.Add(button.Glow);button.Root.Children.Add(button.Cooldown);button.Root.Children.Add(label);button.Root.Children.Add(button.Hit);
                CanvasWidget.SetPosition(label,new Vector2(0,size*.71f));
                roots[i]=button.Root;panel.Children.Add(button.Root);
            }
            string[] deathAssets={"DeathSwitch","DeathEmpower","DeathWave",null,"DeathGuardian"};
            for(int i=0;i<5;i++)deathIcons[i]=deathAssets[i]==null?voidIcons[i]:new Subtexture(ContentManager.Get<Texture2D>("Textures/Abyss"+deathAssets[i]),Vector2.Zero,Vector2.One);
            voidIcons[0]=deathIcons[0];
            status=new LabelWidget { Size=new Vector2(232,24),FontScale=.42f,DropShadow=true,Color=new Color(221,190,244),IsHitTestVisible=false };
            panel.Children.Add(status);gui.ControlsContainerWidget.Children.Add(panel);
        }
        internal int Poll(WidgetInput input)
        {
            if(!panel.IsVisibleGlobal||input==null){CancelInput();return 0;}
            bool seen=false;
            foreach(var touch in input.TouchLocations)
            {
                if(capturedTouch<0&&touch.State==TouchLocationState.Pressed)
                { int slot=At(touch.Position);if(slot!=0){capturedTouch=touch.Id;capturedSlot=slot;} }
                if(touch.Id!=capturedTouch)continue;seen=true;
                if(touch.State==TouchLocationState.Released)
                { int slot=capturedSlot;CancelInput();return At(touch.Position)==slot?slot:0; }
            }
            if(capturedTouch>=0&&!seen)CancelInput();
            if(!input.Click.HasValue)return 0;
            var click=input.Click.Value;int picked=At(click.Start);return picked!=0&&At(click.End)==picked?picked:0;
        }
        internal void CancelInput(){capturedTouch=-1;capturedSlot=0;}
        private int At(Vector2 screen)
        {
            for(int i=0;i<5;i++)if(buttons[i].Hit.IsEnabledGlobal&&buttons[i].Hit.HitTest(screen))return i+1;
            return 0;
        }
        internal void Update(double remaining,double blinkRemaining,double shatterRemaining,double horizonRemaining,bool busy,bool pending,string feedback,bool blocked,DeathAuthorityRuntime death=null,ComponentPlayer actor=null)
        {
            if(blocked){panel.IsVisible=false;CancelInput();return;}
            if(!Artifacts.ArtifactSkillHud.ArrangeAbyss(gui,panel,status,roots)){CancelInput();return;}
            for(int i=0;i<5;i++)
            {
                bool form=death!=null&&death.IsDeath(actor);
                buttons[i].Icon.Subtexture=buttons[i].Glow.Subtexture=(form?deathIcons:voidIcons)[i];
                buttons[i].Name.Text=i==0?(form?"切换虚空":"切换死亡"):i==1?(form?"夺魂赋刃":"无界折跃"):i==2?(form?"死亡蔓延":"万象破碎"):i==3?"虚夜湮灭":form?"冥界军势":"裂界星门";
                double cooldown=i==0?remaining:i==1?blinkRemaining:i==2?shatterRemaining:i==3?horizonRemaining:0;
                if(death!=null){if(i==0)cooldown=death.Remaining(actor,1);else if(form&&i!=3)cooldown=death.Remaining(actor,(byte)(i==1?2:i==2?3:4));}
                bool ready=cooldown<=0;
                buttons[i].Cooldown.Text=i==0&&pending?"…":ready?string.Empty:Math.Ceiling(cooldown).ToString("0");
                if(i==1&&form&&death.Empowered(actor))buttons[i].Cooldown.Text="赋能\n"+Math.Ceiling(death.EmpowerRemaining(actor)).ToString("0");
                buttons[i].Icon.FillColor=ready&&!busy?Color.White:new Color(120,103,142,225);
                float pulse=.5f+.5f*MathUtils.Sin((float)Time.RealTime*2.2f);
                buttons[i].Glow.FillColor=ready&&!busy?new Color(177,112,240)*(buttons[i].Hit.IsPressed?.28f:.035f+pulse*.055f):Color.Transparent;
            }
            status.Text=feedback??"轻触连斩 · 长按蓄力";
        }
        public void Dispose(){CancelInput();if(panel.ParentWidget!=null)panel.ParentWidget.Children.Remove(panel);}
    }
}
