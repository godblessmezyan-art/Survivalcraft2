using System.Collections.Generic;
using BuilderAssistant.Core.Blueprints;
using BuilderAssistant.Core.Safety;

namespace BuilderAssistant.Core.Building
{
    /// <summary>
    /// Independent structural QC for a generated blueprint. It re-derives facts from the
    /// produced <see cref="Blueprint"/> (not from the components' intentions) and reports
    /// every problem it finds instead of throwing, so the composer can surface them and a
    /// test can prove each rule actually fires. This is the guard that keeps a parametric
    /// generator from quietly emitting a broken building.
    ///
    /// What it verifies (all cheap, all reliable):
    ///   - non-empty, within the 1024 / 64-per-axis safety caps;
    ///   - footprint equals the requested Width x Length (walls really reach the edges);
    ///   - no duplicate coordinates;
    ///   - every material key is a known catalog key AND its authored family matches;
    ///   - exactly one entry door, with solid support below, air headroom above, and an open
    ///     cell just inside it;
    ///   - each storey's perimeter wall ring is unbroken at mid-height (a missing wall is a
    ///     hole in the shell);
    ///   - the interior is a real hollow volume, and the cell inside the door can reach a
    ///     substantial connected room by horizontal flood fill (partitions never seal the
    ///     entrance off);
    ///   - multi-storey: each floor boundary has an actual stair in the well and a carved
    ///     slab opening, so the floors are genuinely connected, not just stacked boxes;
    ///   - the whole structure is a SINGLE connected component (no floating/isolated blocks).
    ///
    /// Subtler failures that are impractical to auto-prove with certainty (a stair that is
    /// technically present but physically un-climbable in the live engine, exact furniture
    /// ergonomics) are instead pinned down by targeted negative TESTS below, per AGENTS.md -
    /// we never paper over them with an over-optimistic heuristic.
    /// </summary>
    internal sealed class BuildingValidator
    {
        private readonly BuildingSpec _spec;
        private readonly BuildingContext _ctx;
        private readonly Blueprint _bp;
        private readonly HashSet<CellPos> _occ;
        private readonly List<string> _problems = new List<string>();

        public BuildingValidator(BuildingSpec spec, BuildingContext ctx, Blueprint bp)
        {
            _spec = spec;
            _ctx = ctx;
            _bp = bp;
            _occ = new HashSet<CellPos>();
            foreach (BlueprintCell c in bp.Cells)
            {
                _occ.Add(c.Relative);
            }
        }

        public BuildingValidationResult Validate()
        {
            CheckNonEmptyAndBudget();
            CheckFootprint();
            CheckUniqueCoordinates();
            CheckMaterials();
            CheckEntrance();
            CheckPerimeterRings();
            CheckInteriorVolumeAndEntryReach();
            CheckStairConnections();
            CheckSingleConnectedComponent();

            return new BuildingValidationResult(_problems);
        }

        private void Problem(string message)
        {
            _problems.Add(message);
        }

        private bool IsSolid(int x, int y, int z)
        {
            return _occ.Contains(new CellPos(x, y, z));
        }

        private void CheckNonEmptyAndBudget()
        {
            if (_bp.CellCount == 0)
            {
                Problem("蓝图没有任何方块。");
                return;
            }
            if (_bp.CellCount > BuildSafetyLimits.MaxBlocksPerOperation)
            {
                Problem("方块数 " + _bp.CellCount + " 超过每事务上限 " + BuildSafetyLimits.MaxBlocksPerOperation + "。");
            }
            if (_bp.SizeX > BuildSafetyLimits.MaxSelectionAxisLength ||
                _bp.SizeY > BuildSafetyLimits.MaxSelectionAxisLength ||
                _bp.SizeZ > BuildSafetyLimits.MaxSelectionAxisLength)
            {
                Problem("尺寸 " + _bp.SizeX + "x" + _bp.SizeY + "x" + _bp.SizeZ + " 超过单轴上限。");
            }
        }

        private void CheckFootprint()
        {
            if (_bp.SizeX != _spec.Width)
            {
                Problem("实际占地宽 " + _bp.SizeX + " 不等于规格宽 " + _spec.Width + "。");
            }
            if (_bp.SizeZ != _spec.Length)
            {
                Problem("实际占地长 " + _bp.SizeZ + " 不等于规格长 " + _spec.Length + "。");
            }
        }

        private void CheckUniqueCoordinates()
        {
            if (_occ.Count != _bp.CellCount)
            {
                Problem("存在重复坐标：" + _bp.CellCount + " 格但只有 " + _occ.Count + " 个唯一坐标。");
            }
        }

