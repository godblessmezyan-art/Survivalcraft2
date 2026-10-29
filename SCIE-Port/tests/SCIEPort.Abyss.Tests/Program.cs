using System;using SCIEPort.Features.AbyssReaper;
class Program {static int checks;static void Check(bool b){checks++;if(!b)throw new Exception("Abyss assertion "+checks);}static void Main(string[] args){
if(args.Length==2&&args[0]=="--preview"){AbyssFeelTests.WritePreview(args[1]);return;}
var s=new AbyssCombo();double t=0;for(int i=0;i<3;i++){s.Input(true,t);s.Input(false,t+.03);Check(s.Stage==i);Check(!s.Impact(t+.04));Check(s.Impact(t+.4));Check(!s.Impact(t+.45));t+=.65;}
s.Input(true,5);s.Input(false,5.01);Check(s.Stage==0);
s.Input(true,7);Check(s.Charge(7.7)==1);s.Input(false,7.8);Check(s.Stage==3);Check(s.Impact(9));Check(!s.Impact(10));
Check(!AbyssCombo.Sector(4,1,0,0));Check(!AbyssCombo.Sector(2,-1,0,0));Check(!AbyssCombo.Sector(2,1,2,0));Check(AbyssCombo.Sector(2,1,0,0));Check(!AbyssCombo.Sector(2,.5f,0,2));// Native touch emits its first Hold only after 0.2 seconds. Count Duration from finger-down.
var touch=new AbyssCombo();touch.Input(false,.1);touch.Input(true,.25,.25f);Check(touch.Charge(.25)>.38f);
touch.Input(true,.50,.50f);Check(touch.Charge(.50)>.76f);touch.Input(false,.66);Check(touch.Stage==3);Check(touch.Duration==.85);
Check(!touch.Impact(.70));Check(touch.Impact(1.10));Check(!touch.Impact(1.20));
// Moving the finger keeps a continuous hold even when the next frame has no Duration hint.
var drag=new AbyssCombo();drag.Input(true,2.25,.25f);drag.Input(true,2.5);drag.Input(true,2.7);Check(drag.Charge(2.7)==1);drag.Input(false,2.71);Check(drag.Stage==3);
// A terminal touch Tap produces a normal attack; no heavy attack from a short hold.
var tap=new AbyssCombo();tap.Input(true,3);tap.Input(false,3.01);Check(tap.Stage==0);Check(tap.Duration==.35);
// A release during the previous swing does not queue another impact.
tap.Input(true,3.03);tap.Input(false,3.08);Check(tap.Stage==0);Check(tap.Impact(3.3));Check(!tap.Impact(4));
// Clamp a delayed input hint to game-time origin instead of invalidating a negative press time.
var delayed=new AbyssCombo();delayed.Input(true,.3,.5f);Check(delayed.Charge(.4)>0);delayed.Input(false,.7);Check(delayed.Stage==3);
WorldCutTests.Run();
PortalTests.Run();
PortalTravelTests.Run();
VoidSkillTests.Run();
Console.WriteLine("Abyss combat: "+checks+" assertions passed.");}}
