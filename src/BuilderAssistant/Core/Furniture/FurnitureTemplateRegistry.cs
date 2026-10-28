using System;
using System.Collections.Generic;
using BuilderAssistant.Core.Blueprints;

namespace BuilderAssistant.Core.Furniture
{
    internal sealed class FurnitureTemplateRegistry
    {
        private readonly Dictionary<string, FurnitureTemplate> _templates =
            new Dictionary<string, FurnitureTemplate>(StringComparer.Ordinal);
        public static FurnitureTemplateRegistry Default { get; } = CreateDefault();
        public IEnumerable<FurnitureTemplate> Templates { get { return _templates.Values; } }
        public void Register(FurnitureTemplate template)
        {
            if (template == null) throw new ArgumentNullException("template");
            if (_templates.ContainsKey(template.Id)) throw new ArgumentException("重复家具模板 id: " + template.Id);
            _templates.Add(template.Id, template);
        }
        public bool TryGet(string id, out FurnitureTemplate template)
        {
            template = null;
            return id != null && _templates.TryGetValue(id, out template);
        }

        private static FurnitureTemplateRegistry CreateDefault()
        {
            var registry = new FurnitureTemplateRegistry();
            string[] ids = { "TallBookshelf_A", "WideBookshelf_B", "CornerBookshelf_C",
                "ReadingDesk_A", "WritingDesk_B", "WoodChair_A", "Bench_A", "Cabinet_A", "Bed_A", "LampStand_A" };
            string[] names = { "高书架", "宽书架", "转角书架", "阅读桌", "写字桌", "木椅", "长椅", "木柜", "床榻", "灯架" };
            for (int i = 0; i < ids.Length; i++)
            {
                var b = new BlueprintBuilder();
                if (i <= 2)
                {
                    int width = i == 1 ? 5 : 3, height = i == 1 ? 3 : 4;
                    for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        bool frame = x == 0 || x == width - 1 || y == 0 || y == height - 1;
                        b.Set(x, y, 0, frame ? "PlanksBlock" : "BricksBlock");
                        if (i == 2) b.Set(0, y, x, frame ? "PlanksBlock" : "StoneBrickBlock");
                    }
                }
                else if (i == 3 || i == 4)
                {
                    int width = i == 3 ? 3 : 4;
                    b.SetOriented(0, 0, 0, "WoodenFenceBlock", BlockFamily.Fence, 0);
                    b.SetOriented(width - 1, 0, 0, "WoodenFenceBlock", BlockFamily.Fence, 0);
                    for (int x = 0; x < width; x++) b.SetOriented(x, 1, 0, "WoodenSlabBlock", BlockFamily.Slab, 1);
                    if (i == 4) b.Set(3, 0, 0, "PlanksBlock");
                }
                else if (i == 5 || i == 6)
                {
                    int width = i == 5 ? 1 : 3;
                    for (int x = 0; x < width; x++)
                    {
                        b.SetOriented(x, 0, 1, "WoodenStairsBlock", BlockFamily.Stairs, 0);
                        b.Set(x, 0, 0, "PlanksBlock");
                        b.SetOriented(x, 1, 0, "WoodenSlabBlock", BlockFamily.Slab, 0);
                    }
                }
                else if (i == 7)
                {
                    b.FillBox(0, 0, 0, 1, 2, 0, "PlanksBlock");
                    b.SetOriented(0, 3, 0, "WoodenSlabBlock", BlockFamily.Slab, 0);
                    b.SetOriented(1, 3, 0, "WoodenSlabBlock", BlockFamily.Slab, 0);
                }
                else if (i == 8)
                {
                    b.FillBox(0, 0, 0, 1, 0, 2, "PlanksBlock");
                    b.FillBox(0, 1, 0, 1, 1, 0, "PlanksBlock");
                    for (int x = 0; x < 2; x++)
                    for (int z = 1; z < 3; z++) b.SetOriented(x, 1, z, "WoodenSlabBlock", BlockFamily.Slab, 0);
                }
                else
                {
                    b.SetOriented(0, 0, 0, "WoodenSlabBlock", BlockFamily.Slab, 0);
                    b.SetOriented(0, 1, 0, "WoodenFenceBlock", BlockFamily.Fence, 0);
                    b.Set(0, 2, 0, "GlassBlock");
                    b.SetOriented(0, 3, 0, "WoodenSlabBlock", BlockFamily.Slab, 0);
                }
                registry.Register(new FurnitureTemplate(ids[i], names[i], b.ToBlueprint(ids[i], 2)));
            }
            return registry;
        }
    }
}
