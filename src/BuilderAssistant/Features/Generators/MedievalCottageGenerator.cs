using BuilderAssistant.Core;
using BuilderAssistant.Core.Blueprints;

namespace BuilderAssistant.Features.Generators
{
    /// <summary>
    /// The first playable structure: a medieval fantasy cottage, 9 wide (X),
    /// 11 long (Z), 10 tall (Y). Built from composable relative-coordinate
    /// modules (foundation, walls, timber frame, windows, doorway, stepped roof,
    /// chimney, interior posts) - NOT hardcoded world positions, and NOT a hollow
    /// box.
    ///
    /// Since schema v2 it also uses the newly supported oriented families:
    ///   - a wooden door in the front doorway (WoodenDoorBlock, two-cell),
    ///   - wooden stairs around the eave so the roof reads as a slope
    ///     (WoodenStairsBlock, exercising all four facings),
    ///   - stone slabs as windowsills under the front windows (StoneSlabBlock),
    ///   - wooden fence posts capping the roof ridge ends (WoodenFenceBlock).
    /// These overwrite cells the plain-cube version already occupied (or fill an
    /// existing carved opening), so the block count and safety envelope are
    /// unchanged and every write still flows through the same planner/gateway.
    /// </summary>
    internal sealed class MedievalCottageGenerator : IBuildingGenerator
    {
        public const int Width = 9;   // X: 0..8
        public const int Length = 11; // Z: 0..10
        public const int Height = 10; // Y: 0..9
        private const int BlueprintVersion = 2;

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
            AddWindowSills(b, x0, z0, x1, z1);
            CarveDoorway(b, x0, z0, x1, z1);
            AddDoor(b, x0, z0, x1, z1);
            AddRoof(b, x0, z0, x1, z1);
            AddStairEaves(b, x0, z0, x1, z1);
            AddRidgeFence(b, x0, z0, x1, z1);
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

        // Stone slab windowsills (lower half) under the two front windows. The sill
        // overwrites the wall cell directly beneath each window pane, so it adds no
        // cells; isTop=0 means the bottom half so it reads as a ledge.
        private static void AddWindowSills(BlueprintBuilder b, int x0, int z0, int x1, int z1)
        {
            b.SetOriented(1, 2, z0, BuildingMaterials.WindowSill, BlockFamily.Slab, 0);
            b.SetOriented(7, 2, z0, BuildingMaterials.WindowSill, BlockFamily.Slab, 0);
        }

        // A 2x2 doorway opening carved through the front wall.
        private static void CarveDoorway(BlueprintBuilder b, int x0, int z0, int x1, int z1)
        {
            b.Carve(3, 1, z0, 4, 2, z0);
        }

        // Fill the left half of the doorway with a wooden door. Only the bottom cell is
        // authored: writing it makes SubsystemDoorBlockBehavior auto-create the top cell,
        // and the cell below (foundation at y=0) is solid support, which the engine requires.
        // Rotation 2 faces +Z (into the room); closed (open bit 0); right-handed (bit3 0).
        private static void AddDoor(BlueprintBuilder b, int x0, int z0, int x1, int z1)
        {
            b.SetOriented(3, 1, z0, BuildingMaterials.EntryDoor, BlockFamily.Door, 2);
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

        // Turn the outer eave ring (y=5 border) into wooden stairs so the roof edge
        // reads as a slope. Each side faces inward/up so the two step-boxes sit against
        // the wall below (y=4). This overwrites existing eave cells - no net block added -
        // and exercises all four stair facings so blueprint rotation is meaningful.
        private static void AddStairEaves(BlueprintBuilder b, int x0, int z0, int x1, int z1)
        {
            const int y = 5;
            const int up0 = 0; // right-side up
            // Front (z=z0): high side toward +Z (inward) -> rotation 2.
            for (int x = x0; x <= x1; x++)
            {
                if (x == 3) continue; // leave the doorway header clear of stairs above the door
                b.SetOriented(x, y, z0, BuildingMaterials.RoofStairs, BlockFamily.Stairs, 2 | up0);
            }
            // Back (z=z1): high side toward -Z (inward) -> rotation 0.
            for (int x = x0; x <= x1; x++)
            {
                b.SetOriented(x, y, z1, BuildingMaterials.RoofStairs, BlockFamily.Stairs, 0 | up0);
            }
            // Left (x=x0): high side toward +X (inward) -> rotation 3. Interior only (corners done above).
            for (int z = z0 + 1; z <= z1 - 1; z++)
            {
                b.SetOriented(x0, y, z, BuildingMaterials.RoofStairs, BlockFamily.Stairs, 3 | up0);
            }
            // Right (x=x1): high side toward -X (inward) -> rotation 1.
            for (int z = z0 + 1; z <= z1 - 1; z++)
            {
                b.SetOriented(x1, y, z, BuildingMaterials.RoofStairs, BlockFamily.Stairs, 1 | up0);
            }
        }

        // Wooden fence posts capping the two ends of the ridge, as simple finials. They
        // overwrite existing ridge cells (no net block added). The engine auto-connects a
        // lone post to nothing, so both render as single posts; connection bits start 0.
        private static void AddRidgeFence(BlueprintBuilder b, int x0, int z0, int x1, int z1)
        {
            b.SetOriented(4, 9, 4, BuildingMaterials.GardenFence, BlockFamily.Fence, 0);
            b.SetOriented(4, 9, 6, BuildingMaterials.GardenFence, BlockFamily.Fence, 0);
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
