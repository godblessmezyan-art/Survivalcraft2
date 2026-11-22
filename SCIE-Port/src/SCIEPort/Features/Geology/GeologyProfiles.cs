using System;
using Game;
using static SCIEPort.Features.Geology.GeologyKind;

namespace SCIEPort.Features.Geology
{
    internal struct GeologyColumn
    {
        internal float Weight;
        internal int Surface, Cap, Rock, ColorBand;
        internal int Liquid, Waterline, SourceLine;
        internal int CaveFloor, CaveRoof, CaveLining;

        internal GeologyColumn(float weight, int surface, int cap, int rock)
        {
            this = default;
            Weight = weight;
            Surface = surface;
            Cap = Terrain.MakeBlockValue(cap);
            Rock = Terrain.MakeBlockValue(rock);
        }

        internal void Flood(int fluidValue, int waterline, int sourceLine = 0)
        {
            Liquid = fluidValue;
            Waterline = waterline;
            SourceLine = sourceLine == 0 ? waterline : sourceLine;
        }

        internal void Tunnel(int floor, int roof, int lining)
        {
            CaveFloor = floor;
            CaveRoof = roof;
            CaveLining = Terrain.MakeBlockValue(lining);
        }

        internal int MaterialAt(int y, int elevation)
        {
            if (ColorBand != 0)
            {
                int band = GeologySites.FloorDiv(y + ColorBand, 3) & 3;
                return Terrain.MakeBlockValue(PaintedStrataBlock.Index, 0, band);
            }
            return y == elevation ? Cap : Rock;
        }
    }

    internal static class GeologyProfiles
    {
        internal const int VolcanoApron = 8;
        private static readonly int[] TerraceHalfWidths = { 0, 2, 4, 5, 5, 4, 3, 0 };

        internal static GeologyColumn At(GeologySite site, int x, int z)
        {
            int dx = x - site.X, dz = z - site.Z;
            float radius = (float)Math.Sqrt(dx * dx + dz * dz);
            float weight = Edge((site.Radius - radius) / 7f);
            int v = dx * site.Facing.X + dz * site.Facing.Y;
            int u = dz * site.Facing.X - dx * site.Facing.Y;
            if (weight == 0 && !(GeologySites.IsVolcano(site.Kind) && v <= -8 &&
                v > -site.Extent && Math.Abs(u) < 6)) return default;
            switch (site.Kind)
            {
                case Crater:
                case MagmaCrater:
                case CraterLakes: return Volcano(site, u, v, radius, weight);
                case Terraces: return TravertinePools(site, u, v, weight);
                case PaintedHills: return Painted(site, u, v, weight);
                case BasaltCoast: return Coast(site, u, v, weight);
                case Pinnacles: return StoneForest(site, u, v, weight);
                case ColumnarBasalt: return BasaltPlateau(site, u, v, weight);
                case DryCanyon: return Canyon(site, u, v, weight);
                case RiverCave:
                case IceCave: return Cavern(site, u, v, weight);
                default: return SaltBasin(site, u, v, radius, weight);
            }
        }

