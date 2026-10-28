using System;

namespace SCIEPort.Features.Inventory
{
    /// <summary>Namespaced creative tabs and data-aware equipment skins. No game state or IDs are changed.</summary>
    internal static class PortInventoryRules
    {
        internal const string Artifacts = "SCIEPort.Artifacts", Vehicles = "SCIEPort.Vehicles", Saints = "SCIEPort.Saints";
        internal static readonly string[] Categories = { Artifacts, Vehicles, Saints, "SCIEPort.Plants", "SCIEPort.Food", "SCIEPort.Materials", "SCIEPort.Industry", "SCIEPort.Building", "SCIEPort.Creatures", "SCIEPort.Misc" };
        internal static readonly string[] Labels = { "神器", "载具", "圣兽套装", "生态植物", "食材", "工业材料", "工业部件", "建筑装置", "生物生成", "移植杂项" };
        internal enum Theme { None, Mjolnir, FalseHammer, Gungnir, Poseidon, OmniscientEye, DevouringEye, Qinglong, Xuanwu, Baihu, Zhuque, Artifact }
        internal struct Rgba
        {
            internal byte R,G,B,A;
            internal Rgba(int r,int g,int b,int a) { R=(byte)r;G=(byte)g;B=(byte)b;A=(byte)a; }
        }
        internal struct Skin { internal Rgba Center, Border; }
        internal static bool IsCategory(string category) => Array.IndexOf(Categories,category)>=0;
        internal static bool IsSaint(int contents,int clothingIndex) => contents==203 && clothingIndex>=200 && clothingIndex<=203;
        internal static Theme Select(int contents,int clothingIndex,string category)
        {
            if(IsSaint(contents,clothingIndex))return (Theme)((int)Theme.Qinglong+clothingIndex-200);
            switch(contents)
            {
                case 942:return Theme.Mjolnir;
                case 943:return Theme.FalseHammer;
                case 948:return Theme.Gungnir;
                case 949:return Theme.OmniscientEye;
                case 950:return Theme.DevouringEye; // Reserved port-branch artifact; not registered here.
                case 951:return Theme.Poseidon;
                default:return category==Artifacts ? Theme.Artifact : Theme.None;
            }
        }
        internal static Skin Colors(Theme theme,float pulse)
        {
            if(float.IsNaN(pulse) || float.IsInfinity(pulse))pulse=0;
            pulse=Math.Max(0,Math.Min(1,pulse));
            switch(theme)
            {
                case Theme.Mjolnir:return Pair(52,40,108,165,34,24,60,45,140,110,235,200,75,80,20,55,pulse);
                case Theme.Gungnir:return Pair(34,37,45,182,6,6,6,12,197,153,81,220,8,8,8,16,pulse);
                case Theme.Poseidon:return Pair(8,40,75,195,6,38,38,20,185,147,70,225,58,69,70,25,pulse);
                case Theme.FalseHammer:return Pair(38,40,52,195,10,11,14,15,135,122,101,220,22,21,20,20,pulse);
                case Theme.OmniscientEye:return Pair(12,24,64,205,9,16,25,15,185,132,60,225,46,54,40,25,pulse);
                case Theme.DevouringEye:return Pair(28,16,48,205,15,9,24,15,156,113,68,225,38,24,52,25,pulse);
                case Theme.Qinglong:return Pair(12,52,37,205,9,23,16,15,84,178,127,225,35,40,27,25,pulse);
                case Theme.Xuanwu:return Pair(13,31,63,205,8,18,29,15,98,171,211,225,34,36,30,25,pulse);
                case Theme.Baihu:return Pair(47,46,43,205,17,17,16,15,202,187,143,225,31,34,38,25,pulse);
                case Theme.Zhuque:return Pair(65,22,20,205,24,13,8,15,214,119,59,225,36,46,34,25,pulse);
                case Theme.Artifact:return Pair(24,29,57,205,10,10,16,15,187,151,80,225,35,34,30,25,pulse);
                default:return default(Skin);
            }
        }
        private static Skin Pair(int r,int g,int b,int a,int dr,int dg,int db,int da,int x,int y,int z,int w,int dx,int dy,int dz,int dw,float p)
            => new Skin {Center=new Rgba((int)(r+dr*p),(int)(g+dg*p),(int)(b+db*p),(int)(a+da*p)),Border=new Rgba((int)(x+dx*p),(int)(y+dy*p),(int)(z+dz*p),(int)(w+dw*p))};
    }
}
