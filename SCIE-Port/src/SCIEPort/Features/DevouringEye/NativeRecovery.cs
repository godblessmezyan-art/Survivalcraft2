using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Engine;
using Game;
using Game.NetWork;
using GameEntitySystem;
using SCIEPort.Features.Tanks;
using TemplatesDatabase;

namespace SCIEPort.Features.DevouringEye
{
    internal sealed class NativeRecovery : IRecoverySteps
    {
        private readonly SubsystemDevouringEye m_runtime;
        private readonly ComponentPlayer m_player;
        private readonly RecoveryTarget m_target;
        private readonly string m_id = Guid.NewGuid().ToString("N");
        private readonly Dictionary<Point3, int> m_neighbors = new Dictionary<Point3, int>();
        private IInventory m_inventory;
        private int m_slot = -1, m_beforeCount, m_beforeValue, m_output;
        private bool m_creative;
        private string m_pending;

        internal NativeRecovery(SubsystemDevouringEye runtime, ComponentPlayer player, RecoveryTarget target)
        { m_runtime = runtime; m_player = player; m_target = target; }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private int Cell(Point3 p) => Terrain.ReplaceLight(m_runtime.TerrainSystem.Terrain.GetCellValue(p.X,p.Y,p.Z), 0);
        private static bool NativeBlock(Block block, int value) => block.GetType().Assembly == typeof(Block).Assembly && RecoveryRules.BlockMapped(block.GetType().Name, Terrain.ExtractData(value));

        public void Validate()
        {
            m_runtime.Authority(m_player);
            Require(m_player.Entity.IsAddedToProject && m_player.PlayerData.ComponentPlayer == m_player, "玩家已离开或重生。");
            Require(Terrain.ExtractContents(m_player.ComponentMiner.ActiveBlockValue) == DevouringEyeBlock.Index, "请继续手持吞噬之眼。");
            Require(m_target != null && !m_target.Moving, "没有目标或目标为移动方块结构。");
            Require(Vector3.DistanceSquared(m_player.ComponentBody.Position, m_target.ActorPosition) <= .25f, "玩家位置已变化，请重新识别。");
            Require(m_target.Same(m_runtime.Scan(m_player, m_target.Ray)), "前景遮挡或目标已变化，请重新识别。");
            if (m_target.Entity != null) ValidateTank(); else ValidateCell();
            Require(!m_runtime.Info.WorldSettings.IsBlockDiable(m_output), "返还物品被世界配置禁用。");
            m_inventory = m_player.ComponentMiner.Inventory;
            m_creative = m_inventory?.GetType() == typeof(ComponentCreativeInventory);
            Require(m_creative || m_inventory?.GetType() == typeof(ComponentInventory), "当前背包类型未验证，禁止修改。");
            int slots = m_creative ? ((ComponentCreativeInventory)m_inventory).OpenSlotsCount : m_inventory.SlotsCount;
            if (m_creative) slots = Math.Min(slots, m_inventory.VisibleSlotsCount);
            for (int i=0; i<slots; i++)
            {
                int count = m_inventory.GetSlotCount(i), value = m_inventory.GetSlotValue(i);
                if (RecoveryRules.CanFit(m_creative,count,value,m_output,m_inventory.GetSlotCapacity(i,m_output))) { m_slot = i; break; }
            }
            Require(m_slot >= 0, "背包已满：需要一个空位或可合并槽；不会在地面生成掉落。");
            m_beforeCount = m_inventory.GetSlotCount(m_slot); m_beforeValue = m_inventory.GetSlotValue(m_slot);
        }

