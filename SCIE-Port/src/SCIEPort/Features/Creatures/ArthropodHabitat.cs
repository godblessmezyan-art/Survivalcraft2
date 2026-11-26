using Engine;
using Game;

namespace SCIEPort.Features.Creatures
{
    internal static class ArthropodHabitat
    {
        internal static readonly Point2[] Directions =
        {
            new Point2(1, 0), new Point2(0, 1), new Point2(-1, 0), new Point2(0, -1),
            new Point2(1, 1), new Point2(-1, 1), new Point2(-1, -1), new Point2(1, -1)
        };

        internal static bool IsNight(float time) => time < .23f || time >= .77f;

        internal static Block At(Terrain terrain, int x, int y, int z)
            => BlocksManager.Blocks[terrain.GetCellContents(x, y, z)];

        internal static bool IsSand(Block block) => block is SandBlock || block is SandstoneBlock;

        internal static bool ShallowWater(Terrain terrain, int x, int y, int z)
            => At(terrain, x, y, z) is WaterBlock && At(terrain, x, y - 1, z).IsCollidable
                && At(terrain, x, y + 1, z) is AirBlock;

        internal static bool Suitable(Terrain terrain, int species, Point3 point)
        {
            int x = point.X, y = point.Y, z = point.Z;
            if (terrain.GetTopHeight(x, z) >= y || !(At(terrain, x, y, z) is AirBlock)
                || !(At(terrain, x, y + 1, z) is AirBlock)) return false;

            Block floor = At(terrain, x, y - 1, z);
            int temperature = terrain.GetTemperature(x, z);
            if (species == WildlifeRules.SandScorpion)
                return y <= 126 && temperature >= 9 && terrain.GetHumidity(x, z) <= 5 && IsSand(floor);

            if (temperature < 5 || !(floor is SandBlock || floor is GravelBlock || floor is DirtBlock)) return false;
            for (int distance = 1; distance <= 2; distance++)
            foreach (Point2 direction in Directions)
                if (ShallowWater(terrain, x + distance * direction.X, y - 1, z + distance * direction.Y))
                    return true;
            return false;
        }
    }
}
