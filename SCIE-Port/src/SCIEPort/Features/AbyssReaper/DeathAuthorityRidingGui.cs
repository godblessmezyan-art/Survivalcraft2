using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;
namespace SCIEPort.Features.AbyssReaper
{
    internal sealed partial class DeathAuthorityRuntime
    {
        private readonly Dictionary<ComponentPlayer,DeathRidingHud> ridingHuds=new Dictionary<ComponentPlayer,DeathRidingHud>();
        private readonly Dictionary<ComponentPlayer,CerberusIntroHud> introHuds=new Dictionary<ComponentPlayer,CerberusIntroHud>();
        private readonly Dictionary<Guid,double> cerberusIntros=new Dictionary<Guid,double>();
        private readonly HashSet<ComponentModel> posedModels=new HashSet<ComponentModel>();private int poseFrame=-1;
        internal void UpdateRidingGui(ComponentGui gui)
        {
            var p=gui?.m_componentPlayer;if(p==null||!p.PlayerData.IsMainPlayer||p.GameWidget==null||!p.IsAddedToProject)return;
            UpdateCerberusIntroGui(gui,p);
            DeathRidingHud hud;var row=RiddenFriend(p);
            if(row==null){if(ridingHuds.TryGetValue(p,out hud)){hud.Dispose();ridingHuds.Remove(p);}return;}
            if(!ridingHuds.TryGetValue(p,out hud)){if(ridingHuds.Count>=4)return;hud=new DeathRidingHud(gui);ridingHuds.Add(p,hud);}
            hud.Update(this,p,row,(Get(p)?.PendingUntil??0)>Now);
        }
        /// <summary>Owner-only awakening veil and skip button. No tutorial text.</summary>
        private void UpdateCerberusIntroGui(ComponentGui gui,ComponentPlayer player)
        {
            double until;bool active=cerberusIntros.TryGetValue(player.PlayerGuid,out until)&&Now<until;
            CerberusIntroHud hud;
            if(!active){if(introHuds.TryGetValue(player,out hud)){hud.Dispose();introHuds.Remove(player);}return;}
            if(!introHuds.TryGetValue(player,out hud)){if(introHuds.Count>=4)return;hud=new CerberusIntroHud(gui);introHuds.Add(player,hud);}
            hud.Update(gui,this,player);
        }
        private void ClearRidingHud(){foreach(var hud in ridingHuds.Values)hud.Dispose();ridingHuds.Clear();ClearCerberusIntroHud();}
        private void ClearCerberusIntroHud()
        {
            foreach(var hud in introHuds.Values)hud.Dispose();
            introHuds.Clear();cerberusIntros.Clear();
        }
        internal bool RidingUiInput(ComponentPlayer player)
        {
            DeathRidingHud hud;
            if(ridingHuds.TryGetValue(player,out hud)&&hud.Captures(player.GuiWidget.Input))return true;
            CerberusIntroHud intro;
            return introHuds.TryGetValue(player,out intro)&&intro.Captures(player.GuiWidget.Input);
        }
        internal void RidingPose(ComponentModel model,Camera camera)
        {
            var rider=model.Entity.FindComponent<ComponentRider>(false);var mount=rider?.Mount;
            if(mount==null||model.Model==null||mount.Entity.FindComponent<ComponentCerberusModel>(false)==null&&mount.Entity.FindComponent<ComponentBoneDragonModel>(false)==null)return;
            if(poseFrame!=Time.FrameIndex){poseFrame=Time.FrameIndex;posedModels.Clear();}
            if(posedModels.Add(model))
            {
                // Widen the native mounted pose over the broad original torso; keep
                // native arms/body and apply the same legs to clothing model copies.
                for(int i=0;i<2;i++)
                {
                    var bone=model.Model.FindBone(i==0?"Leg1":"Leg2",false);if(bone==null)continue;
                    float side=bone.Transform.Translation.X<0?-1:1;
                    model.SetBoneTransform(bone.Index,Matrix.CreateRotationZ(side*.85f)*(model.GetBoneTransform(bone.Index)??Matrix.Identity));
                }
            }
            model.CalculateAbsoluteBonesTransforms(camera);
        }
        private sealed class CerberusIntroHud:IDisposable
        {
            private readonly CanvasWidget root=new CanvasWidget{IsHitTestVisible=false};
            private readonly RectangleWidget veil=new RectangleWidget{FillColor=new Color(3,1,7,168),OutlineColor=Color.Transparent,OutlineThickness=0,IsHitTestVisible=false};
            private readonly CanvasWidget slot=new CanvasWidget{Size=new Vector2(118),IsHitTestVisible=false};
            private readonly BevelledButtonWidget skip=new BevelledButtonWidget{Text="跳过",Size=new Vector2(118,64),FontScale=.5f};
            private readonly double m_started=Time.FrameStartTime;
            internal CerberusIntroHud(ComponentGui gui)
            {
                root.Children.Add(veil);root.Children.Add(slot);slot.Children.Add(skip);
                CanvasWidget.SetPosition(skip,new Vector2(0,26));
                gui.ControlsContainerWidget.Children.Add(root);
            }
            internal void Update(ComponentGui gui,DeathAuthorityRuntime runtime,ComponentPlayer player)
            {
                bool visible=gui.ModalPanelWidget==null&&!DialogsManager.HasDialogs(player.GuiWidget)&&player.GameWidget.ActiveCamera.IsEntityControlEnabled;
                root.IsVisible=visible;
                if(!visible)return;
                // Darkness gathers, breaks with the triple roar, then clears for the fight.
                float t=MathUtils.Clamp((float)((Time.FrameStartTime-m_started)/CerberusRules.IntroDuration),0f,1f);
                int alpha=t<.35f?(int)(168f*t/.35f):t<.8f?(int)(168f-(t-.35f)/.45f*90f):(int)(78f*(1f-(t-.8f)/.2f));
                veil.FillColor=new Color(3,1,7,MathUtils.Clamp(alpha,0,168));
                veil.Size=gui.ControlsContainerWidget.ActualSize;CanvasWidget.SetPosition(veil,Vector2.Zero);
                if(!Artifacts.ArtifactSkillHud.Arrange(gui,root,slot))return;
                if(skip.IsClicked)runtime.Request(player,DeathAuthorityRules.SkipIntro,Vector3.UnitZ);
            }
            internal bool Captures(WidgetInput input)
            {
                if(!root.IsVisibleGlobal||input==null)return false;
                foreach(var touch in input.TouchLocations)if(Inside(touch.Position))return true;
                return input.Click.HasValue&&(Inside(input.Click.Value.Start)||Inside(input.Click.Value.End));
            }
            private bool Inside(Vector2 screen)
            {
                if(!skip.IsVisibleGlobal)return false;Vector2 at=skip.ScreenToWidget(screen);
                return at.X>=0&&at.Y>=0&&at.X<skip.ActualSize.X&&at.Y<skip.ActualSize.Y;
            }
            public void Dispose(){if(root.ParentWidget!=null)root.ParentWidget.Children.Remove(root);}
        }
    }
}