        private void ValidateTank()
        {
            Entity entity = m_target.Entity;
            Require(entity.Project == m_runtime.Project && entity.IsAddedToProject, "目标已移除。");
            Require(entity.FindComponent<ComponentPlayer>(false) == null, "绝不回收玩家角色。");
            Require(entity.ValuesDictionary.DatabaseObject.Name == RecoveryRules.TankTemplate && entity.ValuesDictionary.DatabaseObject.Guid == RecoveryRules.TankGuid, "未知实体/非白名单：只允许 SCIEPortT34Tank。");
            Type[] allowed = { typeof(ComponentBody), typeof(ComponentSpawn), typeof(ComponentMount), typeof(ComponentSimpleModel), typeof(ComponentTankDrive) };
            Require(entity.Components.All(c => allowed.Contains(c.GetType())) && entity.Components.Count() == 5, "坦克含未验证附加组件/容器，拒绝拆解。");
            ComponentBody body = entity.FindComponent<ComponentBody>(true);
            ComponentSpawn spawn = entity.FindComponent<ComponentSpawn>(true);
            Require(!spawn.AutoDespawn && !spawn.IsDespawning, "目标不是已验证的持久化坦克状态。");
            Require(body.ParentBody == null && body.ChildBodies.Count == 0 && entity.FindComponents<ComponentMount>().All(m => m.Rider == null), "乘客或附属实体未离开。");
            Require(m_runtime.Project.Entities.All(e => !e.FindComponents<ComponentRider>().Any(r => r.Mount?.Entity == entity)), "检测到骑乘关系，拒绝回收。");
            Require(body.Velocity.LengthSquared() < .25f && entity.FindComponent<ComponentTankDrive>(true).CanRecover, "坦克仍在移动/射击或炮弹未结束。");
            BoundingBox box = body.BoundingBox;
            Require(box.Max.X-box.Min.X < 8 && box.Max.Z-box.Min.Z < 10, "坦克范围异常。");
            for (int x=Terrain.ToCell(box.Min.X); x<=Terrain.ToCell(box.Max.X); x++)
            for (int z=Terrain.ToCell(box.Min.Z); z<=Terrain.ToCell(box.Max.Z); z++)
            { m_runtime.CheckRegion(m_player,x,Terrain.ToCell(box.Min.Y),z); m_runtime.CheckRegion(m_player,x,Terrain.ToCell(box.Max.Y),z); }
            Require(Project.DetermineNotOwnedEntities(new[]{entity}).Count == 1, "实体含所有权依赖，无法无损拆解。");
            SubsystemSpawn system = m_runtime.Project.FindSubsystem<SubsystemSpawn>(true);
            Require(!system.m_chunks.Values.Any(c => c.SpawnsData.Any(s => s.TemplateName == RecoveryRules.TankTemplate && Vector3.DistanceSquared(s.Position,body.Position)<1)), "存在待重生记录，拒绝可能重复的回收。");
            m_output = Terrain.MakeBlockValue(TankBlock.Index);
        }

        private void ValidateCell()
        {
            Point3 p = m_target.Cell;
            m_runtime.CheckRegion(m_player,p.X,p.Y,p.Z);
            Require(Cell(p) == m_target.Value && Terrain.ExtractContents(m_target.Value) != 0, "方块已经变化。");
            Block block = BlocksManager.Blocks[Terrain.ExtractContents(m_target.Value)];
            Require(NativeBlock(block,m_target.Value) && !block.IsEditable && !block.IsInteractive(m_runtime.TerrainSystem,m_target.Value), "没有无损回收映射（容器、门、家具、多方块及未知 Mod 方块拒绝）。");
            Require(m_runtime.BlockEntities.GetBlockEntity(p.X,p.Y,p.Z) == null, "方块绑定了实体数据，禁止丢弃内容。");
            bool wood = block is WoodBlock;
            Require(m_runtime.Behaviors.GetBlockBehaviors(block.BlockIndex).All(b => wood && b.GetType() == typeof(SubsystemWoodBlockBehavior)), "存在未验证的方块移除行为。");
            int radius = wood ? 3 : 1;
            m_neighbors.Clear();
            for(int dx=-radius;dx<=radius;dx++) for(int dz=-radius;dz<=radius;dz++) for(int dy=-radius;dy<=radius;dy++)
            {
                Point3 q = new Point3(p.X+dx,p.Y+dy,p.Z+dz);
                Require(q.Y>=0 && q.Y<256, "邻域跨越世界边界。");
                TerrainChunk chunk=m_runtime.TerrainSystem.Terrain.GetChunkAtCell(q.X,q.Z);
                Require(chunk != null && chunk.State>TerrainChunkState.InvalidContents4, "邻域区块未就绪。");
                if (q == p) continue;
                int v=Cell(q), contents=Terrain.ExtractContents(v);
                // Avoid support-triggered drops, falling blocks, fluid flow, leaf decay and shape changes.
                Require(m_runtime.BlockEntities.GetBlockEntity(q.X,q.Y,q.Z)==null && m_runtime.Behaviors.GetBlockBehaviors(contents).Length==0, "邻近有容器/动态方块，可能连锁变化，V1 拒绝回收。");
                Require(contents==0 || NativeBlock(BlocksManager.Blocks[contents],v), "邻近特殊方块/未知结构，V1 拒绝回收。");
                m_neighbors.Add(q,v);
            }
            m_output = m_target.Value; // Exact valid DATA preserved; only transient light bits were removed.
        }

