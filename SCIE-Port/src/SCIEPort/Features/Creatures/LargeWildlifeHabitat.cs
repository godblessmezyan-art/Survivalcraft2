using Engine;
using Game;

namespace SCIEPort.Features.Creatures
{
    internal static class LargeWildlifeHabitat
    {
        internal const float RayLaunchHeight = 7f;
        internal const float TurtleHalfBox = 1.19f;

        internal static bool Ready(Terrain terrain, int x, int z)
        {
            TerrainChunk chunk = terrain.GetChunkAtCell(x, z);
            return chunk != null && chunk.State >= TerrainChunkState.InvalidLight;
        }

        private static Block At(Terrain terrain, int x, int y, int z)
            => ArthropodHabitat.At(terrain, x, y, z);

        private static bool Natural(Block block)
            => block is GrassBlock || block is DirtBlock || block is SandBlock
                || block is GravelBlock || block is SandstoneBlock || block is GraniteBlock;

        internal static bool Suitable(Terrain terrain, bool ray, Point3 p)
        {
            int x = p.X, y = p.Y, z = p.Z;
            if (y < 4 || y > 186 || terrain.GetTopHeight(x, z) >= y || !Natural(At(terrain, x, y - 1, z)))
                return false;
            Vector3 point = new Vector3(x + .5f, y, z + .5f);
            if (ray)
                return y >= 75 && terrain.GetHumidity(x, z) >= 3
                    && TryFlightFloor(terrain, point, out float height) && height <= y + 3;
            return y <= 112 && terrain.GetTemperature(x, z) >= 5 && terrain.GetHumidity(x, z) >= 7
                && TurtleFooting(terrain, point, false, out _) && FindTurtleWater(terrain, point).HasValue;
        }

        internal static Vector3 SpawnPosition(Point3 p, bool ray)
            => new Vector3(p.X + .5f, p.Y + (ray ? RayLaunchHeight : .07f), p.Z + .5f);

        internal static Vector3? FindTurtleWater(Terrain terrain, Vector3 shore)
        {
            int x = Terrain.ToCell(shore.X), y = Terrain.ToCell(shore.Y), z = Terrain.ToCell(shore.Z);
            for (int radius = 4; radius <= 16; radius += 4)
            foreach (Point2 direction in ArthropodHabitat.Directions)
            {
                int wx = x + direction.X * radius, wz = z + direction.Y * radius;
                if (!TryFeet(terrain, wx, wz, y + 1, y - 3, true, out int feet) || !(At(terrain, wx, feet, wz) is WaterBlock))
                    continue;
                // Keep the shell's air-breathing head at or near the surface.
                if (At(terrain, wx, feet + 1, wz) is FluidBlock) continue;
                Vector3 candidate = new Vector3(wx + .5f, feet, wz + .5f);
                if (TurtleFooting(terrain, candidate, true, out _) && HasShoreRoute(terrain, shore, candidate))
                    return candidate;
            }
            return null;
        }

        private static bool HasShoreRoute(Terrain terrain, Vector3 shore, Vector3 water)
        {
            Vector2 range = (water - shore).XZ;
            int samples = (int)MathUtils.Ceiling(range.Length() / 1.35f);
            if (samples < 1) return true;
            int feet = Terrain.ToCell(shore.Y + .09f);
            for (int i = 1; i <= samples; i++)
            {
                Vector2 sample = shore.XZ + range * ((float)i / samples);
                if (!TurtleFooting(terrain, new Vector3(sample.X, feet, sample.Y), true, out feet))
                    return false;
            }
            return MathUtils.Abs(feet - water.Y) <= 1f;
        }

        internal static bool TurtleFooting(Terrain terrain, Vector3 center, bool allowWater, out int top)
        {
            int y = Terrain.ToCell(center.Y + .09f);
            top = y - 1;
            int bottom = y + 1;
            for (int x = Terrain.ToCell(center.X - TurtleHalfBox); x <= Terrain.ToCell(center.X + TurtleHalfBox); x++)
            for (int z = Terrain.ToCell(center.Z - TurtleHalfBox); z <= Terrain.ToCell(center.Z + TurtleHalfBox); z++)
            {
                if (!TryFeet(terrain, x, z, y + 1, y - 1, allowWater, out int feet)) return false;
                top = MathUtils.Max(top, feet);
                bottom = MathUtils.Min(bottom, feet);
                if (top - bottom > 1) return false;
            }
            return true;
        }

        private static bool TryFeet(Terrain terrain, int x, int z, int high, int low, bool allowWater, out int feet)
        {
            feet = 0;
            if (!Ready(terrain, x, z)) return false;
            for (int y = MathUtils.Min(high, 251); y >= MathUtils.Max(low, 2); y--)
            {
                if (!At(terrain, x, y - 1, z).IsCollidable) continue;
                Block body = At(terrain, x, y, z), head = At(terrain, x, y + 1, z);
                if (body.IsCollidable || head.IsCollidable || head is FluidBlock
                    || (body is FluidBlock && (!allowWater || !(body is WaterBlock)))) return false;
                feet = y;
                return true;
            }
            return false;
        }

        internal static bool TryFlightFloor(Terrain terrain, Vector3 point, out float floor)
        {
            floor = 0f;
            int x = Terrain.ToCell(point.X), z = Terrain.ToCell(point.Z);
            for (int dx = -4; dx <= 4; dx += 2)
            for (int dz = -4; dz <= 4; dz += 2)
            {
                if (!Ready(terrain, x + dx, z + dz)) return false;
                floor = MathUtils.Max(floor, terrain.GetTopHeight(x + dx, z + dz) + 1);
            }
            return true;
        }

        internal static bool TryPerch(Terrain terrain, Vector3 around, out Vector3 perch)
        {
            perch = default;
            int x = Terrain.ToCell(around.X), z = Terrain.ToCell(around.Z);
            if (!Ready(terrain, x, z)) return false;
            int y = terrain.GetTopHeight(x, z) + 1;
            for (int i = 0; i < 2 && y > 4; i++)
            {
                Block top = At(terrain, x, y - 1, z);
                if (top.IsCollidable || top is FluidBlock) break;
                y--;
            }
            if (y < 4 || y > 244 || !Natural(At(terrain, x, y - 1, z))) return false;
            for (int dx = -4; dx <= 4; dx++)
            for (int dz = -4; dz <= 4; dz++)
            {
                int px = x + dx, pz = z + dz;
                if (!Ready(terrain, px, pz) || terrain.GetTopHeight(px, pz) > y + 1
                    || At(terrain, px, y, pz).IsCollidable || At(terrain, px, y + 1, pz).IsCollidable
                    || At(terrain, px, y - 1, pz) is FluidBlock) return false;
                if (MathUtils.Abs(dx) <= 1 && MathUtils.Abs(dz) <= 1
                    && !At(terrain, px, y - 1, pz).IsCollidable) return false;
            }
            perch = new Vector3(x + .5f, y + .02f, z + .5f);
            return true;
        }
    }
}
