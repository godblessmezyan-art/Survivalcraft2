using System;
using System.Collections.Generic;
using System.Linq;
using Engine;
using Game;
using Game.NetWork;
using GameEntitySystem;
using TemplatesDatabase;

namespace SCIEPort.Features.DevouringEye
{
    /// <summary>World-owned V1 single-object recovery, no client mutation or batch/network requests.</summary>
    public sealed class SubsystemDevouringEye : SubsystemBlockBehavior
    {
        internal SubsystemTerrain TerrainSystem;
        internal SubsystemGameInfo Info;
        internal SubsystemBlockBehaviors Behaviors;
        internal SubsystemBlockEntities BlockEntities;
        internal readonly RecoveryTransaction Transaction = new RecoveryTransaction();
        private readonly Dictionary<ComponentPlayer, RecoveryDialog> m_dialogs = new Dictionary<ComponentPlayer, RecoveryDialog>();
        private readonly Queue<Action> m_requests = new Queue<Action>();
        internal string LastCommit = "";
        private double m_nextOperation;
        /// <inheritdoc />
        public override int[] HandledBlocks => new[] { DevouringEyeBlock.Index };
        /// <inheritdoc />
        public override void Load(ValuesDictionary values)
        {
            base.Load(values);
            TerrainSystem = Project.FindSubsystem<SubsystemTerrain>(true);
            Info = Project.FindSubsystem<SubsystemGameInfo>(true);
            Behaviors = Project.FindSubsystem<SubsystemBlockBehaviors>(true);
            BlockEntities = Project.FindSubsystem<SubsystemBlockEntities>(true);
            LastCommit = values.GetValue("LastCommittedRecovery", "");
            if (CommonLib.WorkType != WorkType.Client && !string.IsNullOrEmpty(Info.DirectoryName) && Info.DirectoryName != "NetWorld")
            {
                string directory = Storage.CombinePaths(Info.DirectoryName, "DevouringEyeJournal");
                try { if (Storage.DirectoryExists(directory) && Storage.ListFileNames(directory).Any(n => n.EndsWith(".pending", StringComparison.Ordinal))) Transaction.Lock(); }
                catch(Exception error) { Transaction.Lock();Log.Error("[SCIEPort:DevouringEye] Cannot inspect recovery journal: "+error); }
            }
        }
        /// <inheritdoc />
        public override void Save(ValuesDictionary values) { values.SetValue("LastCommittedRecovery", LastCommit); }

        /// <inheritdoc />
        public override bool OnUse(Ray3 ray, ComponentMiner miner)
        {
            if (Terrain.ExtractContents(miner.ActiveBlockValue) != DevouringEyeBlock.Index) return false;
            ComponentPlayer player = miner.ComponentPlayer;
            if (player?.GameWidget == null || m_dialogs.ContainsKey(player)) return true;
            try
            {
                RecoveryTarget target = Scan(player, ray);
                if(target!=null)target.ActorPosition=player.ComponentBody.Position;
                var dialog = new RecoveryDialog(this, player, target);
                m_dialogs.Add(player, dialog);
                DialogsManager.ShowDialog(player.GameWidget.GuiWidget, dialog);
            }
            catch(Exception error) { player.ComponentGui.DisplaySmallMessage("吞噬之眼：无法识别目标，未修改世界。",Color.Yellow,false,false);Log.Error("[SCIEPort:DevouringEye] Identify failed: "+error); }
            return true;
        }

        internal RecoveryTarget Scan(ComponentPlayer player, Ray3 ray)
        {
            if (ray.Direction.LengthSquared() < .001f) return null;
            ray.Direction = Vector3.Normalize(ray.Direction);
            Vector3 end = ray.Position + ray.Direction * 6f;
            BodyRaycastResult? body = Project.FindSubsystem<SubsystemBodies>(true).Raycast(ray.Position, end, 0,
                (b, distance) => b.Entity != player.Entity); // Do not filter illegal foreground entities out.
            TerrainRaycastResult? cell = TerrainSystem.Raycast(ray.Position, end, false, true, (value, distance) => Terrain.ExtractContents(value) != 0);
            MovingBlocksRaycastResult? moving = Project.FindSubsystem<SubsystemMovingBlocks>(true).Raycast(ray.Position, end, true);
            float bd = body?.Distance ?? float.PositiveInfinity, cd = cell?.Distance ?? float.PositiveInfinity, md = moving?.Distance ?? float.PositiveInfinity;
            if (float.IsPositiveInfinity(MathUtils.Min(bd, MathUtils.Min(cd, md)))) return null;
            switch (RecoveryRules.Nearest(cd, bd, md))
            {
                case 2: return new RecoveryTarget { Ray = ray, Moving = true, Name = "移动/多方块结构", Distance = md };
                case 1:
                    Entity entity = body.Value.ComponentBody.Entity;
                    return new RecoveryTarget { Ray = ray, Entity = entity, Name = entity.ValuesDictionary.DatabaseObject.Name, Distance = bd };
                default:
                    int value = Game.Terrain.ReplaceLight(cell.Value.Value, 0);
                    return new RecoveryTarget { Ray = ray, Cell = cell.Value.CellFace.Point, Value = value, Name = BlocksManager.Blocks[Game.Terrain.ExtractContents(value)].GetDisplayName(TerrainSystem, value), Distance = cd };
            }
        }

