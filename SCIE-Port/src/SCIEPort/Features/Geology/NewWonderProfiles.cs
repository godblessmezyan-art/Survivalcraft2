using System;
using Game;

namespace SCIEPort.Features.Geology
{
    internal static partial class GeologyProfiles
    {
        private static GeologyColumn Fjord(GeologySite site, int u, int v, float weight)
        {
            int head = 17 + site.Seed % 6;
            if (v > head + 3 || v < -27) return default;
            int bend = (int)Math.Round((2 + site.Seed % 4) * Math.Sin((v + 8) * .13));
            int side = Math.Abs(u - bend);
            if (side >= 20) return default;
            int width = v >= head - 4 ? 6 - (v - (head - 4)) / 2 : 5;
            int bed = site.Sea - 10 + Math.Max(0, v - (head - 9));
            int cliff = site.Sea + 4 + Math.Max(0, v + 8) / 2;
            int height = Mix(bed, cliff, Edge((side - width) / 5f));
            float mouth = Edge((v + 27f) / 6);
            float end = Edge((head + 4f - v) / 5);
            float shore = Edge((20f - side) / 6);
            var result = new GeologyColumn(weight * mouth * end * shore, height,
                height <= site.Sea + 2 ? GravelBlock.Index : GraniteBlock.Index, GraniteBlock.Index);
            if (height < site.Sea) result.Flood(Terrain.MakeBlockValue(WaterBlock.Index), site.Sea);
            return result;
        }

        private static GeologyColumn Tabletop(GeologySite site, int u, int v, float weight)
        {
            float skew = u + .21f * v;
            float distance = (float)Math.Sqrt(skew * skew + .58f * v * v);
            float edge = Edge((21f - distance) / 6);
            if (edge == 0) return default;
            int height = site.Base + (int)Math.Round((24 + site.Seed % 5) * edge);
            if (v < -4)
            {
                float trail = Edge((4.6f - Math.Abs(u - (float)Math.Sin(v * .25) * 2f)) / 3);
                int grade = site.Base + Math.Max(0, 31 + v);
                height = Mix(height, Math.Min(height, grade), trail);
            }
            var result = new GeologyColumn(weight, height,
                edge >= .8f ? SandstoneBlock.Index : GravelBlock.Index, SandstoneBlock.Index);
            if (edge >= .18f && edge < .83f) result.ColorBand = 70 + v / 10;
            return result;
        }

        private static GeologyColumn ReefLanes(GeologySite site, int u, int v, float weight)
        {
            int bend = (int)Math.Round(3 * Math.Sin(v * .15));
            int side = Math.Abs(u - bend);
            if (side >= 20 || Math.Abs(v) >= 22) return default;
            // Three separated ridges leave two navigable cuts along their full length.
            int ridge = Math.Min(Math.Abs(side - 12), side);
            int breakAt = 4 + site.Seed % 7;
            float crest = Edge((4.5f - ridge) / 3.5f) * Edge((22f - Math.Abs(v)) / 8);
            if (Math.Abs(v - breakAt) <= 2 && side < 5) crest = 0;
            int height = site.Sea - 9 + (int)Math.Round(7 * crest);
            bool shell = crest > .72f && ((v + 24) / 5 & 1) == 0 && BlocksManager.Blocks[ShellLimestoneBlock.Index] is ShellLimestoneBlock;
            var result = new GeologyColumn(weight, height,
                shell ? ShellLimestoneBlock.Index : crest > .35f ? TravertineBlock.Index : SandBlock.Index, KarstLimestoneBlock.Index);
            result.Flood(Terrain.MakeBlockValue(WaterBlock.Index), site.Sea);
            return result;
        }

        private static GeologyColumn GlacialSteps(GeologySite site, int u, int v, float weight)
        {
            int taper = Math.Abs(u - (int)Math.Round(2 * Math.Sin(v * .17)));
            if (taper > 17 || Math.Abs(v) >= 28) return default;
            int width = 8 + (site.Seed % 3);
            int step = Math.Max(0, (v + 24) / 8) * 3;
            int grade = (v + 27) * 2 / 5;
            int rise = taper <= 2 ? grade : step;
            bool face = (v + 24) % 8 <= 1;
            bool ice = taper <= width && (face || taper <= 2);
            float shoulders = Edge((18f - taper) / 6);
            float ends = Edge((28f - Math.Abs(v)) / 6);
            int height = site.Base - 8 + rise;
            bool band = ice && face && taper > 2 && BlocksManager.Blocks[BandedGlacierIceBlock.Index] is BandedGlacierIceBlock;
            return new GeologyColumn(weight * shoulders * ends, height,
                band ? BandedGlacierIceBlock.Index : ice ? BlueIceBlock.Index : GraniteBlock.Index,
                face && ice ? BlueIceBlock.Index : GraniteBlock.Index);
        }
    }
}
