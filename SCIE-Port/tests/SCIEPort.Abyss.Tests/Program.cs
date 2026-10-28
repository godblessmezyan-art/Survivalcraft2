using System;using SCIEPort.Features.AbyssReaper;
class Program {static int checks;static void Check(bool b){checks++;if(!b)throw new Exception("Abyss assertion "+checks);}static void Main(){
var s=new AbyssCombo();double t=0;for(int i=0;i<3;i++){s.Input(true,t);s.Input(false,t+.03);Check(s.Stage==i);Check(!s.Impact(t+.04));Check(s.Impact(t+.4));Check(!s.Impact(t+.45));t+=.65;}
s.Input(true,5);s.Input(false,5.01);Check(s.Stage==0);
s.Input(true,7);Check(s.Charge(7.7)==1);s.Input(false,7.8);Check(s.Stage==3);Check(s.Impact(9));Check(!s.Impact(10));
Check(!AbyssCombo.Sector(4,1,0,0));Check(!AbyssCombo.Sector(2,-1,0,0));Check(!AbyssCombo.Sector(2,1,2,0));Check(AbyssCombo.Sector(2,1,0,0));Check(!AbyssCombo.Sector(2,.5f,0,2));Console.WriteLine("Abyss combat: "+checks+" assertions passed.");}}
