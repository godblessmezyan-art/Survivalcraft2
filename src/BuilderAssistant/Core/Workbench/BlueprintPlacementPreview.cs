using System.Collections.Generic;
using BuilderAssistant.Core.Blueprints;
using BuilderAssistant.Core.BuildEngine;
using BuilderAssistant.Core.Transactions;

namespace BuilderAssistant.Core.Workbench
{
    /// <summary>A read-only placement inspection. Rendering is capped; planning is always complete.</summary>
    internal sealed class BlueprintPlacementPreview
    {
        public const int MaxMarkers = 128;
        public readonly BuildPlan Plan;
        public readonly Dictionary<CellPos, int> Snapshot = new Dictionary<CellPos, int>();
        public readonly List<CellPos> Markers = new List<CellPos>();
        public int OccupiedCount { get; private set; }
        public int UnreadableCount { get; private set; }
        public int EnvelopeHeight { get; private set; }
        private BlueprintPlacementPreview(BuildPlan plan) { Plan = plan; }
        public bool Ready { get { return Plan.Success && UnreadableCount == 0; } }

        public static BlueprintPlacementPreview Inspect(Blueprint bp, CellPos origin,
            IBlockValueResolver resolver, ITerrainGateway terrain, bool clear)
        {
            var p = new BlueprintPlacementPreview(BuildPlanner.Plan(bp, origin, resolver, terrain, clear, false));
            p.EnvelopeHeight = HeightOf(bp);
            foreach (BlueprintCell cell in bp.Cells)
            {
                p.Read(origin.Add(cell.Relative), terrain, true);
                if (cell.Family == BlockFamily.Door) p.Read(origin.Add(cell.Relative).Add(new CellPos(0, 1, 0)), terrain, true);
            }
            foreach (CellPos relative in bp.FurnitureFootprint)
            {
                CellPos pos = origin.Add(relative);
                if (!p.Snapshot.ContainsKey(pos)) p.Read(pos, terrain, false);
            }
            return p;
        }

        public static int HeightOf(Blueprint bp)
        {
            int height = bp.SizeY;
            foreach (BlueprintCell c in bp.Cells)
                if (c.Family == BlockFamily.Door) height = System.Math.Max(height, c.Relative.Y + 2);
            return height;
        }

        private void Read(CellPos pos, ITerrainGateway terrain, bool writeTarget)
        {
            int value = 0;
            bool readable = terrain.InspectCell(pos).IsEditable && terrain.TryGetCellValue(pos, out value);
            if (!readable) { UnreadableCount++; Mark(pos); return; }
            Snapshot[pos] = value;
            if (!terrain.IsCellAir(pos))
            {
                if (writeTarget) OccupiedCount++;
                Mark(pos);
            }
        }
        private void Mark(CellPos pos) { if (Markers.Count < MaxMarkers) Markers.Add(pos); }

        public bool StillMatches(ITerrainGateway terrain, System.Func<int, int, bool> equivalent)
        {
            if (!Ready) return false;
            foreach (var pair in Snapshot)
            {
                int now;
                if (!terrain.InspectCell(pair.Key).IsEditable || !terrain.TryGetCellValue(pair.Key, out now) ||
                    !equivalent(pair.Value, now)) return false;
            }
            return true;
        }
    }
}
