using System;
using Game;

namespace SCIEPort.Features.Plants
{
    internal static class PlantGrowthSite
    {
        internal static bool IsLiving(SubsystemTerrain subsystem)
            => subsystem.SubsystemGameInfo == null ||
                subsystem.SubsystemGameInfo.WorldSettings.EnvironmentBehaviorMode == EnvironmentBehaviorMode.Living;

        internal static bool HasWater(int x, int y, int z, Func<int, int, int, int> getContents)
        {
            if (x < int.MinValue + 2 || x > int.MaxValue - 2 ||
                z < int.MinValue + 2 || z > int.MaxValue - 2 || y < 2 || y > 254) return false;

            for (int dx = -2; dx <= 2; dx++)
            for (int dz = -2; dz <= 2; dz++)
            for (int dy = -2; dy <= 0; dy++)
            {
                if (BlocksManager.Blocks[getContents(x + dx, y + dy, z + dz)] is WaterBlock)
                    return true;
            }

            return false;
        }
    }
}
