using System;
using Engine;
using Game;
using GameEntitySystem;
using SCIEPort.Features.Suspension;
using TemplatesDatabase;

namespace SCIEPort.Features.Shrines
{
    internal static class ShrineWorld
    {
        internal static bool OverlapsSavedChunks(SubsystemTerrain terrain, Point2 origin, int columns, int rows)
        {
            TerrainSerializer23.IStorage storage = terrain.TerrainSerializer?.m_storage;
            if (storage == null) return true;
            for (int z = 0; z < rows; z++)
            for (int x = 0; x < columns; x++)
                if (storage.Exists(new Point2((origin.X >> 4) + x, (origin.Y >> 4) + z))) return true;
            return false;
        }

        internal static bool Ready(SubsystemTerrain terrain, Point3 point)
        {
            TerrainChunk chunk = terrain.Terrain.GetChunkAtCell(point.X, point.Z);
            return chunk != null && chunk.State >= TerrainChunkState.InvalidLight;
        }

        internal static bool AllowsChange(Point3 point)
        {
            if (!SubsystemBedrockBlockBehavior.CheckIsInTerritoriy(point.X, point.Z, out Territoriy territory)) return true;
            return territory.AllowBlockBehavior &&
                (!territory.IsVisible || !SubsystemBedrockBlockBehavior.IsInTerritoriyBorder(territory, point.X, point.Z));
        }

        internal static bool CanReach(SubsystemTerrain terrain, ComponentPlayer player, Point3 point)
            => CanReach(terrain, player, point, new Vector3(.5f));

        internal static bool CanReach(SubsystemTerrain terrain, ComponentPlayer player, Point3 point, Vector3 offset)
        {
            Vector3 eye = player.ComponentCreatureModel.EyePosition;
            Vector3 target = new Vector3(point) + offset;
            if (!(Vector3.DistanceSquared(eye, target) <= 25f)) return false;
            TerrainRaycastResult? hit = terrain.Raycast(eye, target, true, true,
                (value, distance) => !BlocksManager.Blocks[Terrain.ExtractContents(value)].IsPlacementTransparent);
            return hit.HasValue && hit.Value.CellFace.Point == point;
        }

        internal static bool TryStock(SubsystemTerrain terrain, SubsystemBlockEntities entities, Point3 point, int seal, int reward)
        {
            if (!Ready(terrain, point) || !(BlocksManager.Blocks[SuspendedItemBlock.Index] is SuspendedItemBlock))
                return false;
            int current = Terrain.ReplaceLight(terrain.Terrain.GetCellValue(point.X, point.Y, point.Z), 0);
            if (current != 0 && current != Terrain.MakeBlockValue(SuspendedItemBlock.Index) && current != seal)
                return false;
            if (entities.GetBlockEntity(point.X, point.Y, point.Z) != null) return false;

            var project = terrain.Project;
            var database = project.GameDatabase;
            var template = database.Database.FindDatabaseObject("SCIEPortSuspendedItem", database.EntityTemplateType, true);
            var values = new ValuesDictionary();
            values.PopulateFromDatabaseObject(template);
            var blockEntity = values.GetValue<ValuesDictionary>("BlockEntity");
            blockEntity.SetValue("Coordinates", point);
            blockEntity.SetValue("Owner", Guid.Empty);
            Entity entity = project.CreateEntity(values);
            var inventory = entity.FindComponent<ComponentSuspendedItem>(true);
            // Initialize before AddEntity so the native creation packet contains the reward.
            if (!inventory.AddNetSlotItems(0, reward, 1))
            {
                entity.Dispose();
                return false;
            }
            terrain.ChangeCell(point.X, point.Y, point.Z, Terrain.MakeBlockValue(SuspendedItemBlock.Index));
            if (terrain.Terrain.GetCellContents(point.X, point.Y, point.Z) != SuspendedItemBlock.Index)
            {
                entity.Dispose();
                return false;
            }
            project.AddEntity(entity);
            return true;
        }
    }
}
