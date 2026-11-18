using System;
using Engine;
using Game;

namespace SCIEPort.Features.Fishing
{
    internal sealed class FishingWater
    {
        private static readonly Func<int, float, bool> s_hit = (value, distance) =>
        {
            Block block = BlocksManager.Blocks[Terrain.ExtractContents(value)];
            return block.IsCollidable || block is FluidBlock;
        };

        private readonly SubsystemTerrain m_subsystem;
        private readonly Terrain m_terrain;
        private readonly Point3[] m_columns = new Point3[FishingRules.RequiredWaterColumns];

        internal FishingWater(SubsystemTerrain subsystem)
        {
            m_subsystem = subsystem;
            m_terrain = subsystem.Terrain;
        }

        internal FishingCastArc Trace(Vector3 eye, FishingCastArc arc, out Point3? water)
        {
            const int steps = 50;
            const float step = FishingCastArc.MaxDuration / steps;
            water = null;
            if (m_subsystem.Raycast(eye, arc.Origin, false, true, s_hit).HasValue)
            {
                arc.Origin = eye;
            }

            Vector3 previous = arc.Origin;
            for (int i = 1; i <= steps; i++)
            {
                float time = i * step;
                Vector3 next = arc.At(time);
                TerrainRaycastResult? hit = m_subsystem.Raycast(previous, next, false, true, s_hit);
                if (hit.HasValue)
                {
                    TerrainRaycastResult contact = hit.Value;
                    float fraction = MathUtils.Saturate(contact.Distance / Vector3.Distance(previous, next));
                    arc.Duration = time - step * (1f - fraction);
                    Point3 cell = contact.CellFace.Point;
                    if (contact.CellFace.Face == 4 && Terrain.ExtractContents(contact.Value) == WaterBlock.Index &&
                        Vector3.DistanceSquared(eye, arc.End) <= FishingRules.MaxCastDistance * FishingRules.MaxCastDistance &&
                        IsFishable(cell))
                        water = cell;
                    return arc;
                }

                if (Vector3.DistanceSquared(eye, next) >= FishingRules.MaxCastDistance * FishingRules.MaxCastDistance)
                {
                    arc.Duration = time;
                    return arc;
                }
                previous = next;
            }
            return arc;
        }

        internal Vector3 Surface(Point3 cell, Vector3? landing = null)
        {
            int value = m_terrain.GetCellValue(cell.X, cell.Y, cell.Z);
            float height = Terrain.ExtractContents(value) == WaterBlock.Index ? Height(value) : 0.875f;
            return new Vector3(landing.HasValue ? MathUtils.Clamp(landing.Value.X, cell.X + 0.01f, cell.X + 0.99f) : cell.X + 0.5f,
                cell.Y + height, landing.HasValue ? MathUtils.Clamp(landing.Value.Z, cell.Z + 0.01f, cell.Z + 0.99f) : cell.Z + 0.5f);
        }

        internal bool IsFishable(Point3 cell) => IsDeep(cell) && HasArea(cell);

        internal bool IsDeep(Point3 cell)
        {
            return cell.Y > 0 && IsSurface(cell) &&
                m_terrain.GetCellContents(cell.X, cell.Y - 1, cell.Z) == WaterBlock.Index;
        }

        private bool IsSurface(Point3 cell)
        {
            if (!m_terrain.IsCellValid(cell.X, cell.Y, cell.Z)) return false;
            int value = m_terrain.GetCellValue(cell.X, cell.Y, cell.Z);
            return Terrain.ExtractContents(value) == WaterBlock.Index && Height(value) >= 0.5f &&
                m_terrain.GetCellContents(cell.X, cell.Y + 1, cell.Z) == 0;
        }

        private bool HasArea(Point3 cell)
        {
            int count = 1;
            m_columns[0] = cell;
            for (int i = 0; i < count && count < m_columns.Length; i++)
            {
                Point3 column = m_columns[i];
                AddColumn(new Point3(column.X - 1, column.Y, column.Z), ref count);
                AddColumn(new Point3(column.X + 1, column.Y, column.Z), ref count);
                AddColumn(new Point3(column.X, column.Y, column.Z - 1), ref count);
                AddColumn(new Point3(column.X, column.Y, column.Z + 1), ref count);
            }
            return count == m_columns.Length;
        }

        private void AddColumn(Point3 cell, ref int count)
        {
            if (count == m_columns.Length || !IsSurface(cell)) return;
            for (int i = 0; i < count; i++)
                if (m_columns[i] == cell) return;
            m_columns[count++] = cell;
        }

        private static float Height(int value)
        {
            var water = (FluidBlock)BlocksManager.Blocks[WaterBlock.Index];
            return water.GetLevelHeight(FluidBlock.GetLevel(Terrain.ExtractData(value)));
        }
    }
}