        public void Prepare()
        {
            string dir = Storage.CombinePaths(m_runtime.Info.DirectoryName,"DevouringEyeJournal");
            Storage.CreateDirectory(dir);
            Require(Storage.ListFileNames(dir).Count()<256, "回收日志已达 256 条，请备份归档后再操作。");
            var record = new XElement("Recovery",new XAttribute("Id",m_id),new XAttribute("Player",m_player.PlayerData.PlayerGUID),new XAttribute("Output",m_output),new XAttribute("Slot",m_slot),new XAttribute("BeforeSlotValue",m_beforeValue),new XAttribute("BeforeSlotCount",m_beforeCount));
            if (m_target.Entity != null)
            {
                var saved = new ValuesDictionary(); m_runtime.Project.SaveEntities(new[]{m_target.Entity}).Save(saved);
                record.Add(new XElement("EntitySnapshot",new XCData(saved.ToJsonText())));
            }
            else
            {
                record.Add(new XElement("Cell",new XAttribute("X",m_target.Cell.X),new XAttribute("Y",m_target.Cell.Y),new XAttribute("Z",m_target.Cell.Z),new XAttribute("Value",m_target.Value)));
                record.Add(new XElement("Neighbors",m_neighbors.Select(k => new XElement("Cell",new XAttribute("X",k.Key.X),new XAttribute("Y",k.Key.Y),new XAttribute("Z",k.Key.Z),new XAttribute("Value",k.Value)))));
            }
            m_pending=Storage.CombinePaths(dir,m_id+".pending");
            try
            {
                using(Stream stream=Storage.OpenFile(m_pending,OpenFileMode.Create))
                using(var writer=new StreamWriter(stream)) { writer.Write(record.ToString()); writer.Flush(); stream.Flush(); }
                // Engine snapshot includes both entity/inventory and Terrain.SaveChunk. Verify on-disk acknowledgement.
                SaveVerified(m_id+"-before");
            }
            catch { m_runtime.Transaction.Lock(); throw; }
            Log.Information("[SCIEPort:DevouringEye] PREPARED "+m_id+" source="+m_target.Name);
        }