        private void CheckMaterials()
        {
            MaterialCatalog.Entry e;
            foreach (BlueprintCell c in _bp.Cells)
            {
                if (!MaterialCatalog.TryGet(c.MaterialKey, out e))
                {
                    Problem("材料键 '" + c.MaterialKey + "' 不在 MaterialCatalog（无法解析）。");
                    continue;
                }
                if (e.Family != c.Family)
                {
                    Problem("材料 '" + c.MaterialKey + "' 在 " + c.Relative +
                            " 的族为 " + c.Family + "，但目录要求 " + e.Family + "。");
                }
            }
        }

        private void CheckEntrance()
        {
            if (!_ctx.HasDoor)
            {
                Problem("规格要求有入口门，但未生成门。");
                return;
            }
            int doors = 0;
            foreach (BlueprintCell c in _bp.Cells)
            {
                if (c.Family == BlockFamily.Door) doors++;
            }
            if (doors != 1)
            {
                Problem("只应有 1 个门底格（引擎自动生成上半），实际 " + doors + "。");
            }

            int dx = _ctx.DoorX, dy = _ctx.DoorY, dz = _ctx.DoorZ;
            if (!IsSolid(dx, dy - 1, dz))
            {
                Problem("门在 (" + dx + "," + dy + "," + dz + ") 下方无支撑块。");
            }
            if (IsSolid(dx, dy + 1, dz))
            {
                Problem("门上方 (" + dx + "," + (dy + 1) + "," + dz + ") 未留空，引擎无法生成门上半。");
            }
            // The cell you step into from the doorway must be open.
            int insideZ = dz == _ctx.Z0 ? dz + 1 : dz; // door is on the front wall
            if (IsSolid(dx, dy, insideZ))
            {
                Problem("门内侧 (" + dx + "," + dy + "," + insideZ + ") 被阻挡，入口不可通行。");
            }
        }

        private void CheckPerimeterRings()
        {
            for (int story = 0; story < _spec.Floors; story++)
            {
                int baseY = _ctx.WallBaseY(story);
                int topY = _ctx.WallTopY(story);
                int planeY = baseY + (_spec.WallHeight / 2);
                if (planeY < baseY) planeY = baseY;
                if (planeY > topY) planeY = topY;

                for (int z = _ctx.Z0; z <= _ctx.Z1; z++)
                {
                    AssertRingSolid(_ctx.X0, planeY, z, "左墙");
                    AssertRingSolid(_ctx.X1, planeY, z, "右墙");
                }
                for (int x = _ctx.X0; x <= _ctx.X1; x++)
                {
                    AssertRingSolid(x, planeY, _ctx.Z0, "前墙");
                    AssertRingSolid(x, planeY, _ctx.Z1, "后墙");
                }
            }
        }

        private void AssertRingSolid(int x, int y, int z, string which)
        {
            // The mid-height ring is checked at a course above the doorway (y>=3 for the
            // default storey height), so a genuine doorway is not mistaken for a hole.
            if (!IsSolid(x, y, z))
            {
                Problem(which + " 在 (" + x + "," + y + "," + z + ") 处出现缺口，外壳不完整。");
            }
        }

        private void CheckInteriorVolumeAndEntryReach()
        {
            for (int story = 0; story < _spec.Floors; story++)
            {
                int baseY = _ctx.WallBaseY(story);
                int topY = _ctx.WallTopY(story);
                int planeY = baseY + (_spec.WallHeight / 2);

                // Count interior (non-wall) columns that are open (air) at mid-height.
                int open = 0, total = 0;
                for (int z = _ctx.Z0 + 1; z <= _ctx.Z1 - 1; z++)
                {
                    for (int x = _ctx.X0 + 1; x <= _ctx.X1 - 1; x++)
                    {
                        total++;
                        if (!IsSolid(x, planeY, z)) open++;
                    }
                }
                if (total == 0)
                {
                    Problem("规格过小，楼层 " + story + " 无内部列。");
                    continue;
                }
                if (open < 3)
                {
                    Problem("楼层 " + story + " 内部几乎没有空气空间（open=" + open + "），疑似实心填充。");
                }

                // Flood fill from the cell just inside the entry to prove the entrance
                // reaches a substantial connected room on the ground floor.
                int reached = FloodFillGroundRoom();
                int threshold = System.Math.Max(3, total * 2 / 5);
                if (reached < threshold)
                {
                    Problem("入口可达连通空间过小（reached=" + reached + " < " + threshold +
                            "），房间被隔断封死。");
                }
            }
        }

