using System;
using System.Collections.Generic;
using SCIEPort.Features.Artifacts;

namespace SCIEPort.Features.Blueprints
{
    /// <summary>Finds space for touch preview controls without covering native controls.</summary>
    internal static class BlueprintPreviewLayout
    {
        internal static bool Place(float width,float height,IList<ArtifactSkillLayout.Rect> obstacles,out ArtifactSkillLayout.Rect result)
        {
            result=default(ArtifactSkillLayout.Rect);
            foreach(float panelWidth in new[]{360f,312f})
            {
                if(width<panelWidth+16 || height<206)continue;
                foreach(float x in new[]{(width-panelWidth)*.5f,8f,width-panelWidth-8})
                for(float y=8;y<=height-198;y+=8)
                {
                    var candidate=new ArtifactSkillLayout.Rect{X=x,Y=y,W=panelWidth,H=190};bool valid=true;
                    foreach(var obstacle in obstacles)if(candidate.Overlaps(obstacle)){valid=false;break;}
                    if(valid){result=candidate;return true;}
                }
            }
            return false;
        }
    }
}
