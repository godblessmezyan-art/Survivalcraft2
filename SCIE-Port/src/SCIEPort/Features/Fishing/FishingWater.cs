using Engine;
using Game;

namespace SCIEPort.Features.Fishing
{
    internal sealed class FishingWater
    {
        private readonly Terrain m_terrain;
        private readonly Point3[] m_columns = new Point3[FishingRules.RequiredWaterColumns];

        internal FishingWater(Terrain terrain) => m_terrain = terrain;

        internal bool TryCast(Vector3 origin, Vector3 direction, float distance, out Point3 cell)
        {
            cell = default(Point3);
            float length = direction.Length();
            if (float.IsNaN(length) || float.IsInfinity(length) || length < 0.001f) return false;
            Vector3 step = direction * (0.125f / length);
            Vector3 point = origin;
            for (int i = 0; i <= distance * 8f; i++, point += step)
            {
                int x = Terrain.ToCell(point.X);
                int y = Terrain.ToCell(point.Y);
                int z = Terrain.ToCell(point.Z);
                if (!m_terrain.IsCellValid(x, y, z)) return false;
                int value = m_terrain.GetCellValue(x, y, z);
                int contents = Terrain.ExtractContents(value);
                if (contents == 0) continue;
                if (contents != WaterBlock.Index) return false;
                float surface = Height(value);
                if (point.Y > y + surface + 0.005f) continue;
                cell = new Point3(x, y, z);
                return IsDeep(cell) && HasArea(cell);
            }
            return false;
        }

        internal Vector3 Surface(Point3 cell)
        {
            int value = m_terrain.GetCellValue(cell.X, cell.Y, cell.Z);
            float height = Terrain.ExtractContents(value) == WaterBlock.Index ? Height(value) : 0.875f;
            return new Vector3(cell.X + 0.5f, cell.Y + height, cell.Z + 0.5f);
        }

        internal bool ValidateTarget(Vector3 eye, Point3 cell)
        {
            if (!IsDeep(cell) || !HasArea(cell)) return false;
            Vector3 delta = Surface(cell) + new Vector3(0, 0.02f, 0) - eye;
            float distance = delta.Length();
            if (!(distance <= FishingRules.MaxCastDistance + 0.6f) || distance < 0.001f) return false;

            Vector3 step = delta * (0.125f / distance);
            Vector3 point = eye;
            for (int i = 0; i <= distance * 8f; i++, point += step)
            {
                int x = Terrain.ToCell(point.X);
                int y = Terrain.ToCell(point.Y);
                int z = Terrain.ToCell(point.Z);
                if (!m_terrain.IsCellValid(x, y, z)) return false;
                int value = m_terrain.GetCellValue(x, y, z);
                int contents = Terrain.ExtractContents(value);
                if (contents != 0 && (contents != WaterBlock.Index || point.Y < y + Height(value))) return false;
            }
            return true;
        }

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