        internal void Authority(ComponentPlayer player)
        {
            if (!player.Entity.IsAddedToProject || player.ComponentHealth.Health <= 0f) throw new InvalidOperationException("玩家已死亡或离开，取消回收。");
            if (CommonLib.WorkType == WorkType.Client) throw new InvalidOperationException("普通客户端仅可识别；请由房主本地执行回收。");
            if (!RecoveryRules.AllowExecutor(CommonLib.WorkType==WorkType.Client,player.PlayerData.IsMainPlayer,CommonLib.WorkType==WorkType.Server,player.PlayerData.ServerManager||player.PlayerData.ServerMaster))
                throw new InvalidOperationException("V1 仅允许单机主玩家或服务器本地管理员执行。");
            if (Info.WorldSettings.GameMode == GameMode.Adventure) throw new InvalidOperationException("冒险世界禁止回收。");
            if (GameManager.Project != Project || string.IsNullOrEmpty(Info.DirectoryName) || Info.DirectoryName == "NetWorld") throw new InvalidOperationException("没有可验证的本地权威存档。");
            if (Transaction.Locked) throw new InvalidOperationException("存在未完成恢复记录，本世界回收已锁定。");
        }

        internal void CheckRegion(ComponentPlayer player, int x, int y, int z)
        {
            if (!RecoveryRules.InBounds(x, y, z)) throw new InvalidOperationException("目标在边界/安全操作范围外。");
            Territoriy territory;
            if (SubsystemBedrockBlockBehavior.CheckIsInTerritoriy(x, z, out territory))
                throw new InvalidOperationException(SubsystemBedrockBlockBehavior.AllowPlayerAction(player, territory) ? "V1 不操作任何原生领地保护区域。" : "你没有此领地权限。");
        }

        internal void Queue(ComponentPlayer player, RecoveryTarget target, RecoveryDialog dialog)
        {
            if (m_requests.Count != 0 || Time.RealTime < m_nextOperation) { dialog.Result("操作过快，请关闭后重新识别。"); return; }
            m_requests.Enqueue(() => {
                string result = Transaction.Execute(new NativeRecovery(this, player, target));
                dialog.Result(result ?? "回收成功：已返还物品并验证存档。\n原目标已移除；正常退出重进不会复生。");
                Log.Information("[SCIEPort:DevouringEye] " + (result ?? "COMMITTED") + " player=" + player.PlayerData.PlayerGUID);
                m_nextOperation = Time.RealTime + .5;
            });
        }
        internal void Tick()
        {
            foreach (var pair in m_dialogs.ToArray())
                if (pair.Key.GameWidget == null || pair.Key.PlayerData.ComponentPlayer != pair.Key || (pair.Value.ParentWidget != null && !DialogsManager.Dialogs.Contains(pair.Value))) { Cancel(pair.Key);Close(pair.Key); }
            if (m_requests.Count > 0) m_requests.Dequeue()(); // Exactly one object, never a region/batch.
        }
        internal void Close(ComponentPlayer player)
        {
            RecoveryDialog dialog;
            if (m_dialogs.TryGetValue(player, out dialog)) { dialog.Release(); DialogsManager.HideDialog(dialog); m_dialogs.Remove(player); }
        }
        internal void Cancel(ComponentPlayer player) { m_requests.Clear(); }
        /// <inheritdoc />
        public override void Dispose()
        {
            m_requests.Clear();
            foreach (ComponentPlayer player in m_dialogs.Keys.ToArray()) Close(player);
            base.Dispose();
        }
    }
}
