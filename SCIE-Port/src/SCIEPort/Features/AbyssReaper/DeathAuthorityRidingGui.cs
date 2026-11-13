using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;
namespace SCIEPort.Features.AbyssReaper
{
    internal sealed partial class DeathAuthorityRuntime
    {
        private readonly Dictionary<ComponentPlayer,DeathRidingHud> ridingHuds=new Dictionary<ComponentPlayer,DeathRidingHud>();
        private readonly HashSet<ComponentModel> posedModels=new HashSet<ComponentModel>();private int poseFrame=-1;
        internal void UpdateRidingGui(ComponentGui gui)
        {
            var p=gui?.m_componentPlayer;if(p==null||!p.PlayerData.IsMainPlayer||p.GameWidget==null||!p.IsAddedToProject)return;
            DeathRidingHud hud;var row=RiddenFriend(p);
            if(row==null){if(ridingHuds.TryGetValue(p,out hud)){hud.Dispose();ridingHuds.Remove(p);}return;}
            if(!ridingHuds.TryGetValue(p,out hud)){if(ridingHuds.Count>=4)return;hud=new DeathRidingHud(gui);ridingHuds.Add(p,hud);}
            hud.Update(this,p,row,(Get(p)?.PendingUntil??0)>Now);
        }
        internal bool RidingUiInput(ComponentPlayer player){DeathRidingHud hud;return ridingHuds.TryGetValue(player,out hud)&&hud.Captures(player.GuiWidget.Input);}
        private void ClearRidingHud(){foreach(var hud in ridingHuds.Values)hud.Dispose();ridingHuds.Clear();}
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
    }
}