        private int FloodFillGroundRoom()
        {
            if (!_ctx.HasDoor)
            {
                return 0;
            }
            int planeY = _ctx.WallBaseY(0) + (_spec.WallHeight / 2);
            int startZ = _ctx.Z0 + 1;
            // If the door column at mid-height is a window (solid), fall back to a nearby
            // open interior cell so the reachability test is about connectivity, not glazing.
            if (IsSolid(_ctx.DoorX, planeY, startZ))
            {
                bool found = false;
                for (int z = _ctx.Z0 + 1; z <= _ctx.Z1 - 1; z++)
                {
                    if (!IsSolid(_ctx.DoorX, planeY, z)) { startZ = z; found = true; break; }
                }
                if (!found)
                {
                    return 0;
                }
            }
            var start = new CellPos(_ctx.DoorX, planeY, startZ);

            var seen = new HashSet<CellPos> { start };
            var stack = new Stack<CellPos>();
            stack.Push(start);
            int[] dxs = { 1, -1, 0, 0 };
            int[] dzs = { 0, 0, 1, -1 };
            while (stack.Count > 0)
            {
                CellPos c = stack.Pop();
                for (int k = 0; k < 4; k++)
                {
                    int nx = c.X + dxs[k];
                    int nz = c.Z + dzs[k];
                    if (nx <= _ctx.X0 || nx >= _ctx.X1 || nz <= _ctx.Z0 || nz >= _ctx.Z1)
                    {
                        continue; // interior flood only
                    }
                    var np = new CellPos(nx, planeY, nz);
                    if (IsSolid(nx, planeY, nz) || seen.Contains(np))
                    {
                        continue;
                    }
                    seen.Add(np);
                    stack.Push(np);
                }
            }
            return seen.Count;
        }

        private void CheckStairConnections()
        {
            if (_spec.Floors < 2)
            {
                return;
            }
            if (!_ctx.HasStairwell)
            {
                Problem("多层建筑缺少楼梯井（Floors>=2 但没有连接）。");
                return;
            }

            for (int story = 0; story < _spec.Floors - 1; story++)
            {
                int bandLo = _ctx.WallBaseY(story);
                int slabNext = _spec.FloorSlabY(story + 1);

                bool hasStair = false;
                foreach (BlueprintCell c in _bp.Cells)
                {
                    if (c.Family == BlockFamily.Stairs &&
                        c.Relative.Y >= bandLo && c.Relative.Y <= slabNext &&
                        (c.Relative.X == _ctx.StairwellX || c.Relative.X == _ctx.StairwellX + 1))
                    {
                        hasStair = true;
                        break;
                    }
                }
                if (!hasStair)
                {
                    Problem("楼层 " + story + "->" + (story + 1) + " 楼梯井内无楼梯。");
                }

                // Require a carved opening in the destination slab so the two volumes connect.
                bool opening = false;
                for (int col = _ctx.StairwellX; col <= _ctx.StairwellX + 1 && !opening; col++)
                {
                    for (int z = _ctx.Z0 + 1; z <= _ctx.Z1 - 1 && !opening; z++)
                    {
                        if (!IsSolid(col, slabNext, z))
                        {
                            opening = true;
                        }
                    }
                }
                if (!opening)
                {
                    Problem("楼层 " + (story + 1) + " 楼板在楼梯井处未开洞，无法上下通行。");
                }
            }
        }

        private void CheckSingleConnectedComponent()
        {
            if (_bp.CellCount == 0)
            {
                return;
            }
            var seen = new HashSet<CellPos>();
            var start = _bp.Cells[0].Relative;
            var stack = new Stack<CellPos>();
            stack.Push(start);
            seen.Add(start);

            int[] dxs = { 1, -1, 0, 0, 0, 0 };
            int[] dys = { 0, 0, 1, -1, 0, 0 };
            int[] dzs = { 0, 0, 0, 0, 1, -1 };

            while (stack.Count > 0)
            {
                CellPos c = stack.Pop();
                for (int k = 0; k < 6; k++)
                {
                    var np = new CellPos(c.X + dxs[k], c.Y + dys[k], c.Z + dzs[k]);
                    if (!seen.Contains(np) && _occ.Contains(np))
                    {
                        seen.Add(np);
                        stack.Push(np);
                    }
                }
            }

            if (seen.Count != _occ.Count)
            {
                Problem("结构不连通：主块 " + seen.Count + "，另有 " + (_occ.Count - seen.Count) +
                        " 个孤立/悬空方块。");
            }
        }
    }

    /// <summary>Result of <see cref="BuildingValidator.Validate"/>.</summary>
    internal sealed class BuildingValidationResult
    {
        private readonly List<string> _problems;

        public BuildingValidationResult(List<string> problems)
        {
            _problems = problems ?? new List<string>();
        }

        public bool IsValid { get { return _problems.Count == 0; } }
        public IReadOnlyList<string> Problems { get { return _problems; } }

        public string JoinedProblems()
        {
            return string.Join("; ", _problems.ToArray());
        }
    }
}
