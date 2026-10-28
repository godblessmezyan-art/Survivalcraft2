using System.Collections.Generic;
using System.IO;
using System.Text;
using Game;

namespace SCIEPort.Features.Inventory
{
    /// <summary>Uses the real AddCategory/LanguageControl ABI; the creative panel filters raw keys, not labels.</summary>
    internal static class PortInventoryCatalog
    {
        internal static void Initialize()
        {
            var json=new StringBuilder("{\"BlocksManager\":{");
            for(int i=0;i<PortInventoryRules.Categories.Length;i++)
            {
                if(i>0)json.Append(',');
                json.Append('"').Append(PortInventoryRules.Categories[i]).Append("\":\"").Append(PortInventoryRules.Labels[i]).Append('"');
            }
            json.Append("}}");
            using(var stream=new MemoryStream(Encoding.UTF8.GetBytes(json.ToString())))LanguageControl.loadJson(stream);
            var used=new HashSet<string> {PortInventoryRules.Saints};
            foreach(Block block in BlocksManager.Blocks)
            {
                if(block==null || block.GetType().Assembly!=typeof(PortInventoryCatalog).Assembly || block.BlockIndex==203)continue;
                if(!PortInventoryRules.IsCategory(block.DefaultCategory))
                {
                    Engine.Log.Warning("[SCIEPort:Inventory] Missing dedicated category for "+block.GetType().Name+"; using SCIEPort.Misc. Release validation must reject this.");
                    block.DefaultCategory="SCIEPort.Misc";
                }
                used.Add(block.DefaultCategory);
            }
            // Only reorder this namespace, before any world inventory widget has been created.
            foreach(string key in PortInventoryRules.Categories)BlocksManager.m_categories.Remove(key);
            foreach(string key in PortInventoryRules.Categories)if(used.Contains(key))BlocksManager.AddCategory(key);
        }
    }
}