        private static GeologyColumn Volcano(GeologySite site, int u, int v, float radius, float weight)
        {
            bool lakes = site.Kind == CraterLakes;
            int floor = site.Base + (lakes ? 4 : -4);
            float ring = (radius + 1.8f * (float)Math.Sin(u * .12 + v * .15)) / site.Radius;
            float ramp = ring <= .57f ? Edge((ring - .31f) / .26f) : Edge((1f - ring) / .43f);
            int height = floor + (int)Math.Round(ramp * (lakes ? 24 : 23));
            int reach = -v;
            int side = Math.Abs(u);
            if (reach >= 8 && side <= 6)
            {
                // Keep the central five columns on grade through the ring's outer edge.
                // Fade the extended approach beyond the circular part of the mountain.
                float grade = (reach - 8f) / (site.Radius - 9);
                int trail = Mix(floor, site.Outlet, Math.Min(grade, 1f));
                float shoulder = Edge((6f - side) / 4f);
                height = Mix(height, trail, shoulder);
                weight = Math.Max(weight, shoulder * Edge((site.Extent - reach) / (VolcanoApron + 1f)));
            }

            int cap = radius > site.Radius * .72f || (radius < 14 && GeologySites.Grain(u, v, site.Seed, 7) == 0)
                ? VolcanicSandBlock.Index : VolcanicRockBlock.Index;
            if (site.Kind == Crater && radius >= 3 && radius <= 12 &&
                Math.Abs(u - .54 * v + 1.4 * Math.Sin(v * .6)) < .65)
            {
                height--;
                cap = VolcanicSandBlock.Index;
            }
            var result = new GeologyColumn(weight, height, cap, VolcanicRockBlock.Index);
            if (lakes)
            {
                AddCraterLake(ref result, site, u + 16, v, 0, 4);
                AddCraterLake(ref result, site, u - 16, v, 1, 6);
                AddCraterLake(ref result, site, u, v - 16, 2, 5);
                return result;
            }

            if (site.Kind == MagmaCrater && u * u + v * v <= 41)
            {
                result.Surface = floor - (u * u + v * v < 20 ? 5 : 4);
                result.Flood(Terrain.MakeBlockValue(MagmaBlock.Index), floor - 2);
            }

            int center = (int)Math.Round(2 * Math.Sin((v - 8) * .19));
            if (v >= 8 && v <= 32 && Math.Abs(u - center) <= 1)
            {
                int tunnelFloor = floor + (v - 8) / 3;
                if (v < 27) result.Tunnel(tunnelFloor, tunnelFloor + 3, VolcanicRockBlock.Index);
                else result.Surface = Math.Min(result.Surface, tunnelFloor);
            }
            return result;
        }

        private static void AddCraterLake(ref GeologyColumn column, GeologySite site, int x, int z, int color, int elevation)
        {
            int distance = x * x + z * z;
            if (distance > 49) return;
            int level = site.Base + elevation;
            if (distance <= 30)
            {
                column.Surface = level - (distance <= 11 ? 4 : distance <= 21 ? 3 : 2);
                column.Cap = Terrain.MakeBlockValue(color == 0 ? TravertineBlock.Index : VolcanicRockBlock.Index);
                column.Flood(MineralWaterBlock.Make(color), level);
            }
            else
            {
                column.Surface = Mix(level + 1, Math.Max(level + 2, column.Surface), Edge((distance - 31f) / 19f));
                column.Cap = Terrain.MakeBlockValue(VolcanicRockBlock.Index);
            }
        }

        private static GeologyColumn TravertinePools(GeologySite site, int u, int v, float weight)
        {
            int side = Math.Abs(u);
            if (v < -14 || v > 13 || side > 8) return default;
            weight *= Edge((9f - side) / 3) * Edge((v + 15f) / 3) * Edge((14f - v) / 3);
            int tier = Math.Max(0, Math.Min(2, (v + 12) / 8));
            int level = site.Base + 9 - tier * 4;
            int row = v + 12 - tier * 8;
            var result = new GeologyColumn(weight, level + 1 + (side > 5 ? (side - 5) / 2 : 0), TravertineBlock.Index, TravertineBlock.Index);

            if (row >= 1 && row <= 6 && side <= TerraceHalfWidths[row])
            {
                result.Surface = level - (side <= 2 ? 2 : 1);
                result.Flood(MineralWaterBlock.Make(tier % 2 == 0 ? 1 : 3), level);
            }
            else if (side == 0 && tier < 2 && row == 7)
            {
                result.Surface = level - 1;
                result.Flood(MineralWaterBlock.Make(tier == 0 ? 1 : 3), level);
            }
            else if (tier > 0 && row == 0)
            {
                result.Surface = side == 0 ? level - 2 : level + 4;
                if (side == 0) result.Flood(MineralWaterBlock.Make(tier == 1 ? 3 : 1), level + 4, level);
            }
            return result;
        }

