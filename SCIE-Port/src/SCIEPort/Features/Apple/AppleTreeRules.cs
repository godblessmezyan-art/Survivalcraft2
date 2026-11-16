using System;
using System.Collections.Generic;

namespace SCIEPort.Features.Apple
{
    /// <summary>Persistent apple-tree parts and bounded growth/fruit rules, independent of the game.</summary>
    public static class AppleTreeRules
    {
        /// <summary>Tree base part, retaining the old low growth and fruit bits.</summary>
        public const int Root = 16;
        /// <summary>Upper trunk part.</summary>
        public const int Trunk = 32;
        /// <summary>Canopy part.</summary>
        public const int Leaf = 48;
        /// <summary>Harvested canopy remains empty until the following spring.</summary>
        public const int Harvested = 8192;
        /// <summary>Maximum cells in a tree transaction, including soil.</summary>
        public const int MaxChanges = 24;
        /// <summary>Forward plus reserved rollback writes between game updates.</summary>
        public const int FrameWrites = 64;
        /// <summary>Reads the structural role; zero is the legacy sapling.</summary>
        public static int Part(int data) => data & 48;
        /// <summary>Reads a part's horizontal offset from its own root.</summary>
        public static int OffsetX(int data) => ((data >> 6) & 3) - 1;
        /// <summary>Reads a part's horizontal offset from its own root.</summary>
        public static int OffsetZ(int data) => ((data >> 8) & 3) - 1;
        /// <summary>Reads a part's height above its own root.</summary>
        public static int OffsetY(int data) => (data >> 10) & 7;
        /// <summary>Encodes a validated part using only DATA, preserving existing block identities.</summary>
        public static int Encode(int part,int x,int y,int z)
        {
            if((part!=Trunk&&part!=Leaf)||x< -1||x>1||z< -1||z>1||y<1||y>4) throw new ArgumentOutOfRangeException();
            return part|((x+1)<<6)|((z+1)<<8)|(y<<10)|(part==Trunk?7:0);
        }
        /// <summary>Whether an occupied part coordinate leaves space for support/light sampling without overflow.</summary>
        public static bool InBounds(int x,int y,int z) => x>int.MinValue&&x<int.MaxValue&&z>int.MinValue&&z<int.MaxValue&&y>=1&&y<=254;
        /// <summary>Whether every tree part fits inside the supported coordinate range.</summary>
        public static bool CanFitTree(int x,int y,int z) => InBounds(x,y,z)&&x>int.MinValue+1&&x<int.MaxValue-1&&z>int.MinValue+1&&z<int.MaxValue-1&&y<=250;
        /// <summary>Tree growth pauses during frost and winter.</summary>
        public static bool CanGrow(int light,int temperature,bool winter) => light>=AppleRules.MinLightToGrow&&temperature>2&&!winter;
        /// <summary>Season codes: winter 0, spring 1, summer 2, autumn 3; -1 is a seasonless world.</summary>
        public static int NextFruit(int data,int season,bool suitable,bool step)
        {
            if(season==0) return AppleRules.SetFruiting(AppleRules.SetSize(data,0),false);
            if(season==1) return (data&~(Harvested|15))|(suitable?1:0);
            if(season<0&&suitable&&step) data&=~Harvested;
            if((data&Harvested)!=0||!suitable||!step) return data;
            int stage=AppleRules.GetSize(data);
            if(season==2) return AppleRules.SetFruiting(AppleRules.SetSize(data,Math.Min(6,Math.Max(2,stage+1))),false);
            if(season==3||season<0)
            {
                int next=Math.Min(7,stage+1);
                return AppleRules.SetFruiting(AppleRules.SetSize(data,next),next==7);
            }
            return data;
        }
        /// <summary>Offsets of a 3 by 3, five-cell-high tree; every part stays inside its own cell.</summary>
        public static IEnumerable<int[]> Parts()
        {
            yield return new[]{0,1,0,Trunk};
            yield return new[]{0,2,0,Trunk};
            for(int y=2;y<=4;y++) for(int x=-1;x<=1;x++) for(int z=-1;z<=1;z++)
            {
                int radius=Math.Abs(x)+Math.Abs(z);
                if(y==2&&radius!=1||y==4&&radius>1) continue;
                yield return new[]{x,y,z,Leaf};
            }
        }
    }

    /// <summary>Tree commit with a snapshot captured before writes, conditional reverse undo and a fixed size limit.</summary>
    public static class AppleTreeTransaction
    {
        /// <summary>Applies a prevalidated plan; never undoes an intervening foreign replacement.</summary>
        public static bool Apply(int[] before,int[] after,Func<int,int> read,Action<int,int> write,out bool undoFailed,out Exception error)
        {
            undoFailed=false;error=null;
            if(before.Length!=after.Length||before.Length==0||before.Length>AppleTreeRules.MaxChanges) return false;
            int attempted=-1;
            try
            {
                for(int i=0;i<before.Length;i++) if(read(i)!=before[i]) return false;
                for(int i=0;i<before.Length;i++)
                {
                    if(read(i)!=before[i]) throw new InvalidOperationException("Apple growth conflict");
                    attempted=i;write(i,after[i]);
                    if(read(i)!=after[i]) throw new InvalidOperationException("Apple growth rejected");
                }
                return true;
            }
            catch(Exception failure)
            {
                error=failure;
                for(int i=attempted;i>=0;i--)
                {
                    try
                    {
                        int actual=read(i);
                        if(actual==before[i]) continue;
                        if(actual!=after[i]) {undoFailed=true;continue;}
                        write(i,before[i]);
                        if(read(i)!=before[i]) undoFailed=true;
                    }
                    catch(Exception undo){undoFailed=true;error=new AggregateException(error,undo);}
                }
                return false;
            }
        }
    }
}
