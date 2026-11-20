using System;
using Engine;
using Game;

namespace SCIEPort.Features.Geology
{
    internal static partial class GeologyProfiles
    {
        internal const int LagoonPatchRadius = 6;

        private static GeologyColumn BridgeGorge(GeologySite site, int u, int v, float weight)
        {
            int bend = 2 + site.Seed % 5;
            int side = Math.Abs(u - (int)Math.Round(bend * Math.Sin(v * .12)));
            if (side > 16 || Math.Abs(v) >= 33) return default;
            float bank = Edge((17f - side) / 5);
            float ends = Edge((33f - Math.Abs(v)) / 8);
            bool basin = Math.Abs(Math.Abs(v) - 9) <= 4;
            int floor = site.Base - 12 + Math.Max(0, Math.Abs(v) - 15) * 2 / 3;
            int halfWidth = basin ? 6 : 3;
            int height = Mix(floor, site.Base + 4, Edge((side - halfWidth) / 8f));
            var result = new GeologyColumn(weight * bank * ends, height,
                side <= halfWidth ? GravelBlock.Index : KarstLimestoneBlock.Index, KarstLimestoneBlock.Index);
            int span = Math.Abs(v + 18) <= 1 ? 0 : Math.Abs(v) <= 2 ? 1 : Math.Abs(v - 18) <= 1 ? 2 : -1;
            if (span < 0 || side > 9) return result;
            int arch = floor + (span == 1 ? 10 : 8) - side * side / 10;
            result.Surface = Math.Max(height, site.Base + (span == 1 ? 5 : 3));
            result.Cap = Terrain.MakeBlockValue(KarstLimestoneBlock.Index);
            if (side <= 6 && arch > height + 2)
                result.Tunnel(height, arch, KarstLimestoneBlock.Index);
            return result;
        }

        private static bool LagoonOffset(GeologySite site, int u, int v, out Point2 offset)
        {
            int distance = Math.Abs(u + 16) + Math.Abs(v);
            offset = new Point2(u + 16, v);
            int east = Math.Abs(u - 16) + Math.Abs(v);
            if (east < distance)
            {
                distance = east;
                offset = new Point2(u - 16, v);
            }
            if ((site.Seed & 1) == 0 && Math.Abs(u) + Math.Abs(v - 16) < distance)
                offset = new Point2(u, v - 16);
            return Math.Abs(offset.X) <= LagoonPatchRadius && Math.Abs(offset.Y) <= LagoonPatchRadius;
        }

        internal static bool IsLagoonPatch(GeologySite site, int x, int z)
        {
            int dx = x - site.X, dz = z - site.Z;
            int u = dz * site.Facing.X - dx * site.Facing.Y;
            int v = dx * site.Facing.X + dz * site.Facing.Y;
            return LagoonOffset(site, u, v, out _);
        }

        private static GeologyColumn Dunes(GeologySite site, int u, int v, float weight)
        {
            int level = Math.Max(site.Sea + 2, site.Base);
            float curve = u + .014f * v * v + 1.2f * (float)Math.Sin(v * .17);
            int crest = (int)Math.Round(7 * Math.Pow((1 + Math.Cos(curve * .49)) / 2, 2));
            var result = new GeologyColumn(weight, level + crest, SandBlock.Index, SandstoneBlock.Index);
            if (!LagoonOffset(site, u, v, out Point2 offset)) return result;
            int skew = (site.Seed % 5) - 2;
            int cross = offset.X + skew * offset.Y / 4;
            int distance = 2 * cross * cross + offset.Y * offset.Y;
            result.Weight = 1;
            if (distance <= 25)
            {
                result.Surface = level - (distance <= 9 ? 3 : 1);
                result.Flood(MineralWaterBlock.Make(1), level);
            }
            else result.Surface = Math.Max(level + 1, result.Surface);
            return result;
        }

        private static GeologyColumn Reef(GeologySite site, int u, int v, float radius, float weight)
        {
            int throat = 8 + site.Seed % 3;
            float descent = Edge((throat + 2f - radius) / 4);
            int bottom = site.Sea - 8 - (int)Math.Round(19 * descent);
            bool passage = Math.Abs(u - (int)Math.Round(2 * Math.Sin(v * .18))) <= 2 && v < 0;
            float reef = Edge((radius - (throat + 2)) / 5f) * Edge((22 - radius) / 6);
            int shelf = site.Sea - 9 + (int)Math.Round((passage ? 0 : 7) * reef);
            int height = radius <= throat + 2 ? bottom : shelf;
            int cap = descent > .2f ? KarstLimestoneBlock.Index :
                reef > .2f && !passage ? TravertineBlock.Index : SandBlock.Index;
            var result = new GeologyColumn(weight, height, cap, KarstLimestoneBlock.Index);
            result.Flood(Terrain.MakeBlockValue(WaterBlock.Index), site.Sea);
            return result;
        }

        private static GeologyColumn Glacier(GeologySite site, int u, int v, float weight)
        {
            int bend = (int)Math.Round((3 + site.Seed % 4) * Math.Sin(v * .09));
            int side = Math.Abs(u - bend);
            if (side > 14 || Math.Abs(v) > 29) return default;
            float shoulder = Edge((15f - side) / 5);
            float ends = Edge((30f - Math.Abs(v)) / 6);
            int throat = 5 + (v + 29) / 14;
            int height = site.Base - 4 + (v + 29) / 4;
            int bank = (int)Math.Round(9 * Edge((side - throat) / 5f));
            bool ice = side <= throat;
            int ridge = Math.Abs(side - throat - 1) <= 1 ? 2 : 0;
            return new GeologyColumn(weight * shoulder * ends, height + bank + ridge,
                ice ? BlueIceBlock.Index : ridge > 0 ? GravelBlock.Index : GraniteBlock.Index,
                ice ? BlueIceBlock.Index : GraniteBlock.Index);
        }
    }
}