        private static GeologyColumn Painted(GeologySite site, int u, int v, float weight)
        {
            float a = (float)Math.Sin(u * .17 + Math.Sin(v * .09) * .8);
            float b = (float)Math.Cos(v * .12 - u * .07);
            int height = Math.Max(site.Sea + 1, site.Base + 2 + (int)Math.Round(11 * a + 6 * b));
            return new GeologyColumn(weight, height, PaintedStrataBlock.Index, PaintedStrataBlock.Index)
            {
                ColorBand = 64 + u / 2 - v / 5 + (int)Math.Round(3 * Math.Sin(v * .11))
            };
        }

        private static GeologyColumn Coast(GeologySite site, int u, int v, float weight)
        {
            int side = Math.Abs(u);
            if (side > 16 || v < -13 || v > 16) return default;
            weight *= Edge((17f - side) / 5) * Edge((v + 14f) / 4) * Edge((17f - v) / 5);
            int sea = site.Sea;
            int shelf = sea - 2 + Math.Max(0, v) / 2;
            bool stone = v >= 1 && v <= 13 && side <= 12;
            int riser = stone ? 2 + GeologySites.Grain(GeologySites.FloorDiv(u + (v / 2 & 1), 2), v / 3, site.Seed, 9) : 0;
            var result = new GeologyColumn(weight, shelf + riser, stone ? BasaltPillarBlock.Index : VolcanicSandBlock.Index, BasaltPillarBlock.Index);
            result.Flood(Terrain.MakeBlockValue(WaterBlock.Index), sea);
            int center = (int)Math.Round(Math.Sin(v * .26));
            if (v >= -7 && v <= 8 && Math.Abs(u - center) <= 2)
            {
                int floor = sea - 2;
                if (v <= 1) result.Surface = floor;
                else
                {
                    result.Surface = Math.Max(result.Surface, sea + 6);
                    result.Tunnel(floor, sea + 4, BasaltPillarBlock.Index);
                }
            }
            return result;
        }

        private static GeologyColumn StoneForest(GeologySite site, int u, int v, float weight)
        {
            int gx = GeologySites.FloorDiv(u + 3, 6), gz = GeologySites.FloorDiv(v + 3, 6);
            int x = gx * 6 + GeologySites.Grain(gx, gz, site.Seed, 3) - 1;
            int z = gz * 6 + GeologySites.Grain(gz, gx, site.Seed + 3, 3) - 1;
            int distance = Math.Abs(u - x) + Math.Abs(v - z);
            int height = distance < 3 ? Math.Max(0, 12 + GeologySites.Grain(gx, gz, site.Seed + 7, 12) - distance * 7) : 0;
            int trail = (int)Math.Round(2.3 * Math.Sin(v * .16));
            if (Math.Abs(u - trail) <= 1) height = 0;
            int floor = Math.Max(site.Sea + 1, site.Base - 2 + (int)Math.Round(Math.Sin(v * .1) * 1.7));
            return new GeologyColumn(weight, floor + height, KarstLimestoneBlock.Index, KarstLimestoneBlock.Index);
        }

        private static GeologyColumn BasaltPlateau(GeologySite site, int u, int v, float weight)
        {
            int row = GeologySites.FloorDiv(v, 4);
            int column = GeologySites.FloorDiv(u + (row & 1) * 2, 4);
            int rise = 5 + GeologySites.Grain(column, row, site.Seed, 10);
            int gully = (int)Math.Round(3 * Math.Sin(v * .19));
            float shoulder = Edge((Math.Abs(u - gully) - 1f) / 6);
            int height = site.Base - 2 + (int)Math.Round(rise * shoulder);
            return new GeologyColumn(weight, height,
                shoulder < .25f ? VolcanicSandBlock.Index : BasaltPillarBlock.Index, BasaltPillarBlock.Index);
        }

