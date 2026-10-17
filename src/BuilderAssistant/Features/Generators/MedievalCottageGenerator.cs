using BuilderAssistant.Core;
using BuilderAssistant.Core.Blueprints;

namespace BuilderAssistant.Features.Generators
{
    /// <summary>
    /// The first playable structure: a medieval fantasy cottage, 9 wide (X),
    /// 11 long (Z), 10 tall (Y). Built from composable relative-coordinate
    /// modules (foundation, walls, timber frame, windows, doorway, layered hip
    /// roof, chimney, interior posts) - NOT hardcoded world positions, and NOT
    /// a hollow box. Uses only plain cube vanilla blocks (no stair/slab/door
    /// blocks that would need orientation data bits), so every write is safe.
    /// </summary>
    internal sealed class MedievalCottageGenerator : IBuildingGenerator
    {
        public const int Width = 9;   // X: 0..8
        public const int Length = 11; // Z: 0..10
        public const int Height = 10; // Y: 0..9
        private const int BlueprintVersion = 1;

        public string Key { get { return "medieval_cottage"; } }
        public string DisplayName { get { return "中世纪奇幻小屋 9x11x10"; } }

        public Blueprint Generate(GenerationContext context)
        {
            BlueprintBuilder b = new BlueprintBuilder();

            const int x0 = 0, x1 = Width - 1;   // 0..8
            const int z0 = 0, z1 = Length - 1;  // 0..10

            BuildFoundation(b, x0, z0, x1, z1);
            BuildWalls(b, x0, z0, x1, z1);
            AddTimberFrame(b, x0, z0, x1, z1);
            AddWindows(b, x0, z0, x1, z1);
            CarveDoorway(b, x0, z0, x1, z1);
            AddRoof(b, x0, z0, x1, z1);
            AddChimney(b);
            AddInterior(b, x0, z0, x1, z1);

            return b.ToBlueprint(Key, BlueprintVersion);
        }

        // Solid stone plinth that also serves as the interior floor.
        private static void BuildFoundation(BlueprintBuilder b, int x0, int z0, int x1, int z1)
        {
            b.FillBox(x0, 0, z0, x1, 0, z1, BuildingMaterials.StoneBase);
        }

        // Timber cottage walls, four blocks high, enclosing real interior.
        private static void BuildWalls(BlueprintBuilder b, int x0, int z0, int x1, int z1)
        {
            b.Walls(x0, z0, x1, z1, 1, 4, BuildingMaterials.WoodWall);
        }

        // Dark corner + mid posts read as a half-timbered frame.
        private static void AddTimberFrame(BlueprintBuilder b, int x0, int z0, int x1, int z1)
        {
            int[] xs = { x0, x0 + 4, x1 };
            int[] zs = { z0, z1 };
            for (int y = 1; y <= 4; y++)
            {
                for (int xi = 0; xi < xs.Length; xi++)
                {
                    for (int zi = 0; zi < zs.Length; zi++)
                    {
                        b.Set(xs[xi], y, zs[zi], BuildingMaterials.WoodTrim);
                    }
                }
                // horizontal mid rail on both long walls
                b.Set(x0, y, 5, BuildingMaterials.WoodTrim);
                b.Set(x1, y, 5, BuildingMaterials.WoodTrim);
            }
        }

        // Small glass windows embedded in the walls (glass is a plain cube, no data bits).
        private static void AddWindows(BlueprintBuilder b, int x0, int z0, int x1, int z1)
        {
            // Front (z = z0) windows flanking the door.
            b.Set(1, 3, z0, BuildingMaterials.Glass);
            b.Set(7, 3, z0, BuildingMaterials.Glass);
            // Back wall windows.
            b.Set(2, 2, z1, BuildingMaterials.Glass);
            b.Set(6, 2, z1, BuildingMaterials.Glass);
            b.Set(2, 3, z1, BuildingMaterials.Glass);
            b.Set(6, 3, z1, BuildingMaterials.Glass);
            // Side wall windows.
            b.Set(x0, 2, 3, BuildingMaterials.Glass);
            b.Set(x0, 2, 7, BuildingMaterials.Glass);
            b.Set(x1, 2, 3, BuildingMaterials.Glass);
            b.Set(x1, 2, 7, BuildingMaterials.Glass);
        }

        // A 2x2 doorway opening carved through the front wall (a real hole, not a door block).
        private static void CarveDoorway(BlueprintBuilder b, int x0, int z0, int x1, int z1)
        {
            b.Carve(3, 1, z0, 4, 2, z0);
        }

        // Layered hip roof: full eave plate, then three stepped inset layers to a ridge.
        private static void AddRoof(BlueprintBuilder b, int x0, int z0, int x1, int z1)
        {
            b.Layer(x0, z0, x1, z1, 5, BuildingMaterials.Roof);        // eave / ceiling, 9x11
            b.Layer(x0 + 1, z0 + 1, x1 - 1, z1 - 1, 6, BuildingMaterials.Roof); // 7x9
            b.Layer(x0 + 2, z0 + 2, x1 - 2, z1 - 2, 7, BuildingMaterials.Roof); // 5x7
            b.Layer(x0 + 3, z0 + 3, x1 - 3, z1 - 3, 8, BuildingMaterials.Roof); // 3x5
            b.FillBox(4, 9, 4, 4, 9, 6, BuildingMaterials.Roof);        // ridge, 1x3
        }

        // Stone chimney stack near the back, poking up past the roof.
        private static void AddChimney(BlueprintBuilder b)
        {
            for (int y = 1; y <= 9; y++)
            {
                b.Set(6, y, 8, BuildingMaterials.StoneWall);
            }
            b.Set(6, 0, 8, BuildingMaterials.StoneWall); // hearth base
        }

        // A couple of interior posts and a small hearth so the inside is not empty.
        private static void AddInterior(BlueprintBuilder b, int x0, int z0, int x1, int z1)
        {
            for (int y = 1; y <= 4; y++)
            {
                b.Set(2, y, 3, BuildingMaterials.WoodTrim);
                b.Set(6, y, 3, BuildingMaterials.WoodTrim);
            }
            // hearth stones
            b.Set(5, 0, 8, BuildingMaterials.StoneWall);
            b.Set(7, 0, 8, BuildingMaterials.StoneWall);
            // a low "table" of planks
            b.Set(3, 1, 6, BuildingMaterials.WoodWall);
            b.Set(3, 1, 7, BuildingMaterials.WoodWall);
        }
    }
}
