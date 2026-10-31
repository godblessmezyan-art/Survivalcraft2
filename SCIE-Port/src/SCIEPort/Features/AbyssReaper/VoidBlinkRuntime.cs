using System;
using Game;
namespace SCIEPort.Features.AbyssReaper
{
    // Preserve the subsystem boundary; V2 blink/shatter share authority and lifecycle.
    internal sealed class VoidBlinkRuntime:IDisposable
    {
        internal readonly VoidSpaceRuntime Space;
        internal VoidBlinkRuntime(SubsystemTerrain terrain,SubsystemBodies bodies,SubsystemTime clock,SubsystemAudio audio,bool players,bool passive,int quality)
        {Space=new VoidSpaceRuntime(terrain,bodies,clock,audio,quality);}
        internal double Remaining(ComponentPlayer p) => Space.Remaining(p,VoidSpaceRules.Blink);
        internal bool Busy(ComponentPlayer p) => Space.Busy(p);
        internal bool TryCast(ComponentPlayer p,out string reason) => Space.Request(p,VoidSpaceRules.Blink,out reason);
        internal void Cancel(ComponentPlayer p) => Space.Cancel(p);
        internal void Update() => Space.Update();
        internal void Draw(Camera camera) => Space.Draw(camera);
        public void Dispose() => Space.Dispose();
    }
}
