using SurvivalcraftRuins.Core;

namespace SurvivalcraftRuins.Builders;

/// <summary>
/// Hunter camp: campfire, two plank lean-tos, a log pile and a drying rack.
/// Small, cheap, and common — pure "someone was here" world dressing.
/// Origin (0,0,0) is ground level at the campfire.
/// </summary>
public static class CampBuilder
{
    public static StructureData Build(int seed)
    {
        var random = new Random(seed);
        var b = new StructureBuilder("hunter_camp");

        b.Set(0, 0, 0, "CampfireBlock");

        // Lean-to A: sloped plank roof over a carpet floor, chest at the back.
        BuildLeanTo(b, -4, -1, facingPositiveX: true);
        b.Chest(-4, 1, 0);

        // Lean-to B on the other side, slightly offset.
        BuildLeanTo(b, 3, 1, facingPositiveX: false);
        b.Chest(3, 1, 2);

        // Log pile.
        int logs = 2 + random.Next(2);
        for (int i = 0; i < logs; i++)
        {
            b.Set(-1 + i, 1, 3, "OakWoodBlock", 1);
        }
        b.Set(-1, 1, 4, "OakWoodBlock", 1);

        // Drying rack: two fence posts and a beam.
        b.Set(0, 1, -3, "WoodenFenceBlock");
        b.Set(0, 2, -3, "WoodenFenceBlock");
        b.Set(2, 1, -3, "WoodenFenceBlock");
        b.Set(2, 2, -3, "WoodenFenceBlock");
        b.Set(1, 2, -3, "PlanksBlock");
        b.Set(2, 3, -3, "TorchBlock", 4);

        return b.Build();
    }

    private static void BuildLeanTo(StructureBuilder b, int x, int z, bool facingPositiveX)
    {
        int back = facingPositiveX ? x - 1 : x + 1;
        for (int dz = -1; dz <= 1; dz++)
        {
            // Back wall, two tall.
            b.Set(back, 1, z + dz, "PlanksBlock");
            b.Set(back, 2, z + dz, "PlanksBlock");
            // Roof: high edge over the back, low edge over the front.
            b.Set(x, 3, z + dz, "PlanksBlock");
            b.Set(facingPositiveX ? x + 1 : x - 1, 2, z + dz, "PlanksBlock");
            // Floor.
            b.Set(x, 0, z + dz, "PlanksBlock");
            b.Set(facingPositiveX ? x + 1 : x - 1, 0, z + dz, "PlanksBlock");
        }
    }
}