        public void Remove()
        {
            m_runtime.Authority(m_player);
            Require(m_inventory.GetSlotValue(m_slot)==m_beforeValue && m_inventory.GetSlotCount(m_slot)==m_beforeCount,"保存检查点后背包已变化。");
            Require(m_target.Same(m_runtime.Scan(m_player,m_target.Ray)),"保存检查点后目标/遮挡已变化。");
            if (m_target.Entity != null)
            {
                ValidateTank(); // Re-check passengers and bounds immediately before the irreversible membership change.
                m_runtime.Project.RemoveEntity(m_target.Entity,false); // No death/Despawn/drop path. Keep alive until commit for rollback.
                Require(!m_target.Entity.IsAddedToProject && !m_runtime.Project.Entities.Contains(m_target.Entity), "实体删除没有完成。");
            }
            else
            {
                Point3 p=m_target.Cell;
                m_runtime.CheckRegion(m_player,p.X,p.Y,p.Z);
                Require(Cell(p)==m_target.Value && m_neighbors.All(k=>Cell(k.Key)==k.Value),"保存检查点后目标/邻域已变化。");
                m_runtime.TerrainSystem.ChangeCell(p.X,p.Y,p.Z,0,true,m_player.ComponentMiner);
                Require(Cell(p)==0, "原生权限/Hook 拒绝了方块修改。");
                Require(m_neighbors.All(k => Cell(k.Key)==k.Value), "邻近方块发生异常变化。");
            }
        }
        public void Grant()
        {
            Require(m_inventory.GetSlotCount(m_slot)==m_beforeCount && m_inventory.GetSlotValue(m_slot)==m_beforeValue, "背包槽已变化。");
            Require(m_inventory.AddNetSlotItems(m_slot,m_output,1), "背包拒绝接收物品。");
            Require(m_inventory.GetSlotValue(m_slot)==m_output && (m_creative || m_inventory.GetSlotCount(m_slot)==m_beforeCount+1), "背包返还验证失败。");
            m_inventory.OnSlotChange(m_slot);
        }
        private void SaveVerified(string token)
        {
            m_runtime.LastCommit=token;
            GameManager.SaveProject(true,true);
            string json=Storage.ReadAllText(Storage.CombinePaths(m_runtime.Info.DirectoryName,"Project.json"));
            byte[] parsed; var saved=new ValuesDictionary();saved.ApplyOverridesUseJson(json,out parsed);
            string acknowledgement=saved.GetValue<ValuesDictionary>("Subsystems").GetValue<ValuesDictionary>("SCIEPortDevouringEye").GetValue<string>("LastCommittedRecovery");
            Require(acknowledgement==token, "原生存档未确认本次事务。");
            if(m_target.Entity!=null && m_runtime.Project is ProjectNet)
            {
                bool persisted=saved.GetValue<ValuesDictionary>("Entities").Values.Cast<ValuesDictionary>().Any(v=>v.GetValue<int>("Id")==m_target.Entity.EntityId);
                Require(persisted==m_target.Entity.IsAddedToProject,"磁盘实体记录与移除/恢复状态不一致。");
            }
        }
        public void Commit()
        {
            SaveVerified(m_id+"-committed");
            Storage.MoveFile(m_pending,Storage.ChangeExtension(m_pending,".done"));
            if (m_target.Entity != null)
                try { m_target.Entity.Dispose(); } catch(Exception error) { Log.Warning("[SCIEPort:DevouringEye] Removed entity disposal: "+error); }
        }
        public void Rollback()
        {
            int count=m_inventory.GetSlotCount(m_slot), value=m_inventory.GetSlotValue(m_slot);
            if (count!=m_beforeCount || value!=m_beforeValue)
            {
                Require(value==m_output && (m_creative ? m_beforeCount==0 && count==9999 : count==m_beforeCount+1), "无法安全恢复被外部修改的背包。");
                m_inventory.RemoveNetSlotItems(m_slot,m_creative?9999:count-m_beforeCount);
                Require(m_inventory.GetSlotCount(m_slot)==m_beforeCount && m_inventory.GetSlotValue(m_slot)==m_beforeValue, "背包回滚不完整。");
                m_inventory.OnSlotChange(m_slot);
            }
            if (m_target.Entity != null)
            {
                if (!m_target.Entity.IsAddedToProject) m_runtime.Project.AddEntity(m_target.Entity);
                Require(m_target.Entity.IsAddedToProject, "坦克恢复失败。");
            }
            else
            {
                Point3 p=m_target.Cell;
                Require(Cell(p)==0 || Cell(p)==m_target.Value, "目标位置被外部占用，停止回滚。");
                if(Cell(p)==0) m_runtime.TerrainSystem.ChangeCell(p.X,p.Y,p.Z,m_target.Value,true,m_player.ComponentMiner);
                Require(Cell(p)==m_target.Value && m_neighbors.All(k=>Cell(k.Key)==k.Value), "地形/邻域回滚失败。");
            }
            SaveVerified(m_id+"-rolledback");
            Storage.MoveFile(m_pending,Storage.ChangeExtension(m_pending,".rolledback"));
        }
    }
}
