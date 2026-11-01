using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;
using Game.NetWork;
using GameEntitySystem;
using TemplatesDatabase;

namespace SCIEPort.Features.Suspension
{
    /// <summary>Native chest storage with a bounded world-space item renderer; no terrain writes.</summary>
    public sealed class SubsystemSuspendedItem : SubsystemChestBlockBehavior, IDrawable
    {
        internal const int MaxVisible = 16;
        internal const float DrawDistance = 48;
        private readonly List<ComponentSuspendedItem> items = new List<ComponentSuspendedItem>();
        private readonly List<ComponentSuspendedItem> visible = new List<ComponentSuspendedItem>(MaxVisible);
        private readonly HashSet<int> failed = new HashSet<int>();
        private readonly PrimitivesRenderer3D renderer = new PrimitivesRenderer3D();
        private readonly DrawBlockEnvironmentData environment = new DrawBlockEnvironmentData { DrawBlockMode = DrawBlockMode.UI };
        private SubsystemTerrain terrain;
        private SubsystemTime clock;
        /// <inheritdoc/>
        public override int[] HandledBlocks => new[] { SuspendedItemBlock.Index };
        /// <inheritdoc/>
        public int[] DrawOrders { get; } = new[] { 102 };
        /// <inheritdoc/>
        public override void Load(ValuesDictionary values)
        {
            base.Load(values);
            terrain = Project.FindSubsystem<SubsystemTerrain>(true);
            clock = Project.FindSubsystem<SubsystemTime>(true);
            environment.SubsystemTerrain = terrain;
        }
        /// <inheritdoc/>
        public override void OnBlockAdded(int value, int oldValue, int x, int y, int z, ComponentMiner miner)
        {
            if (CommonLib.WorkType != WorkType.Client && miner?.ComponentPlayer != null && m_subsystemBlockEntities.GetBlockEntity(x, y, z) == null)
                m_subsystemBlockEntities.CreateBlockEntity("SCIEPortSuspendedItem", new Point3(x, y, z), miner);
        }
        /// <inheritdoc/>
        public override void OnBlockRemoved(int value, int newValue, int x, int y, int z)
        {
            if (CommonLib.WorkType != WorkType.Client) base.OnBlockRemoved(value, newValue, x, y, z);
            else
            {
                var entity = m_subsystemBlockEntities.GetBlockEntity(x, y, z);
                if (entity?.Entity.FindComponent<ComponentSuspendedItem>(false) != null) Project.RemoveEntity(entity.Entity, true);
            }
        }
        /// <inheritdoc/>
        public override void OnHitByProjectile(CellFace face, WorldItem item)
        {
            if (CommonLib.WorkType != WorkType.Client) base.OnHitByProjectile(face, item);
        }
        /// <inheritdoc/>
        public override bool OnInteract(TerrainRaycastResult hit, ComponentMiner miner)
        {
            bool result = base.OnInteract(hit, miner);
            AdaptGui(miner.ComponentPlayer?.ComponentGui);
            return result;
        }
        /// <inheritdoc/>
        public override void OnEntityAdded(Entity entity)
        {
            var item = entity.FindComponent<ComponentSuspendedItem>(false);
            if (item != null && !items.Contains(item)) items.Add(item);
        }
        /// <inheritdoc/>
        public override void OnEntityRemoved(Entity entity)
        {
            var item = entity.FindComponent<ComponentSuspendedItem>(false);
            if (item != null) items.Remove(item);
        }
        internal static Vector3 Center(Point3 p) => new Vector3(p.X + .5f, p.Y + .5f, p.Z + .5f);
        internal static Matrix DisplayMatrix(Point3 p, float seconds, Vector3 offset)
        {
            float phase = (p.X % 31 + p.Z % 29) * .17f;
            return Matrix.CreateTranslation(offset) * Matrix.CreateRotationY(seconds * MathUtils.PI / 15 + phase)
                * Matrix.CreateTranslation(Center(p) + Vector3.UnitY * (.065f * MathUtils.Sin(seconds * MathUtils.PI / 2 + phase)));
        }
        /// <inheritdoc/>
        public void Draw(Camera camera, int drawOrder)
        {
            visible.Clear();
            foreach (var item in items)
            {
                if (!item.IsAddedToProject) continue;
                var p = item.Entity.FindComponent<ComponentBlockEntity>(true).Coordinates;
                var chunk = terrain.Terrain.GetChunkAtCell(p.X, p.Z);
                if (chunk == null || chunk.State < TerrainChunkState.Valid || Terrain.ExtractContents(terrain.Terrain.GetCellValue(p.X, p.Y, p.Z)) != SuspendedItemBlock.Index) continue;
                float distance = Vector3.DistanceSquared(camera.ViewPosition, Center(p));
                if (distance > DrawDistance * DrawDistance || !camera.ViewFrustum.Intersection(new BoundingSphere(Center(p), 2))) continue;
                int at = 0;
                while (at < visible.Count && Vector3.DistanceSquared(camera.ViewPosition, Center(visible[at].Entity.FindComponent<ComponentBlockEntity>(true).Coordinates)) < distance) at++;
                if (at < MaxVisible) { visible.Insert(at, item); if (visible.Count > MaxVisible) visible.RemoveAt(MaxVisible); }
            }
            float time = (float)(clock.GameTime % 120);
            foreach (var item in visible)
            {
                var p = item.Entity.FindComponent<ComponentBlockEntity>(true).Coordinates;
                int value = item.GetSlotCount(0) > 0 ? item.GetSlotValue(0) : Terrain.MakeBlockValue(SuspendedItemBlock.Index);
                DrawItem(value, p, time, camera.ViewProjectionMatrix);
            }
        }
        internal void DrawItem(int value, Point3 p, float seconds, Matrix viewProjection)
        {
            int id = Terrain.ExtractContents(value);
            var block = BlocksManager.Blocks[id];
            if (id == 0 || block == null || failed.Contains(id)) return;
            try
            {
                environment.Light = Math.Max(8, Terrain.ExtractLight(terrain.Terrain.GetCellValue(p.X, p.Y, p.Z)));
                environment.ViewProjectionMatrix = null;
                float size = MathUtils.Clamp(block.GetIconViewScale(value, environment), .1f, 3f) * .9f;
                // Native icon composition scales both the item and its authored centering offset.
                var matrix = DisplayMatrix(p, seconds, block.GetIconBlockOffset(value, environment) * size);
                environment.InWorldMatrix = matrix;
                block.DrawBlock(renderer, value, Color.White, size, ref matrix, environment);
                // Separate items cannot wrap DrawMeshBlock's native ushort batch vertex offset.
                renderer.Flush(viewProjection);
            }
            catch (Exception error) { renderer.Clear(); failed.Add(id); Log.Warning("[SCIEPort:Suspension] Cannot render item " + id + ": " + error.Message); }
        }
        internal static void AdaptGui(ComponentGui gui)
        {
            var chest = gui?.ModalPanelWidget as ChestWidget;
            if (chest != null && !(chest is SuspendedItemWidget) && chest.m_componentChest is ComponentSuspendedItem)
                gui.ModalPanelWidget = new SuspendedItemWidget(gui.m_componentPlayer.ComponentMiner.Inventory, (ComponentSuspendedItem)chest.m_componentChest);
        }
        /// <inheritdoc/>
        public override void Dispose()
        {
            renderer.Clear(); items.Clear(); visible.Clear(); failed.Clear(); environment.SubsystemTerrain = null; base.Dispose();
        }
    }
}
