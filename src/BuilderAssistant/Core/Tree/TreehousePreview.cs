using System.Collections.Generic;
using System.Text;
using BuilderAssistant.Core.Blueprints;

namespace BuilderAssistant.Core.Tree
{
    /// <summary>
    /// Pure offline visualization + export for the giant treehouse. Task §4 is explicit that
    /// "the blueprint is legal / all tests pass" is NOT aesthetic acceptance, so this renderer
    /// exists to actually LOOK at the result before touching a device:
    ///
    ///   - a front elevation (X by Y) and a side elevation (Z by Y) that show the distant
    ///     silhouette: the spreading trunk, the three shrinking storeys, the roof, the canopy;
    ///   - a top-down footprint (X by Z);
    ///   - layered plan slices at each deck level (and one course above it) proving the rooms
    ///     are hollow, the trunk grows through them, stairs and doorways are placed;
    ///   - per-part JSON (reusing <see cref="BlueprintJson"/>) plus a manifest recording the
    ///     master offset each part must be stamped at.
    ///
    /// Everything is a plain string built from the master canvas - no game, no UI. It is
    /// written to the outputs folder for human review, and the exact same builder also feeds
    /// the offline test assertions.
    /// </summary>
    internal static class TreehousePreview
    {
        private static readonly Dictionary<string, char> SymbolByKey = BuildSymbols();

        // Draw priority: structural wood/stone beats foliage beats glass, so a leaf in front
        // of a post never hides the post in an elevation view.
        private static readonly Dictionary<char, int> Priority = BuildPriority();

        public static string RenderElevationFront(TreeCanvas canvas, TreehouseSpec spec)
        {
            return RenderProjection(canvas, spec, axisU: 0 /*X*/, axisV: 1 /*Y*/, label: "正面立面 Front elevation (X horizontal, Y up)");
        }

        public static string RenderElevationSide(TreeCanvas canvas, TreehouseSpec spec)
        {
            return RenderProjection(canvas, spec, axisU: 2 /*Z*/, axisV: 1 /*Y*/, label: "侧面立面 Side elevation (Z horizontal, Y up)");
        }

        public static string RenderTopDown(TreeCanvas canvas, TreehouseSpec spec)
        {
            return RenderProjection(canvas, spec, axisU: 0 /*X*/, axisV: 2 /*Z*/, label: "俯视 footprint (X horizontal, Z down)");
        }

