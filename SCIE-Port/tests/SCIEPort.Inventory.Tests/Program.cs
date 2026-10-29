using System;
using System.Collections.Generic;
using SCIEPort.Features.Inventory;

internal static class Program
{
    private static int s_checks;
    private static void Check(bool b,string message){if(!b)throw new Exception(message);s_checks++;}
    private static void Main()
    {
        Check(PortInventoryRules.Categories.Length==PortInventoryRules.Labels.Length,"category labels");
        var keys=new HashSet<string>();var labels=new HashSet<string>();
        for(int i=0;i<PortInventoryRules.Categories.Length;i++)
        {
            string key=PortInventoryRules.Categories[i];
            Check(key.StartsWith("SCIEPort.") && keys.Add(key) && labels.Add(PortInventoryRules.Labels[i]),"namespaced unique categories");
        }
        foreach(string native in new[]{"Weapons","Items","Food","Clothes","Plants","神器","载具"})Check(!PortInventoryRules.IsCategory(native),"raw/native category must not leak");
        for(int index=0;index<256;index++)
        {
            Check(PortInventoryRules.IsSaint(203,index)==(index>=200 && index<=203),"clothing DATA isolation");
            Check(!PortInventoryRules.IsSaint(204,index),"not another block");
        }
        foreach(int id in new[]{942,943,948,949,950,951,952})
            Check(PortInventoryRules.Select(id,-1,PortInventoryRules.Artifacts)!=PortInventoryRules.Theme.Artifact,"current artifacts require authored themes");
        Check(PortInventoryRules.Select(952,-1,PortInventoryRules.Artifacts)==PortInventoryRules.Theme.AbyssReaper,"Abyss uses its own violet metal theme");
        Check(PortInventoryRules.Select(970,-1,PortInventoryRules.Artifacts)==PortInventoryRules.Theme.Artifact,"future artifact fallback");
        Check(PortInventoryRules.Select(943,-1,PortInventoryRules.Artifacts)==PortInventoryRules.Theme.FalseHammer,"fake hammer gets its own subdued skin, not the true hammer skin");
        Check(PortInventoryRules.Select(1,-1,"Terrain")==PortInventoryRules.Theme.None,"ordinary item stays native");
        var backgrounds=new HashSet<string>();
        for(int i=200;i<=203;i++)
        {
            var style=PortInventoryRules.Select(203,i,"Clothes");
            var c=PortInventoryRules.Colors(style,0).Center;
            Check(backgrounds.Add(c.R+","+c.G+","+c.B),"Four Saints must have four distinct backgrounds");
        }
        foreach(PortInventoryRules.Theme style in Enum.GetValues(typeof(PortInventoryRules.Theme)))
        foreach(float pulse in new[]{-1f,0f,.25f,.5f,1f,2f,float.NaN,float.PositiveInfinity})
        {
            var skin=PortInventoryRules.Colors(style,pulse);
            if(style!=PortInventoryRules.Theme.None)
            {
                Check(skin.Center.A>=165 && skin.Border.A>=200,"readable opaque-enough base/border");
                Check(skin.Center.R+skin.Center.G+skin.Center.B < skin.Border.R+skin.Border.G+skin.Border.B,"frame must be brighter than base");
            }
        }
        var g=PortInventoryRules.Colors(PortInventoryRules.Theme.Gungnir,0);
        Check(g.Center.R==34 && g.Center.G==37 && g.Center.B==45 && g.Border.R==197 && g.Border.G==153 && g.Border.B==81,"Gungnir original obsidian/antique gold preserved");
        var m=PortInventoryRules.Colors(PortInventoryRules.Theme.Mjolnir,1);
        Check(m.Center.R==86 && m.Center.G==64 && m.Center.B==168 && m.Border.B==255,"Mjolnir blue-violet preserved");
        Console.WriteLine("Inventory category/skin rules: "+s_checks+" assertions passed (not live UI).");
    }
}