        private static GeologyColumn Canyon(GeologySite site, int u, int v, float weight)
        {
            int bend = (int)Math.Round(4 * Math.Sin(v * .14) + 2 * Math.Sin(v * .3));
            int side = Math.Abs(u - bend);
            if (side > 16) return default;
            weight *= Edge((17f - side) / 5);
            int floor = Math.Max(site.Sea + 2, site.Base - 10 + (int)Math.Round(Math.Sin(v * .15)));
            if (side <= 2) return new GeologyColumn(weight, floor, GravelBlock.Index, SandstoneBlock.Index);
            int shelf = side <= 5 ? 3 : side <= 8 ? 6 : side <= 11 ? 9 : 12;
            return new GeologyColumn(weight, floor + shelf, PaintedStrataBlock.Index, PaintedStrataBlock.Index)
            {
                ColorBand = 64 + (int)Math.Round(2 * Math.Sin(v * .09))
            };
        }

        private static GeologyColumn Cavern(GeologySite site, int u, int v, float weight)
        {
            bool ice = site.Kind == IceCave;
            int length = ice ? 17 : 23;
            int along = Math.Abs(v);
            int center = (int)Math.Round((ice ? 1.7 : 2.8) * Math.Sin(v * .16));
            int side = Math.Abs(u - center);
            int width = ice ? 4 : 6;
            if (along > length + 4 || side > width + 6) return default;
            weight *= Edge((length + 5f - along) / 5) * Edge((width + 7f - side) / 5);
            int rock = ice ? GraniteBlock.Index : KarstLimestoneBlock.Index;
            int lining = ice ? BlueIceBlock.Index : KarstLimestoneBlock.Index;
            int water = Math.Max(site.Sea, site.Base - 3);
            bool meltwater = ice && side == 0 && along < length - 2;
            int floor = ice ? water - (meltwater ? 1 : 0) : side < 3 ? water - 2 : water + (side > 4 ? 1 : 0);
            int roof = water + (ice ? 5 : 8) + (int)Math.Round(Math.Cos(v * .18) * 2);
            int ridge = Math.Max(water + 2, roof + 6 - Math.Max(0, side - width) * 2);
            var result = new GeologyColumn(weight, ridge, ice ? SnowBlock.Index : rock, rock);
            if (side > width)
            {
                if (ice && side <= width + 2 && along < length - 2) result.Rock = Terrain.MakeBlockValue(lining);
                return result;
            }

            if (along >= length - 2)
            {
                // Open the roof as the river reaches a bank at each end of the ridge.
                if (along >= length + 1) floor = water + 1 + (along - length - 1) / 2;
                result.Surface = floor;
                result.Cap = Terrain.MakeBlockValue(lining);
            }
            else result.Tunnel(floor, roof, lining);

            if (meltwater || (!ice && side < 3 && floor < water)) result.Flood(Terrain.MakeBlockValue(WaterBlock.Index), water);
            return result;
        }

        private static GeologyColumn SaltBasin(GeologySite site, int u, int v, float radius, float weight)
        {
            int flat = site.Base - 1;
            float island = (float)Math.Sqrt((u + 3) * (u + 3) + (v - 2) * (v - 2));
            int relief = Math.Max(0, 5 - (int)island);
            int cap = relief > 0 ? KarstLimestoneBlock.Index : SaltCrustBlock.Index;
            var result = new GeologyColumn(weight, flat + relief + (radius > 23 ? (int)(radius - 23) / 3 : 0), cap, KarstLimestoneBlock.Index);
            int brine = (u - 9) * (u - 9) + (v + 9) * (v + 9);
            if (brine <= 11)
            {
                result.Surface = flat - 1;
                result.Flood(MineralWaterBlock.Make(0), flat);
            }
            return result;
        }

        internal static float Edge(float value)
        {
            float t = Math.Max(0, Math.Min(1, value));
            return t * t * (3 - 2 * t);
        }

        internal static int Mix(int a, int b, float weight) => a + (int)Math.Round((b - a) * weight);
    }
}