        /// <summary>Layered floor plans at the three deck levels and the level just above.</summary>
        public static string RenderFloorPlans(TreeCanvas canvas, TreehouseSpec spec)
        {
            var sb = new StringBuilder();
            int[] levels =
            {
                spec.FloorOneDeckY, spec.FloorOneDeckY + 1,
                spec.FloorTwoDeckY, spec.FloorTwoDeckY + 1,
                spec.FloorThreeDeckY, spec.FloorThreeDeckY + 1,
            };
            foreach (int y in levels)
            {
                sb.Append("── 平面层 plan at Y=").Append(y).Append(" ──\n");
                sb.Append(RenderHorizontalSlice(canvas, spec, y));
                sb.Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>A single horizontal slice (X by Z) at the given Y.</summary>
        public static string RenderHorizontalSlice(TreeCanvas canvas, TreehouseSpec spec, int y)
        {
            int w = spec.Width, d = spec.Depth;
            char[] grid = new char[w * d];
            for (int i = 0; i < grid.Length; i++) grid[i] = '.';

            foreach (KeyValuePair<CellPos, PaintedCell> kv in canvas.Cells)
            {
                if (kv.Key.Y != y) continue;
                int x = kv.Key.X, z = kv.Key.Z;
                if (x < 0 || z < 0 || x >= w || z >= d) continue;
                Put(grid, w, d, x, z, SymbolOf(kv.Value.Key));
            }

            return FormatGrid(grid, w, d, "（每格：.空气 T原木 *树叶 W木板 /楼梯 o玻璃 =石 |栅栏 D门）");
        }

        private static string RenderProjection(TreeCanvas canvas, TreehouseSpec spec, int axisU, int axisV, string label)
        {
            int uMax = AxisSize(spec, axisU);
            int vMax = AxisSize(spec, axisV);
            char[] grid = new char[uMax * vMax];
            for (int i = 0; i < grid.Length; i++) grid[i] = '.';

            foreach (KeyValuePair<CellPos, PaintedCell> kv in canvas.Cells)
            {
                int u = Coord(kv.Key, axisU);
                int v = Coord(kv.Key, axisV);
                char sym = SymbolOf(kv.Value.Key);
                int idx = (vMax - 1 - v) * uMax + u; // v axis drawn top-down (high Y at top)
                char cur = grid[idx];
                if (cur == '.' || Rank(sym) > Rank(cur))
                {
                    grid[idx] = sym;
                }
            }

            var sb = new StringBuilder();
            sb.Append("=== ").Append(label).Append(" ===\n");
            sb.Append(FormatGridRaw(grid, uMax, vMax));
            return sb.ToString();
        }

        private static void Put(char[] grid, int w, int d, int x, int z, char sym)
        {
            int idx = z * w + x;
            char cur = grid[idx];
            if (cur == '.' || cur == ' ')
            {
                grid[idx] = sym;
            }
            else if (Rank(sym) > Rank(cur))
            {
                grid[idx] = sym;
            }
        }

        private static string FormatGrid(char[] grid, int w, int d, string legend)
        {
            var sb = new StringBuilder();
            sb.Append(legend).Append('\n');
            sb.Append(FormatGridRaw(grid, w, d));
            return sb.ToString();
        }

        private static string FormatGridRaw(char[] grid, int w, int d)
        {
            var sb = new StringBuilder();
            for (int r = 0; r < d; r++)
            {
                for (int cc = 0; cc < w; cc++)
                {
                    sb.Append(grid[r * w + cc]);
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }

        public static string SerializePart(TreehousePart part)
        {
            return BlueprintJson.Serialize(part.Blueprint);
        }

        /// <summary>The phased assembly plan: build order, per-part cell counts, sizes, and
        /// the master offset each part is stamped at (anchor + offset + cell).</summary>
        public static string BuildManifest(TreehouseSpec spec, TreehousePart[] parts, TreehouseValidation validation)
        {
            var sb = new StringBuilder();
            sb.Append("森之王庭 Treehouse build manifest\n");
            sb.Append("spec: ").Append(spec.Width).Append('x').Append(spec.Height).Append('x').Append(spec.Depth)
              .Append(" seed=").Append(spec.Seed).Append('\n');
            int total = 0;
            foreach (TreehousePart p in parts)
            {
                total += p.CellCount;
                sb.Append("  part '").Append(p.Phase).Append("' cells=").Append(p.CellCount)
                  .Append(" size=").Append(p.Blueprint.SizeX).Append('x').Append(p.Blueprint.SizeY).Append('x').Append(p.Blueprint.SizeZ)
                  .Append(" offset=(").Append(p.OffsetInMaster.X).Append(',').Append(p.OffsetInMaster.Y).Append(',').Append(p.OffsetInMaster.Z).Append(")\n");
            }
            sb.Append("total blocks=").Append(total).Append(" parts=").Append(parts.Length).Append('\n');
            sb.Append(validation.IsValid ? "VALIDATION: OK" : ("VALIDATION PROBLEMS: " + validation.JoinedProblems()));
            sb.Append('\n');
            return sb.ToString();
        }

        // ---------------- helpers ----------------

        private static int AxisSize(TreehouseSpec spec, int axis)
        {
            return axis == 0 ? spec.Width : (axis == 1 ? spec.Height : spec.Depth);
        }

        private static int Coord(CellPos p, int axis)
        {
            return axis == 0 ? p.X : (axis == 1 ? p.Y : p.Z);
        }

        private static char SymbolOf(string key)
        {
            char c;
            if (key != null && SymbolByKey.TryGetValue(key, out c)) return c;
            return '?';
        }

        private static int Rank(char sym)
        {
            int r;
            return Priority.TryGetValue(sym, out r) ? r : 1;
        }

        private static Dictionary<string, char> BuildSymbols()
        {
            return new Dictionary<string, char>(System.StringComparer.Ordinal)
            {
                { BuildingMaterials.LogTrunk,  'T' },
                { BuildingMaterials.LogBranch, 'T' },
                { BuildingMaterials.Leaves,    '*' },
                { BuildingMaterials.WoodWall,  'W' },
                { BuildingMaterials.WoodTrim,  '#' },
                { BuildingMaterials.InteriorWall, 'W' },
                { BuildingMaterials.SupportBeam,  '#' },
                { BuildingMaterials.StoneWall, '=' },
                { BuildingMaterials.StoneBase, '=' },
                { BuildingMaterials.Glass,     'o' },
                { BuildingMaterials.InteriorStairs, '/' },
                { BuildingMaterials.RoofStairs, '/' },
                { BuildingMaterials.FlatDeck,  '_' },
                { BuildingMaterials.WindowSill, '_' },
                { BuildingMaterials.GardenFence, '|' },
                { BuildingMaterials.StoneRailing, '|' },
                { BuildingMaterials.EntryDoor, 'D' },
            };
        }

        private static Dictionary<char, int> BuildPriority()
        {
            return new Dictionary<char, int>
            {
                { 'T', 5 }, // trunk/branch always visible through the build
                { 'D', 4 },
                { '/', 4 },
                { '#', 4 },
                { '=', 4 },
                { 'W', 3 },
                { '_', 3 },
                { '|', 3 },
                { 'o', 2 },
                { '*', 1 }, // foliage is see-through for the frame
            };
        }
    }
}
